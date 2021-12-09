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
    internal class EmitterStateWriter
    {
        private NativeArray<byte> m_PreviousStateSubBuffer;
        private NativeArray<byte> m_CurrentStateBuffer;

        private buint m_CurrentStateBufferEndPos = 0;
        private bool m_CurrentStateResult = false;

        private byte[] m_MsgBuffer = new byte[0];
        public bool ValidRawStateData => m_PreviousStateSubBuffer != default;

        public IEmitterNodeSyncState nodeState;

        internal delegate bool OnStoreCustomData(NativeArray<byte> buffer, ref buint endPos);
        private static OnStoreCustomData onStoreCustomData;

        internal static void RegisterOnStoreCustomDataDelegate (OnStoreCustomData _onStoreCustomData)
        {
            onStoreCustomData -= _onStoreCustomData;
            onStoreCustomData += _onStoreCustomData;
        }

        public EmitterStateWriter (IEmitterNodeSyncState nodeState)
        {
            this.nodeState = nodeState;
            m_CurrentStateBuffer = new NativeArray<byte>(NodeStateConstants.k_MaxFrameNetworkByteBufferSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        public void Dispose ()
        {
            if (m_CurrentStateBuffer.IsCreated)
                m_CurrentStateBuffer.Dispose();
        }

        public unsafe void PublishCurrentState(ulong currentFrameId)
        {
            using (m_PreviousStateSubBuffer)
            {
                var len = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<EmitterLastFrameData>() + m_PreviousStateSubBuffer.Length;

                if (m_MsgBuffer.Length != len)
                    m_MsgBuffer = new byte[len];

                var msg = new EmitterLastFrameData() {FrameNumber = currentFrameId };
                msg.StoreInBuffer(m_MsgBuffer, Marshal.SizeOf<MessageHeader>()); // Leaver room for header

                Marshal.Copy((IntPtr) m_PreviousStateSubBuffer.GetUnsafePtr(), m_MsgBuffer, Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<EmitterLastFrameData>(), m_PreviousStateSubBuffer.Length);

                var msgHdr = new MessageHeader()
                {
                    MessageType = EMessageType.LastFrameData,
                    Flags = MessageHeader.EFlag.Broadcast,
                    PayloadSize = (UInt16)m_PreviousStateSubBuffer.Length
                };

                nodeState.NetworkAgent.PublishMessage(msgHdr, m_MsgBuffer);
            }
        }

        public void GatherFrameState(ulong frame)
        {
            if (m_CurrentStateResult)
            {
                onStoreCustomData?.Invoke(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos);
                if (MarkStatesEnd(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos))
                    m_PreviousStateSubBuffer = new NativeArray<byte>(m_CurrentStateBuffer.GetSubArray(0, (int)m_CurrentStateBufferEndPos), Allocator.Temp);
                else m_PreviousStateSubBuffer = default;
            }

            else m_PreviousStateSubBuffer = default;

            m_CurrentStateBufferEndPos = 0;
            m_CurrentStateResult =
                StoreInputState(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos) &&
                StoreTimeState(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos) &&
                StoreClusterInputState(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos) &&
                StoreRndGeneratorState(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos);
                
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
