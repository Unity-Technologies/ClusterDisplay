using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.RPC.Runtime")]

namespace Unity.ClusterDisplay
{
    public class RepeaterStateReader
    {
        private NativeArray<byte> m_MsgFromEmitter;
        private byte[] m_OutBuffer = new byte[0];

        private IRepeaterNodeSyncState nodeSyncState;
        
        // For debugging ----------------------------------
        private UInt64 m_LastReportedFrameDone = 0;
        private UInt64 m_LastRxFrameStart = 0;
        private UInt64 m_RxCount = 0;
        private UInt64 m_TxCount = 0;
        public UInt64 RxCount => m_RxCount;
        public UInt64 TxCount => m_TxCount;

        public UInt64 LastReportedFrameDone => m_LastReportedFrameDone;
        public UInt64 LastRxFrameStart => m_LastRxFrameStart;

        private ProfilerMarker m_MarkerReceivedGoFromEmitter = new ProfilerMarker("ReceivedGoFromEmitter");
        private DebugPerf m_NetworkingOverhead = new DebugPerf();

        public float NetworkingOverheadAverage => m_NetworkingOverhead.Average;

        internal delegate bool OnRestoreCustomData(NativeArray<byte> stateData);
        private readonly static OnRestoreCustomData[] delegates = new OnRestoreCustomData[byte.MaxValue];

        internal static void RegisterOnRestoreCustomDataDelegate (byte id, OnRestoreCustomData _onRestoreCustomData)
        {
            /*
            if (delegates[id] != null && delegates[id].GetInvocationList().Length > 0)
                throw new Exception($"Unable to register {nameof(OnRestoreCustomData)} with id: {id}, there is already a delegated registered with that ID.");
            */

            delegates[id] = _onRestoreCustomData;
        }

        public RepeaterStateReader (IRepeaterNodeSyncState nodeSyncState) => this.nodeSyncState = nodeSyncState;

        public void PumpMsg (ulong currentFrameID)
        {
            while (nodeSyncState.NetworkAgent.NextAvailableRxMsg(out var msgHdr, out var outBuffer))
            {
                m_RxCount++;

                if (msgHdr.MessageType != EMessageType.LastFrameData)
                {
                    nodeSyncState.OnUnhandledNetworkMessage(msgHdr);
                    continue;
                }
                
                using (m_MarkerReceivedGoFromEmitter.Auto())
                {
                    m_NetworkingOverhead.SampleNow();
                    var respMsg = EmitterLastFrameData.FromByteArray(outBuffer, msgHdr.OffsetToPayload);

                    // The emitter is on the next frame, so were matching against the previous frame.
                    if (respMsg.FrameNumber != currentFrameID)
                    {
                        ClusterDebug.LogWarning( $"Message of type: {msgHdr.MessageType} with sequence ID: {msgHdr.SequenceID} is for frame: {respMsg.FrameNumber} when we are on frame: {currentFrameID}. Any of the following could have occurred:\n\t1. We already interpreted the message, but an ACK was never sent to the emitter.\n\t2. We already interpreted the message, but our ACK never reached the emitter.\n\t3. We some how never received this message. Yet we proceeded to the next frame anyways.");
                        continue;
                    }

                    ClusterDebug.Assert(outBuffer.Length > 0, "invalid buffer!");
                    m_MsgFromEmitter = new NativeArray<byte>(outBuffer, Allocator.Persistent);

                    RestoreEmitterFrameData();
                    nodeSyncState.OnReceivedEmitterFrameData();
                }
            }
        }

        private void RestoreEmitterFrameData()
        {
            try
            {
                // Read the state from the server
                var msgHdr = MessageHeader.FromByteArray(m_MsgFromEmitter);

                // restore states
                unsafe
                {
                    var bufferPos = msgHdr.OffsetToPayload + Marshal.SizeOf<EmitterLastFrameData>();

                    var buffer = (byte*) m_MsgFromEmitter.GetUnsafePtr();
                    ClusterDebug.Assert(buffer != null, "msg buffer is null");

                    do
                    {
                        byte id = (*(buffer + bufferPos++));
                        if ((StateID)id == StateID.End)
                            break;

                        var stateSize = *(int*)(buffer + bufferPos);
                        bufferPos += Marshal.SizeOf<int>();

                        // Reached end of list
                        if (stateSize <= 0)
                        {
                            ClusterDebug.LogWarning($"Received invalid state with id: {id} of size: {stateSize}");
                            break;
                        }

                        var stateData = m_MsgFromEmitter.GetSubArray(bufferPos, stateSize);
                        bufferPos += stateSize;

                        switch ((StateID)id)
                        {
                            case StateID.Time:
                                ClusterSerialization.RestoreTimeManagerState(stateData);
                                break;

                            case StateID.Input:
                                ClusterSerialization.RestoreInputManagerState(stateData);
                                break;

                            case StateID.Random:
                                RestoreRndGeneratorState(stateData);
                                break;

                            case StateID.ClusterInput:
                                ClusterSerialization.RestoreClusterInputState(stateData);
                                break;

                            default:
                            {
                                delegates[(byte)id]?.Invoke(stateData);
                            } break;
                        }

                    } while (true);
                }
            }

            finally
            {
                m_MsgFromEmitter.Dispose();
                m_MsgFromEmitter = default;
            }
        }

        public void SignalEnteringNextFrame (ulong currentFrameID)
        {
            ClusterDebug.Log($"(Frame: {currentFrameID}): Signaling Frame Done.");
            // Send out to server that this repeater has finished with last requested frame.
            var msgHdr = new MessageHeader()
            {
                MessageType = EMessageType.EnterNextFrame,
                DestinationIDs = nodeSyncState.EmitterNodeIdMask,
            };

            var len = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<RepeaterEnteredNextFrame>();
            if (m_OutBuffer.Length != len)
                m_OutBuffer = new byte[len];

            var msg = new RepeaterEnteredNextFrame()
            {
                FrameNumber = currentFrameID
            };

            m_LastReportedFrameDone = currentFrameID;
            msg.StoreInBuffer(m_OutBuffer, Marshal.SizeOf<MessageHeader>());

            m_NetworkingOverhead.RefPoint();
            nodeSyncState.NetworkAgent.PublishMessage(msgHdr, m_OutBuffer);
            m_TxCount++;
        }

        private static unsafe bool RestoreRndGeneratorState(NativeArray<byte> stateData)
        {
            UnityEngine.Random.State rndState = default;
            var rawData = (byte*)&rndState;

            ClusterDebug.Assert(*((UInt64*)stateData.GetUnsafePtr() + 0) != 0 && *((UInt64*)stateData.GetUnsafePtr() + 0) != 0, "invalid rnd state being restored." );

            UnsafeUtility.MemCpy( rawData, (byte*)stateData.GetUnsafePtr(), Marshal.SizeOf<UnityEngine.Random.State>());

            UnityEngine.Random.state = rndState;
            // RuntimeLogWriter.Log($"Seed: {UnityEngine.Random.seed}");
            return true;
        }
    }
}
