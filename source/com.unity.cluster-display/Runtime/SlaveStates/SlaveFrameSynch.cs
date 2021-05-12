using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay.SlaveStateMachine
{
    internal class SynchronizeFrame : SlaveState
    {
        enum EStage
        {
            WaitingOnGoFromMaster,
            ReadyToProcessFrame,
        }

        private EStage m_Stage;
        private NativeArray<byte> m_MsgFromMaster;
        private byte[] m_OutBuffer = new byte[0];

        TimeSpan m_TsOfStage;
        
        // For debugging ----------------------------------
        private UInt64 m_LastReportedFrameDone = 0;
        private UInt64 m_LastRxFrameStart = 0;
        private UInt64 m_RxCount = 0;
        private UInt64 m_TxCount = 0;

        private DebugPerf m_NetworkingOverhead = new DebugPerf();
        ProfilerMarker m_MarkerDoFrame = new ProfilerMarker("SynchronizeFrame::DoFrame");
        ProfilerMarker m_MarkerWaitingOnGoFromMaster = new ProfilerMarker("WaitingOnGoFromMaster");
        ProfilerMarker m_MarkerReadyToProcessFrame = new ProfilerMarker("ReadyToProcessFrame");
        ProfilerMarker m_MarkerReceivedGoFromMaster = new ProfilerMarker("ReceivedGoFromMaster");

        public override string GetDebugString()
        {
            return $"{base.GetDebugString()} / {m_Stage} : {LocalNode.CurrentFrameID}, {m_LastReportedFrameDone}, {m_LastRxFrameStart}, {m_RxCount}, {m_TxCount}" +
            $"\r\nNetwork: {m_NetworkingOverhead.Average * 1000:000.0}";
        }
        //-------------------------------------------------

        public override void InitState()
        {
            m_Stage = EStage.WaitingOnGoFromMaster;
            m_TsOfStage = m_Time.Elapsed;
            m_Cancellation = new CancellationTokenSource();
        }

        protected override NodeState DoFrame(bool newFrame)
        {
            using (m_MarkerDoFrame.Auto())
            {
                switch (m_Stage)
                {
                    case EStage.WaitingOnGoFromMaster:
                    {
                        using (m_MarkerWaitingOnGoFromMaster.Auto())
                        {
                            PumpMsg();
                            
                            if ((m_Time.Elapsed - m_TsOfStage) > MaxTimeOut)
                            {
                                PendingStateChange =
                                    new FatalError(
                                        $"Have not received GO from Master after {MaxTimeOut.TotalMilliseconds}ms.");

                            }

                            // If we just processed the StartFrame message, then the Stage is now set to ReadyToProcessFrame.
                            // This will un-block the player loop to process the frame and next time this method is called (DoFrame)
                            // We will have actually processed a frame and be ready to inform master and wait for next frame start.
                            break;
                        }
                    }

                    case EStage.ReadyToProcessFrame:
                    {
                        if (newFrame)
                        {
                            using (m_MarkerReadyToProcessFrame.Auto())
                            {
                                SignalFrameDone();
                            }
                        }
                        break;
                    }
                }
                return this;
            }
        }

        private void PumpMsg()
        {
            while (LocalNode.UdpAgent.NextAvailableRxMsg(out var msgHdr, out var outBuffer))
            {
                m_RxCount++;

                if (msgHdr.MessageType == EMessageType.StartFrame)
                {
                    using (m_MarkerReceivedGoFromMaster.Auto())
                    {
                        m_NetworkingOverhead.SampleNow();
                        if (msgHdr.PayloadSize > 0)
                        {
                            var respMsg = AdvanceFrame.FromByteArray(outBuffer, msgHdr.OffsetToPayload);
                            m_LastRxFrameStart = respMsg.FrameNumber;
                            if (respMsg.FrameNumber == LocalNode.CurrentFrameID)
                            {
                                Debug.Assert(outBuffer.Length > 0, "invalid buffer!");
                                m_MsgFromMaster = new NativeArray<byte>(outBuffer, Allocator.Persistent);

                                RestoreStates();

                                m_Stage = EStage.ReadyToProcessFrame;
                                m_TsOfStage = m_Time.Elapsed;
                            }
                            else
                            {
                                PendingStateChange =
                                    new FatalError(
                                        $"Received a message from node {msgHdr.OriginID} about a starting frame {respMsg.FrameNumber}, when we are at {LocalNode.CurrentFrameID} (stage: {m_Stage})");
                                break;
                            }
                        }
                    }
                }
                else
                {
                    base.ProcessUnhandledMessage(msgHdr);
                }
            }
        }

        private void SignalFrameDone()
        {
            // Send out to server that this slave has finished with last requested frame.
            var msgHdr = new MessageHeader()
            {
                MessageType = EMessageType.FrameDone,
                DestinationIDs = LocalNode.MasterNodeIdMask,
            };

            var len = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<FrameDone>();
            if (m_OutBuffer.Length != len)
            {
                m_OutBuffer = new byte[len];
            }

            var msg = new FrameDone()
            {
                FrameNumber = LocalNode.CurrentFrameID
            };
            m_LastReportedFrameDone = LocalNode.CurrentFrameID;
            msg.StoreInBuffer(m_OutBuffer, Marshal.SizeOf<MessageHeader>());

            m_Stage = EStage.WaitingOnGoFromMaster;
            m_TsOfStage = m_Time.Elapsed;
            m_NetworkingOverhead.RefPoint();

            LocalNode.CurrentFrameID++;

            LocalNode.UdpAgent.PublishMessage(msgHdr, m_OutBuffer);
            m_TxCount++;
        }

        private void RestoreStates()
        {
            try
            {
                // Read the state from the server
                var msgHdr = MessageHeader.FromByteArray(m_MsgFromMaster);

                // restore states
                unsafe
                {
                    var bufferPos = msgHdr.OffsetToPayload + Marshal.SizeOf<AdvanceFrame>();
                    var buffer = (byte*) m_MsgFromMaster.GetUnsafePtr();
                    Debug.Assert(buffer != null, "msg buffer is null");
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
                            ClusterSerialization.RestoreClusterInputState(stateData);
                        }
                        else if (id == AdvanceFrame.CoreInputStateID)
                        {
                            ClusterSerialization.RestoreInputManagerState(stateData);
                        }
                        else if (id == AdvanceFrame.CoreTimeStateID)
                        {
                            ClusterSerialization.RestoreTimeManagerState(stateData);
                        }
                        else if (id == AdvanceFrame.CoreRandomStateID)
                        {
                            RestoreRndGeneratorState(stateData);
                        }
                        else if (id == AdvanceFrame.RPCStateID)
                        {
                            RestoreRPCState(stateData);
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

        private static unsafe bool RestoreRPCState (NativeArray<byte> stateData)
        {
            Debug.Log($"RPC Buffer Size: {stateData.Length}");
            RPCEmitter.Unlatch(stateData);
            return true;
        }

        private static unsafe bool RestoreRndGeneratorState(NativeArray<byte> stateData)
        {
            UnityEngine.Random.State rndState = default;
            var rawData = (byte*)&rndState;

            Debug.Assert(*((UInt64*)stateData.GetUnsafePtr() + 0) != 0 && *((UInt64*)stateData.GetUnsafePtr() + 0) != 0, "invalid rnd state being restored." );

            UnsafeUtility.MemCpy( rawData, (byte*)stateData.GetUnsafePtr(), Marshal.SizeOf<UnityEngine.Random.State>());

            UnityEngine.Random.state = rndState;
            return true;
        }

        public override bool ReadyToProceed => m_Stage == EStage.ReadyToProcessFrame;
    }
}