﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;

namespace Unity.ClusterDisplay.MasterStateMachine
{
    internal class SynchronizeFrame : MasterState
    {
        enum EStage
        {
            ReadyToSignalStartNewFrame,
            WaitingOnFramesDoneMsgs,
            ProcessFrame,
        }

        private Int64 m_WaitingOnNodes; // bit mask of node id's that we are waiting on to say they are ready for work.
        private EStage m_Stage;
        private TimeSpan m_TsOfStage;

        private NativeArray<byte> m_RawStateData;
        private byte[] m_MsgBuffer = new byte[0];

        public override bool ReadyToProceed => m_Stage == EStage.ProcessFrame;
        ProfilerMarker m_MarkerDoFrame = new ProfilerMarker("SynchronizeFrame::DoFrame");
        ProfilerMarker m_MarkerReadyToSignalStartNewFrame = new ProfilerMarker("ReadyToSignalStartNewFrame");
        ProfilerMarker m_MarkerWaitingOnFramesDoneMsgs = new ProfilerMarker("WaitingOnFramesDoneMsgs");
        ProfilerMarker m_MarkerProcessFrame = new ProfilerMarker("ProcessFrame");
        ProfilerMarker m_MarkerPublishState = new ProfilerMarker("PublishState");

        public override string GetDebugString()
        {
            return $"{base.GetDebugString()} / {(EStage)m_Stage} : {m_WaitingOnNodes}";
        }

        public override void InitState()
        {
            base.InitState();
            m_Stage = (int)EStage.ReadyToSignalStartNewFrame;
            m_TsOfStage = m_Time.Elapsed;
            m_WaitingOnNodes = 0;
        }
        
        protected override NodeState DoFrame(bool newFrame)
        {
            if (LocalNode.TotalExpectedRemoteNodesCount == 0)
            {
                PendingStateChange = new FatalError("No Clients found. Exiting Cluster.");
                return this;
            }

            using (m_MarkerDoFrame.Auto())
            {
                if (newFrame)
                    GatherFrameState();

                switch ((EStage) m_Stage)
                {
                    case EStage.ReadyToSignalStartNewFrame:
                    {
                        using (m_MarkerReadyToSignalStartNewFrame.Auto())
                        {
                            if (m_RawStateData == default) // 1st frame only
                                GatherFrameState();

                            if (m_RawStateData != default)
                            {
                                using (m_MarkerPublishState.Auto())
                                {
                                    PublishCurrentState(m_RawStateData);
                                }

                                m_WaitingOnNodes = (Int64) (LocalNode.UdpAgent.AllNodesMask & ~LocalNode.NodeIDMask);
                                m_Stage = EStage.ProcessFrame;
                            }
                            else
                                Debug.LogError("State buffer is empty!");

                            break;
                        }
                    }

                    case EStage.ProcessFrame:
                    {
                        using (m_MarkerProcessFrame.Auto())
                        {
                            Debug.Assert(newFrame, "this should always be on a new frame.");
                            m_Stage = EStage.WaitingOnFramesDoneMsgs;
                            m_TsOfStage = m_Time.Elapsed;
                            break;
                        }
                    }

                    case EStage.WaitingOnFramesDoneMsgs:
                    {
                        using (m_MarkerWaitingOnFramesDoneMsgs.Auto())
                        {
                            PumpMessages();

                            if ((m_Time.Elapsed - m_TsOfStage) > MaxTimeOut)
                            {
                                KickLateClients();
                                BecomeReadyToSignalStartNewFrame();
                            }

                            if ((m_Time.Elapsed - m_TsOfStage).TotalSeconds > 5)
                            {
                                Debug.Assert(m_WaitingOnNodes != 0,
                                    "Have been waiting on slave nodes 'frame done' for more than 5 seconds!");
                                // One or more clients failed to respond in time!
                                Debug.LogError("The following slaves are late reporting back: " + m_WaitingOnNodes);
                            }

                            break;
                        }
                    }
                }

                return this;
            }
        }

        private unsafe void PublishCurrentState(NativeArray<byte> stateBuffer)
        {
            using (stateBuffer)
            {
                var len = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<AdvanceFrame>() + stateBuffer.Length;
                if (m_MsgBuffer.Length != len)
                {
                    m_MsgBuffer = new byte[len];
                }

                var msg = new AdvanceFrame() {FrameNumber = LocalNode.CurrentFrameID };
                msg.StoreInBuffer(m_MsgBuffer, Marshal.SizeOf<MessageHeader>()); // Leaver room for header

                Marshal.Copy((IntPtr) stateBuffer.GetUnsafePtr(), m_MsgBuffer, Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<AdvanceFrame>(), stateBuffer.Length);

                var msgHdr = new MessageHeader()
                {
                    MessageType = EMessageType.StartFrame,
                    Flags = MessageHeader.EFlag.Broadcast,
                    PayloadSize = (UInt16)stateBuffer.Length
                };

                LocalNode.UdpAgent.PublishMessage(msgHdr, m_MsgBuffer);
            }
        }

        private void PumpMessages()
        {
            while (LocalNode.UdpAgent.NextAvailableRxMsg(out var msgHdr, out var outBuffer))
            {
                switch (msgHdr.MessageType)
                {
                    case EMessageType.FrameDone:
                    {
                        Debug.Assert(m_Stage != (int) EStage.ReadyToSignalStartNewFrame,
                            "Master: received FrameDone msg while not in 'ReadyToSignalStartNewFrame' stage!");

                        var respMsg = FrameDone.FromByteArray(outBuffer, msgHdr.OffsetToPayload);

                        if (respMsg.FrameNumber == LocalNode.CurrentFrameID)
                        {
                            var maskOut = ~((Int64) 1 << msgHdr.OriginID);
                            do
                            {
                                var orgMask = m_WaitingOnNodes;
                                Interlocked.CompareExchange(ref m_WaitingOnNodes, orgMask & maskOut, orgMask);
                            } while ((m_WaitingOnNodes & ((Int64) 1 << msgHdr.OriginID)) != 0);
                        }
                        else
                            PendingStateChange = new FatalError( $"Received a message from node {msgHdr.OriginID} about a completed Past frame {respMsg.FrameNumber}, when we are at {LocalNode.CurrentFrameID}" );

                        break;
                    }

                    default:
                    {
                        ProcessUnhandledMessage(msgHdr);
                        break;
                    }
                }
            }
            
            BecomeReadyToSignalStartNewFrame();
        }

        private void BecomeReadyToSignalStartNewFrame()
        {
            if (m_WaitingOnNodes == 0)
            {
                LocalNode.CurrentFrameID++;
                m_Stage = (int) EStage.ReadyToSignalStartNewFrame;
                m_TsOfStage = m_Time.Elapsed;
            }
        }

        private void KickLateClients()
        {
            Debug.LogError(
                $"The following slaves are late reporting back:{m_WaitingOnNodes} after {MaxTimeOut.TotalMilliseconds}ms. Continuing without them.");
            for (byte id = 0; id < sizeof(UInt64); ++id)
            {
                if ((1 << id & m_WaitingOnNodes) != 0)
                {
                    Debug.LogError($"Unregistering node {id}");
                    LocalNode.UnRegisterNode(id);
                }
            }

            m_WaitingOnNodes = 0;
        }

        private void GatherFrameState()
        {
            var endPos = 0;
            using (var buffer = new NativeArray<byte>(16 * 1024, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                if (
                    StoreInputState(buffer, ref endPos) &&
                    StoreTimeState(buffer, ref endPos) &&
                    StoreClusterInputState(buffer, ref endPos) &&
                    StoreRndGeneratorState(buffer, ref endPos) &&
                    MarkStatesEnd(buffer, ref endPos))
                {
                    m_RawStateData = new NativeArray<byte>(buffer.GetSubArray(0, endPos), Allocator.Temp);
                }
                else
                {
                    m_RawStateData = default;
                }
            }
        }
        
        private static unsafe bool StoreInputState(NativeArray<byte> buffer, ref int endPos)
        {
            var guidLen = Marshal.SizeOf<Guid>();
            
            int sizePos = endPos;
            endPos += Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.CoreInputStateID, guidLen);

            var bytesWritten = ClusterSerialization.SaveInputManagerState(buffer.GetSubArray(endPos, buffer.Length - endPos));
            Debug.Assert(bytesWritten >= 0, "Buffer to small! Input not stored.");
            if (bytesWritten < 0)
                return false;

            endPos += bytesWritten;

            *((int*)((byte*)buffer.GetUnsafePtr() + sizePos)) = bytesWritten;
            return true;
        }

        private static unsafe bool StoreTimeState(NativeArray<byte> buffer, ref int endPos)
        {
            var guidLen = Marshal.SizeOf<Guid>();
            
            int sizePos = endPos;
            endPos += Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.CoreTimeStateID, guidLen);

            var bytesWritten = ClusterSerialization.SaveTimeManagerState(buffer.GetSubArray(endPos, buffer.Length - endPos));
            Debug.Assert(bytesWritten >= 0, "Buffer to small! Time state not stored.");
            if (bytesWritten < 0)
                return false;

            endPos += bytesWritten;

            *((int*)((byte*)buffer.GetUnsafePtr() + sizePos)) = bytesWritten;
            return true;
        }
        
        private static unsafe bool StoreClusterInputState(NativeArray<byte> buffer, ref int endPos)
        {
            var guidLen = Marshal.SizeOf<Guid>();
            
            int startingPos = endPos;
            endPos += Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.ClusterInputStateID, guidLen);

            var bytesWritten = ClusterSerialization.SaveClusterInputState(buffer.GetSubArray(endPos, buffer.Length - endPos));
            Debug.Assert(bytesWritten >= 0, "Buffer to small. ClusterInput not stored.");
            if (bytesWritten < 0)
                return false;

            if (bytesWritten > 0)
            {
                endPos += bytesWritten;
                *((int*) ((byte*) buffer.GetUnsafePtr() + startingPos)) = bytesWritten;
            }
            else
                endPos = startingPos;

            return true;
        }

        private static unsafe bool MarkStatesEnd(NativeArray<byte> buffer, ref int endPos)
        {
            Debug.Assert(endPos < buffer.Length, "Buffer to small to store end marker");
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
