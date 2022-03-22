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

        bool m_Disposed = false;
        private NativeArray<byte> m_StagedStateBuffer;
        private NativeArray<byte> m_CurrentStateBuffer;
        int m_StagedBufferLength = 0;

        private buint m_CurrentStateBufferEndPos = 0;
        private bool m_CurrentStateResult = false;

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

            m_CurrentStateBuffer = new NativeArray<byte>(k_MaxFrameNetworkByteBufferSize, Allocator.Persistent);
            m_StagedStateBuffer = new NativeArray<byte>(k_MaxFrameNetworkByteBufferSize, Allocator.Persistent);
            k_RepeatersDelayed = repeatersDelayed;
        }

        void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }
            
            if (disposing)
            {
                // Dispose managed objects here
            }
            
            // NativeArrays look like regular IDisposable objects but they
            // behave more like unmanaged objects
            if (m_CurrentStateBuffer.IsCreated)
                m_CurrentStateBuffer.Dispose();
            if (m_StagedStateBuffer.IsCreated)
                m_StagedStateBuffer.Dispose();

            m_Disposed = true;
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
            var len = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<EmitterLastFrameData>() + m_StagedBufferLength;

            if (m_MsgBuffer.Length < len)
                m_MsgBuffer = new byte[len];

            var msg = new EmitterLastFrameData() {FrameNumber = currentFrameId };
            msg.StoreInBuffer(m_MsgBuffer, Marshal.SizeOf<MessageHeader>()); // Leaver room for header

            Marshal.Copy((IntPtr) m_StagedStateBuffer.GetUnsafePtr(),
                m_MsgBuffer, 
                Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<EmitterLastFrameData>(),
                m_StagedBufferLength);

            var msgHdr = new MessageHeader()
            {
                MessageType = EMessageType.LastFrameData,
                Flags = MessageHeader.EFlag.Broadcast,
                PayloadSize = (UInt16)m_StagedBufferLength
            };

            nodeState.NetworkAgent.PublishMessage(msgHdr, m_MsgBuffer);
        }

        internal void GatherPreFrameState ()
        {
            m_CurrentStateBufferEndPos = StoreFrameState(m_CurrentStateBuffer);
            m_CurrentStateResult = m_CurrentStateBufferEndPos > 0;
        }

        internal static uint StoreFrameState(NativeArray<byte> buffer, bool markEnd = false)
        {
            uint endPos = 0;
            var success = false;
        
            if (StoreInputState(buffer, ref endPos) &&
                StoreTimeState(buffer, ref endPos) &&
                StoreClusterInputState(buffer, ref endPos) &&
                StoreRndGeneratorState(buffer, ref endPos))
            {
                success = !markEnd || MarkStatesEnd(buffer, ref endPos);
            }

            return success ? endPos : 0;
        }

        private void StageCurrentStateBuffer ()
        {
            if (m_CurrentStateResult)
            {
                onStoreCustomData?.Invoke(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos);

                if (MarkStatesEnd(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos))
                {
                    Swap(ref m_CurrentStateBuffer, ref m_StagedStateBuffer);
                    m_StagedBufferLength = (int) m_CurrentStateBufferEndPos;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Swap<T>(ref T a, ref T b) where T : struct
        {
            (a, b) = (b, a);
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

        private static unsafe bool StoreRndGeneratorState(NativeArray<byte> buffer, ref buint endPos)
        {
            if ((endPos + Marshal.SizeOf<int>() + Marshal.SizeOf<UnityEngine.Random.State>()) >= buffer.Length)
            {
                ClusterDebug.Assert(false, "destination buffer to small to hold state");
                return false;
            }

            var rndState = UnityEngine.Random.state;

            StoreStateID(buffer, ref endPos, (byte)StateID.Random);

            buint sizePos = endPos;
            endPos += (buint)Marshal.SizeOf<int>();

            var rawData = (byte*) &rndState;
            UnsafeUtility.MemCpy((byte*) buffer.GetUnsafePtr() + endPos, rawData, Marshal.SizeOf<UnityEngine.Random.State>());

            buint sizeOfRandomState = (buint)Marshal.SizeOf<UnityEngine.Random.State>();
            endPos += sizeOfRandomState;
            *((buint*)((byte*)buffer.GetUnsafePtr() + sizePos)) = sizeOfRandomState;
            return true;
        }

        internal static unsafe void StoreStateID(NativeArray<byte> buffer, ref buint endPos, byte id) =>
            *((byte*)buffer.GetUnsafePtr() + endPos++) = id;
    }
}
