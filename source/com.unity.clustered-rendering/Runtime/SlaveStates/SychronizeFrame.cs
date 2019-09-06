using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.ClusterRendering.SlaveStateMachine
{
    internal class SynchronizeFrame : SlaveState
    {
        enum EStage
        {
            WaitingOnGoFromMaster,
            MsgFromServerAvailable,
            ReadyToProcessFrame,
        }

        private UInt64 m_CurrentFrameID;
        private EStage m_Stage;
        private NativeArray<byte> m_MsgFromMaster;

        public SynchronizeFrame(SlavedNode node) : base(node)
        {
        }

        public override void InitState()
        {
            m_CurrentFrameID = 0;
            m_Stage = EStage.WaitingOnGoFromMaster;

            m_Cancellation = new CancellationTokenSource();
            m_Task = Task.Run(() => ProcessMessages(m_Cancellation.Token), m_Cancellation.Token);
        }

        protected override BaseState DoFrame(bool frameAdvance)
        {
            switch (m_Stage)
            {
                case EStage.WaitingOnGoFromMaster:
                {
                    // As a slave node, we will wait forever
                    break;
                }

                case EStage.MsgFromServerAvailable:
                {
                    Debug.Assert(m_MsgFromMaster != default);
                    if (m_MsgFromMaster != default)
                    {
                        RestoreStates();
                        m_Stage = EStage.ReadyToProcessFrame;
                    }
                    break;
                }
                case EStage.ReadyToProcessFrame:
                {
                    if (frameAdvance)
                    {
                        SignalFrameDone();
                        m_Stage = EStage.WaitingOnGoFromMaster;
                    }
                    break;
                }
            }

            return this;
        }

        private void SignalFrameDone()
        {
            // Send out to server that this slave has finished with last requested frame.
            var msgHdr = new MessageHeader()
            {
                MessageType = EMessageType.FrameDone,
                DestinationIDs = m_Node.MasterNodeIdMask,
            };

            var outBuffer = new byte[Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<FrameDone>()];
            var msg = new FrameDone()
            {
                FrameNumber = m_CurrentFrameID
            };
            msg.StoreInBuffer(outBuffer, Marshal.SizeOf<MessageHeader>());

            m_Node.UdpAgent.PublishMessage(msgHdr, outBuffer);

            m_CurrentFrameID++;
        }

        private void RestoreStates()
        {
            try
            {
                // Read the state from the server
                var msgHdr = MessageHeader.FromByteArray(m_MsgFromMaster);
                var mixedStateFormat = msgHdr.Flags.HasFlag(MessageHeader.EFlag.SentFromEditorProcess) != Application.isEditor;

                if(mixedStateFormat)
                    Debug.LogError("Partial data synch due to mixed state format (editor vs player)");

                // restore states
                unsafe
                {
                    var bufferPos = msgHdr.OffsetToPayload + Marshal.SizeOf<AdvanceFrame>();
                    var buffer = (byte*) m_MsgFromMaster.GetUnsafePtr();
                    Debug.Assert(buffer != null);
                    do
                    {
                        // Read size of state buffer
                        var stateSize = *(int*) (buffer + bufferPos);
                        bufferPos += Marshal.SizeOf<int>();

                        // Reached end of list
                        if (stateSize == 0)
                            break;

                        // Get state identifier
                        Guid id;
                        UnsafeUtility.MemCpy(&id, buffer + bufferPos, Marshal.SizeOf<Guid>());
                        bufferPos += Marshal.SizeOf<Guid>();

                        var stateData = m_MsgFromMaster.GetSubArray(bufferPos, stateSize);
                        bufferPos += stateSize;

                        if (id == AdvanceFrame.ClusterInputStateID)
                        {
                            if (!mixedStateFormat)
                                ClusterInput.RestoreState(stateData);
                        }
                        else if (id == AdvanceFrame.CoreInputStateID)
                        {
                            if (!mixedStateFormat)
                                InputManager.RestoreState(stateData);
                        }
                        else if (id == AdvanceFrame.CoreTimeStateID)
                        {
                            if (!mixedStateFormat)
                                TimeManager.RestoreState(stateData);
                        }
                        else if (id == AdvanceFrame.CoreRandomStateID)
                        {
                            RestoreRndGeneratorState(stateData);
                        }
                        else
                        {
                            // Send out to user provided handlers
                        }
                    } while (true);
                }
            }
            finally
            {
                m_MsgFromMaster.Dispose();
                m_MsgFromMaster = default;
            }
        }

        private static unsafe bool RestoreRndGeneratorState(NativeArray<byte> stateData)
        {
            UnityEngine.Random.State rndState = default;
            var rawData = (byte*)&rndState;

            Debug.Assert(*((UInt64*)stateData.GetUnsafePtr() + 0) != 0 && *((UInt64*)stateData.GetUnsafePtr() + 0) != 0);

            UnsafeUtility.MemCpy( rawData, (byte*)stateData.GetUnsafePtr(), Marshal.SizeOf<UnityEngine.Random.State>());

            UnityEngine.Random.state = rndState;
            return true;
        }

        private void ProcessMessages(CancellationToken ctk)
        {
            try
            {
                while (!ctk.IsCancellationRequested)
                {
                    while (m_Node.UdpAgent.NextAvailableRxMsg(out var msgHdr, out var outBuffer))
                    {
                        if (msgHdr.MessageType == EMessageType.StartFrame)
                        {
                            Debug.Assert( msgHdr.PayloadSize > 0 );
                            if (msgHdr.PayloadSize > 0)
                            {
                                Debug.Assert(m_Stage == EStage.WaitingOnGoFromMaster);
                                Debug.Assert(m_MsgFromMaster == default );
                                var respMsg = AdvanceFrame.FromByteArray(outBuffer, msgHdr.OffsetToPayload);
                                if (respMsg.FrameNumber == m_CurrentFrameID)
                                {
                                    Debug.Assert(m_Stage == EStage.WaitingOnGoFromMaster);
                                    Debug.Assert(outBuffer.Length > 0);
                                    m_MsgFromMaster = new NativeArray<byte>(outBuffer, Allocator.Persistent);
                                    m_Stage = EStage.MsgFromServerAvailable;
                                }
                                else
                                    Debug.Log( $"Received a message from node {msgHdr.OriginID} about a starting frame {respMsg.FrameNumber}, when we are at {m_CurrentFrameID}");
                            }
                        }
                        else
                        {
                            base.ProcessUnhandledMessage(msgHdr);
                        }
                    }

                    m_Node.UdpAgent.RxWait.WaitOne(500);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public override bool ReadyToProceed => m_Stage == EStage.ReadyToProcessFrame;
    }
}