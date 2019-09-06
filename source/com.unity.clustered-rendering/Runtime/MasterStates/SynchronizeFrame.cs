using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.ClusterRendering.MasterStateMachine
{
    internal class SynchronizeFrame : MasterState
    {
        enum EStage
        {
            ReadyToSignalStartNewFrame,
            WaitingOnFramesDoneMsgs,
            ProcessFrame,
        }

        private UInt64 m_WaitingOnNodes; // bit mask of node id's that we are waiting on to say they are ready for work.
        private UInt64 m_CurrentFrameID;
        private EStage m_Stage;
        private TimeSpan m_TsOfStage;

        public override bool ReadyToProceed => m_Stage == EStage.ProcessFrame;

        public SynchronizeFrame(MasterNode node) : base(node)
        {
        }

        public override void InitState()
        {
            m_Stage = EStage.ReadyToSignalStartNewFrame;
            m_TsOfStage = m_Time.Elapsed;
            m_WaitingOnNodes = 0;
            m_CurrentFrameID = UInt64.MaxValue;
            for (int i = 0; i < m_Node.m_RemoteNodes.Count; i++)
            {
                m_Node.m_RemoteNodes[i].readyToProcessFrameID = 0;
            }

            m_Cancellation = new CancellationTokenSource();
            m_Task = Task.Run(() => ProcessMessages(m_Cancellation.Token), m_Cancellation.Token);
        }
        
        protected override BaseState DoFrame(bool frameAdvance)
        {
            switch (m_Stage)
            {
                case EStage.ReadyToSignalStartNewFrame:
                {
                    var stateBuffer = GatherFrameState();
                    if (stateBuffer != default)
                        PublishCurrentState(stateBuffer);
                    break;
                }

                case EStage.ProcessFrame:
                {
                    m_Stage = EStage.WaitingOnFramesDoneMsgs;
                    m_TsOfStage = m_Time.Elapsed;
                    break;
                }

                case EStage.WaitingOnFramesDoneMsgs:
                {
                    if ((m_Time.Elapsed - m_TsOfStage).TotalSeconds > 5)
                    {
                        Debug.Assert(m_WaitingOnNodes != 0);
                        // One or more clients failed to respond in time!
                        Debug.LogError("The following slaves are late reporting back: " + m_WaitingOnNodes);
                    }

                    break;
                }
            }

            return this;
        }

        private unsafe void PublishCurrentState(NativeArray<byte> stateBuffer)
        {
            using (stateBuffer)
            {
                var msgBuffer = new byte[Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<AdvanceFrame>() + stateBuffer.Length];

                var msg = new AdvanceFrame() {FrameNumber = ++m_CurrentFrameID};
                msg.StoreInBuffer(msgBuffer, Marshal.SizeOf<MessageHeader>()); // Leaver room for header

                Marshal.Copy((IntPtr) stateBuffer.GetUnsafePtr(), msgBuffer, Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<AdvanceFrame>(), stateBuffer.Length);

                var msgHdr = new MessageHeader()
                {
                    MessageType = EMessageType.StartFrame,
                    Flags = MessageHeader.EFlag.Broadcast,
                    PayloadSize = (UInt16)stateBuffer.Length
                };

                m_WaitingOnNodes = m_Node.UdpAgent.AllNodesMask & ~m_Node.NodeIDMask;
                m_Stage = EStage.ProcessFrame;

                m_Node.UdpAgent.PublishMessage(msgHdr, msgBuffer);
            }
        }

        private void ProcessMessages(CancellationToken ctk)
        {
            try
            {
                while (!ctk.IsCancellationRequested)
                {
                    while (m_Node.UdpAgent.NextAvailableRxMsg(out var msgHdr, out var outBuffer))
                    {
                        switch (msgHdr.MessageType)
                        {
                            case EMessageType.FrameDone:
                            {
                                Debug.Assert(m_Stage != EStage.ReadyToSignalStartNewFrame);

                                var respMsg = FrameDone.FromByteArray(outBuffer, msgHdr.OffsetToPayload);
                                if (respMsg.FrameNumber == m_CurrentFrameID)
                                {
                                    m_WaitingOnNodes &= ~((UInt64) 1 << msgHdr.OriginID);
                                    Debug.Log("Msg from slave: Frame Done");
                                }
                                else
                                    Debug.Log(
                                        $"Received a message from node {msgHdr.OriginID} about a completed Past frame {respMsg.FrameNumber}, when we are at {m_CurrentFrameID}");
                                break;
                            }

                            default:
                            {
                                ProcessUnhandledMessage(msgHdr);
                                break;
                            }
                        }
                    }

                    if (m_WaitingOnNodes == 0 && m_Stage != EStage.ReadyToSignalStartNewFrame)
                    {
                        m_Stage = EStage.ReadyToSignalStartNewFrame;
                        m_TsOfStage = m_Time.Elapsed;
                    }

                    m_Node.UdpAgent.RxWait.WaitOne(500);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                m_AsyncStateChange = new FatalError() {Message = "Oups. " + e};
                throw;
            }
        }

        private NativeArray<byte> GatherFrameState()
        {
            var endPos = 0;
            using (var buffer = new NativeArray<byte>(16 * 1024, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
/*                
                if (!StoreInputState(buffer, ref endPos))
                    return new NativeArray<byte>();

                if (!StoreTimeState(buffer, ref endPos))
                    return new NativeArray<byte>();

                if (!StoreClusterInputState(buffer, ref endPos))
                    return default;
  */
                if (!StoreRndGeneratorState(buffer, ref endPos))
                    return default;

                if ( !MarkStatesEnd(buffer, ref endPos) )
                    return default;



                var subArray = buffer.GetSubArray(0, endPos);

                return new NativeArray<byte>(subArray, Allocator.Temp);
            }
        }
        
        private static unsafe bool StoreInputState(NativeArray<byte> buffer, ref int endPos)
        {
            var guidLen = Marshal.SizeOf<Guid>();

            int bytesWritten;
            int sizePos = endPos;
            endPos += Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.CoreInputStateID, guidLen);

            InputManager.SaveState(buffer, endPos, out bytesWritten);
            Debug.Assert(bytesWritten >= 0);
            if (bytesWritten < 0)
                return false;

            endPos += bytesWritten;

            *((int*)((byte*)buffer.GetUnsafePtr() + sizePos)) = bytesWritten;
            return true;
        }

        private static unsafe bool StoreTimeState(NativeArray<byte> buffer, ref int endPos)
        {
            var guidLen = Marshal.SizeOf<Guid>();

            int bytesWritten;
            int sizePos = endPos;
            endPos += Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.CoreTimeStateID, guidLen);

            UnityEngine.TimeManager.SaveState(buffer, endPos, out bytesWritten);
            Debug.Assert(bytesWritten >= 0);
            if (bytesWritten < 0)
                return false;

            endPos += bytesWritten;

            *((int*)((byte*)buffer.GetUnsafePtr() + sizePos)) = bytesWritten;
            return true;
        }
        
        private static unsafe bool StoreClusterInputState(NativeArray<byte> buffer, ref int endPos)
        {
            var guidLen = Marshal.SizeOf<Guid>();

            int bytesWritten;
            int sizePos = endPos;
            endPos += Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.ClusterInputStateID, guidLen);

            ClusterInput.SaveState(buffer, endPos, out bytesWritten);
            Debug.Assert(bytesWritten >= 0);
            if (bytesWritten < 0)
                return false;

            endPos += bytesWritten;

            *((int*) ((byte*) buffer.GetUnsafePtr() + sizePos)) = bytesWritten;
            return true;
        }

        private static unsafe bool MarkStatesEnd(NativeArray<byte> buffer, ref int endPos)
        {
            Debug.Assert(endPos < buffer.Length);
            if (endPos >= buffer.Length)
                return false;

            *((int*)((byte*)buffer.GetUnsafePtr() + endPos)) = 0;
            endPos += Marshal.SizeOf<int>();
            return true;
        }

        private static unsafe bool StoreRndGeneratorState(NativeArray<byte> buffer, ref int endPos)
        {
            if ((endPos + Marshal.SizeOf<int>() + Marshal.SizeOf<UnityEngine.Random.State>()) >= buffer.Length)
            {
                Debug.Assert(false, "destination buffer to small to hold state");
                return false;
            }

            var guidLen = Marshal.SizeOf<Guid>();

            var rndState = UnityEngine.Random.state;

            int sizePos = endPos;
            endPos += Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.CoreRandomStateID, guidLen);

            var rawData = (byte*) &rndState;
            UnsafeUtility.MemCpy((byte*) buffer.GetUnsafePtr() + endPos, rawData, Marshal.SizeOf<UnityEngine.Random.State>());
            endPos += Marshal.SizeOf<UnityEngine.Random.State>();

            *((int*)((byte*)buffer.GetUnsafePtr() + sizePos)) = Marshal.SizeOf<UnityEngine.Random.State>();
            return true;
        }

        private static unsafe int StoreStateID(NativeArray<byte> buffer, int endPos, Guid id, int guidLen)
        {
            UnsafeUtility.MemCpy((byte*) buffer.GetUnsafePtr() + endPos, (byte*) &id, guidLen);
            endPos += guidLen;
            return endPos;
        }
    }
}
