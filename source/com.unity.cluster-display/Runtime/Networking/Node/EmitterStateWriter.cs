using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using buint = System.UInt32;

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.RPC.Runtime")]

namespace Unity.ClusterDisplay
{
    internal class EmitterStateWriter : IDisposable
    {
        private const int k_MaxFrameNetworkByteBufferSize = ushort.MaxValue;

        private NativeArray<byte> m_StagedStateBuffer;
        private NativeArray<byte> m_CurrentStateBuffer;

        private buint m_CurrentStateBufferEndPos = 0;
        private bool m_CurrentStateResult = false;

        private UnityEngine.Random.State previousFrameRndState;
        private byte[] m_MsgBuffer = new byte[0];
        
        public bool ValidRawStateData => m_StagedStateBuffer != default;

        public IEmitterNodeSyncState nodeState;

        internal delegate bool OnStoreCustomData(NativeArray<byte> buffer, ref buint endPos);
        private static OnStoreCustomData onStoreCustomData;

        private readonly bool k_RepeatersDelayed;

        internal static void RegisterOnStoreCustomDataDelegate (OnStoreCustomData _onStoreCustomData)
        {
            onStoreCustomData -= _onStoreCustomData;
            onStoreCustomData += _onStoreCustomData;
        }

        public EmitterStateWriter (IEmitterNodeSyncState nodeState, bool repeatersDelayed)
        {
            this.nodeState = nodeState;
            previousFrameRndState = UnityEngine.Random.state;

            m_CurrentStateBuffer = new NativeArray<byte>(k_MaxFrameNetworkByteBufferSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            k_RepeatersDelayed = repeatersDelayed;
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                // TODO: Dispose managed objects
            }
            
            if (m_CurrentStateBuffer.IsCreated)
                m_CurrentStateBuffer.Dispose();
            if (m_StagedStateBuffer.IsCreated)
                m_StagedStateBuffer.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~EmitterStateWriter()
        {
            Dispose(false);
        }

        public unsafe void PublishCurrentState(ulong currentFrameId)
        {
            using (m_StagedStateBuffer)
            {
                var len = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<EmitterLastFrameData>() + m_StagedStateBuffer.Length;

                if (m_MsgBuffer.Length != len)
                    m_MsgBuffer = new byte[len];

                var msg = new EmitterLastFrameData() {FrameNumber = currentFrameId };
                msg.StoreInBuffer(m_MsgBuffer, Marshal.SizeOf<MessageHeader>()); // Leaver room for header

                Marshal.Copy((IntPtr) m_StagedStateBuffer.GetUnsafePtr(), m_MsgBuffer, Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<EmitterLastFrameData>(), m_StagedStateBuffer.Length);

                var msgHdr = new MessageHeader()
                {
                    MessageType = EMessageType.LastFrameData,
                    Flags = MessageHeader.EFlag.Broadcast,
                    PayloadSize = (UInt16)m_StagedStateBuffer.Length
                };

                nodeState.NetworkAgent.PublishMessage(msgHdr, m_MsgBuffer);
            }
        }

        public void GatherPreFrameState ()
        {
            m_CurrentStateBufferEndPos = 0;
            m_CurrentStateResult =
                StoreInputState(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos) &&
                StoreTimeState(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos) &&
                StoreClusterInputState(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos) &&
                StoreRndGeneratorState(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos);
        }

        private void FlushPreviousStateSubBuffer ()
        {
            if (m_StagedStateBuffer.IsCreated)
            {
                m_StagedStateBuffer.Dispose();
            }

            m_StagedStateBuffer = default;
        }

        private void StageCurrentStateBuffer ()
        {
            if (m_CurrentStateResult)
            {
                onStoreCustomData?.Invoke(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos);

                if (MarkStatesEnd(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos))
                {
                    m_StagedStateBuffer = new NativeArray<byte>(m_CurrentStateBuffer.GetSubArray(0, (int)m_CurrentStateBufferEndPos), Allocator.Temp);
                }
                else
                {
                    FlushPreviousStateSubBuffer();
                }
            }

            else
            {
                FlushPreviousStateSubBuffer();
            }
        }

        public void GatherFrameState(ulong frame)
        {
            if (!k_RepeatersDelayed)
            {
                GatherPreFrameState();
                StageCurrentStateBuffer();

                return;
            }

            StageCurrentStateBuffer();
            GatherPreFrameState();
        }

        private static unsafe bool StoreInputState(NativeArray<byte> buffer, ref buint endPos)
        {
            var guidLen = Marshal.SizeOf<Guid>();
            
            StoreStateID(buffer, ref endPos, (byte)StateID.Input);

            buint sizePos = endPos;
            endPos += (buint)Marshal.SizeOf<int>();

            buint bytesWritten = (buint)ClusterSerialization.SaveInputManagerState(buffer.GetSubArray((int)endPos, (int)(buffer.Length - endPos)));
            ClusterDebug.Assert(bytesWritten >= 0, "Buffer to small! Input not stored.");
            if (bytesWritten < 0)
                return false;

            endPos += bytesWritten;

            *((buint*)((byte*)buffer.GetUnsafePtr() + sizePos)) = bytesWritten;
            return true;
        }

        private static unsafe bool StoreTimeState(NativeArray<byte> buffer, ref buint endPos)
        {
            var guidLen = Marshal.SizeOf<Guid>();
            
            StoreStateID(buffer, ref endPos, (byte)StateID.Time);

            buint sizePos = endPos;
            endPos += (buint)Marshal.SizeOf<int>();

            buint bytesWritten = (buint)ClusterSerialization.SaveTimeManagerState(buffer.GetSubArray((int)endPos, (int)(buffer.Length - endPos)));
            ClusterDebug.Assert(bytesWritten >= 0, "Buffer to small! Time state not stored.");
            if (bytesWritten < 0)
                return false;

            endPos += bytesWritten;

            *((buint*)((byte*)buffer.GetUnsafePtr() + sizePos)) = bytesWritten;
            return true;
        }
        
        private static unsafe bool StoreClusterInputState(NativeArray<byte> buffer, ref buint endPos)
        {
            var guidLen = Marshal.SizeOf<Guid>();
            
            StoreStateID(buffer, ref endPos, (byte)StateID.ClusterInput);

            buint sizePos = endPos;
            endPos += (buint)Marshal.SizeOf<int>();

            var bytesWritten = (buint)ClusterSerialization.SaveClusterInputState(buffer.GetSubArray((int)endPos, (int)(buffer.Length - endPos)));
            ClusterDebug.Assert(bytesWritten >= 0, "Buffer to small. ClusterInput not stored.");
            if (bytesWritten < 0)
                return false;

            if (bytesWritten > 0)
            {
                endPos += bytesWritten;
                *((buint*) ((byte*) buffer.GetUnsafePtr() + sizePos)) = bytesWritten;
            }

            else endPos = sizePos;
            return true;
        }

        private static unsafe bool MarkStatesEnd(NativeArray<byte> buffer, ref buint endPos)
        {
            ClusterDebug.Assert(endPos < buffer.Length, "Buffer to small to store end marker");
            if (endPos >= buffer.Length)
                return false;

            *((buint*)((byte*)buffer.GetUnsafePtr() + endPos)) = 0;
            endPos += (buint)Marshal.SizeOf<int>();

            return true;
        }

        private unsafe bool StoreRndGeneratorState(NativeArray<byte> buffer, ref buint endPos)
        {
            if ((endPos + Marshal.SizeOf<int>() + Marshal.SizeOf<UnityEngine.Random.State>()) >= buffer.Length)
            {
                ClusterDebug.Assert(false, "destination buffer to small to hold state");
                return false;
            }

            var rndState = previousFrameRndState;

            StoreStateID(buffer, ref endPos, (byte)StateID.Random);

            buint sizePos = endPos;
            endPos += (buint)Marshal.SizeOf<int>();

            var rawData = (byte*) &rndState;
            UnsafeUtility.MemCpy((byte*) buffer.GetUnsafePtr() + endPos, rawData, Marshal.SizeOf<UnityEngine.Random.State>());

            buint sizeOfRandomState = (buint)Marshal.SizeOf<UnityEngine.Random.State>();
            endPos += sizeOfRandomState;
            *((buint*)((byte*)buffer.GetUnsafePtr() + sizePos)) = sizeOfRandomState;
            
            previousFrameRndState = UnityEngine.Random.state;
            return true;
        }

        internal static unsafe void StoreStateID(NativeArray<byte> buffer, ref buint endPos, byte id) =>
            *((byte*)buffer.GetUnsafePtr() + endPos++) = id;
    }
}
