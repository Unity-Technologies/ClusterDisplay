using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.RPC")]

namespace Unity.ClusterDisplay
{
    internal class RepeaterStateReader
    {
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

        internal delegate bool OnLoadCustomData(NativeArray<byte> stateData);

        static readonly Dictionary<byte, OnLoadCustomData> k_BuiltInOnLoadDelegates = new() {{(byte)StateID.Time, ClusterSerialization.RestoreTimeManagerState}, {(byte)StateID.Input, ClusterSerialization.RestoreInputManagerState}, {(byte)StateID.Random, RestoreRndGeneratorState}};

        static readonly Dictionary<byte, OnLoadCustomData> k_LoadDataDelegates = k_BuiltInOnLoadDelegates.ToDictionary(
            entry => entry.Key,
            entry => entry.Value);

        internal static void RegisterOnLoadDataDelegate(byte id, OnLoadCustomData onLoadData) =>
            k_LoadDataDelegates[id] = onLoadData;

        internal static void UnregisterOnLoadDataDelegate(byte id) => k_LoadDataDelegates.Remove(id);

        internal static void ClearOnLoadDataDelegates()
        {
            k_LoadDataDelegates.Clear();
            foreach (var entry in k_BuiltInOnLoadDelegates)
            {
                k_LoadDataDelegates.Add(entry.Key, entry.Value);
            }
        }

        public RepeaterStateReader(IRepeaterNodeSyncState nodeSyncState) => this.nodeSyncState = nodeSyncState;

        public void PumpMsg(ulong currentFrameID)
        {
            var agent = nodeSyncState.NetworkAgent;
            while (agent.NextAvailableRxMsg(out var msgHdr, out var outBuffer))
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
                    ClusterDebug.Assert(outBuffer.Length > 0, "invalid buffer!");

                    var respMsg = outBuffer.LoadStruct<EmitterLastFrameData>(msgHdr.OffsetToPayload);

                    // The emitter is on the next frame, so were matching against the previous frame.
                    if (respMsg.FrameNumber != currentFrameID)
                    {
                        ClusterDebug.LogWarning($"Message of type: {msgHdr.MessageType} with sequence ID: {msgHdr.SequenceID} is for frame: {respMsg.FrameNumber} when we are on frame: {currentFrameID}. Any of the following could have occurred:\n\t1. We already interpreted the message, but an ACK was never sent to the emitter.\n\t2. We already interpreted the message, but our ACK never reached the emitter.\n\t3. We some how never received this message. Yet we proceeded to the next frame anyways.");
                        continue;
                    }

                    RestoreEmitterFrameData(outBuffer);
                    nodeSyncState.OnReceivedEmitterFrameData();
                }
            }
        }

        void RestoreEmitterFrameData(byte[] buffer)
        {
            // Read the frame data from the emitter
            var msgHdr = buffer.LoadStruct<MessageHeader>();
            var bufferPos = msgHdr.OffsetToPayload + Marshal.SizeOf<EmitterLastFrameData>();
            var bufferLength = buffer.Length - bufferPos;
            if (bufferLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(buffer), $"{nameof(buffer)} does not contain frame data");
            }

            using var bufferNative = new NativeArray<byte>(buffer, Allocator.Temp);
            foreach (var (id, data) in new FrameDataReader(bufferNative.GetSubArray(bufferPos, bufferLength)))
            {
                // The built-in delegates restore the states of various subsystems
                if (k_LoadDataDelegates.TryGetValue(id, out var onLoadCustomData))
                {
                    onLoadCustomData.Invoke(data);
                }
            }
        }

        public void SignalEnteringNextFrame(ulong currentFrameID)
        {
            ClusterDebug.Log($"(Frame: {currentFrameID}): Signaling Frame Done.");

            // Send out to server that this repeater has finished with last requested frame.
            var msgHdr = new MessageHeader() {MessageType = EMessageType.EnterNextFrame, DestinationIDs = nodeSyncState.EmitterNodeIdMask,};

            var len = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<RepeaterEnteredNextFrame>();
            if (m_OutBuffer.Length != len)
                m_OutBuffer = new byte[len];

            var msg = new RepeaterEnteredNextFrame() {FrameNumber = currentFrameID};

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

            ClusterDebug.Assert(*((UInt64*)stateData.GetUnsafePtr() + 0) != 0 && *((UInt64*)stateData.GetUnsafePtr() + 0) != 0, "invalid rnd state being restored.");

            UnsafeUtility.MemCpy(rawData, (byte*)stateData.GetUnsafePtr(), Marshal.SizeOf<UnityEngine.Random.State>());

            UnityEngine.Random.state = rndState;

            // RuntimeLogWriter.Log($"Seed: {UnityEngine.Random.seed}");
            return true;
        }
    }
}
