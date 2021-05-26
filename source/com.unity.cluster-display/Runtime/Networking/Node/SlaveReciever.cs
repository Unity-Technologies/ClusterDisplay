using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public class SlaveReciever
    {
        private NativeArray<byte> m_MsgFromMaster;
        private byte[] m_OutBuffer = new byte[0];

        private ISlaveNodeSyncState nodeSyncState;
        
        // For debugging ----------------------------------
        private UInt64 m_LastReportedFrameDone = 0;
        private UInt64 m_LastRxFrameStart = 0;
        private UInt64 m_RxCount = 0;
        private UInt64 m_TxCount = 0;
        public UInt64 RxCount => m_RxCount;
        public UInt64 TxCount => m_TxCount;

        public UInt64 LastReportedFrameDone => m_LastReportedFrameDone;
        public UInt64 LastRxFrameStart => m_LastRxFrameStart;

        private ProfilerMarker m_MarkerReceivedGoFromMaster = new ProfilerMarker("ReceivedGoFromMaster");
        private DebugPerf m_NetworkingOverhead = new DebugPerf();
        public float NetworkingOverheadAverage => m_NetworkingOverhead.Average;

        public SlaveReciever (ISlaveNodeSyncState nodeSyncState)
        {
            this.nodeSyncState = nodeSyncState;
            RPCEmitter.Initialize();
        }

        public void PumpMsg (ulong currentFrameID)
        {
            while (nodeSyncState.NetworkAgent.NextAvailableRxMsg(out var msgHdr, out var outBuffer))
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
                            if (respMsg.FrameNumber == currentFrameID)
                            {
                                Debug.Assert(outBuffer.Length > 0, "invalid buffer!");
                                m_MsgFromMaster = new NativeArray<byte>(outBuffer, Allocator.Persistent);

                                RestoreStates();
                                nodeSyncState.OnPumpedMsg();
                            }
                            else
                            {
                                nodeSyncState.OnNonMatchingFrame(msgHdr.OriginID, respMsg.FrameNumber);
                                break;
                            }
                        }
                    }
                }

                else nodeSyncState.OnUnhandledNetworkMessage(msgHdr);
            }
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
                            ClusterSerialization.RestoreClusterInputState(stateData);
                        else if (id == AdvanceFrame.CoreInputStateID)
                            ClusterSerialization.RestoreInputManagerState(stateData);
                        else if (id == AdvanceFrame.CoreTimeStateID)
                            ClusterSerialization.RestoreTimeManagerState(stateData);
                        else if (id == AdvanceFrame.CoreRandomStateID)
                            RestoreRndGeneratorState(stateData);
                        else if (id == AdvanceFrame.RPCStateID)
                            RestoreRPCState(stateData);

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

        public void SignalFrameDone (ulong currentFrameID)
        {
            // Send out to server that this slave has finished with last requested frame.
            var msgHdr = new MessageHeader()
            {
                MessageType = EMessageType.FrameDone,
                DestinationIDs = nodeSyncState.MasterNodeIdMask,
            };

            var len = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<FrameDone>();
            if (m_OutBuffer.Length != len)
            {
                m_OutBuffer = new byte[len];
            }

            var msg = new FrameDone()
            {
                FrameNumber = currentFrameID
            };

            m_LastReportedFrameDone = currentFrameID;
            msg.StoreInBuffer(m_OutBuffer, Marshal.SizeOf<MessageHeader>());

            m_NetworkingOverhead.RefPoint();
            nodeSyncState.OnPublishingMsg();

            nodeSyncState.NetworkAgent.PublishMessage(msgHdr, m_OutBuffer);
            m_TxCount++;
        }

        private unsafe bool RestoreRPCState (NativeArray<byte> stateData)
        {
            // Debug.Log($"RPC Buffer Size: {stateData.Length}");
            RPCEmitter.Unlatch(stateData, m_LastRxFrameStart);
            return true;
        }

        private static unsafe bool RestoreRndGeneratorState(NativeArray<byte> stateData)
        {
            UnityEngine.Random.State rndState = default;
            var rawData = (byte*)&rndState;

            Debug.Assert(*((UInt64*)stateData.GetUnsafePtr() + 0) != 0 && *((UInt64*)stateData.GetUnsafePtr() + 0) != 0, "invalid rnd state being restored." );

            UnsafeUtility.MemCpy( rawData, (byte*)stateData.GetUnsafePtr(), Marshal.SizeOf<UnityEngine.Random.State>());

            UnityEngine.Random.state = rndState;
            // Debug.Log($"Seed: {UnityEngine.Random.seed}");
            return true;
        }
    }
}
