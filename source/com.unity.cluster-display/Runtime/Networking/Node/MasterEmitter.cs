using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public class MasterEmitter
    {
        private NativeArray<byte> m_StateBuffer;
        private NativeArray<byte> m_StateSubBuffer;

        private byte[] m_MsgBuffer = new byte[0];
        public bool ValidRawStateData => m_StateSubBuffer != default;

        public IMasterNodeSyncState nodeState;

        private UnityEngine.Random.State previousFrameState;

        public MasterEmitter (IMasterNodeSyncState nodeState, uint maxFrameNetworkByteBufferSize, uint maxRpcByteBufferSize)
        {
            m_StateBuffer = new NativeArray<byte>((int)maxFrameNetworkByteBufferSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            this.nodeState = nodeState;
            previousFrameState = UnityEngine.Random.state;

            RPCEmitter.Initialize(maxRpcByteBufferSize);
        }

        public unsafe void PublishCurrentState(ulong currentFrameId)
        {
            using (m_StateSubBuffer)
            {
                var len = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<AdvanceFrame>() + m_StateSubBuffer.Length;

                if (m_MsgBuffer.Length != len)
                    m_MsgBuffer = new byte[len];

                var msg = new AdvanceFrame() {FrameNumber = currentFrameId };
                msg.StoreInBuffer(m_MsgBuffer, Marshal.SizeOf<MessageHeader>()); // Leaver room for header

                Marshal.Copy((IntPtr) m_StateSubBuffer.GetUnsafePtr(), m_MsgBuffer, Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<AdvanceFrame>(), m_StateSubBuffer.Length);

                var msgHdr = new MessageHeader()
                {
                    MessageType = EMessageType.StartFrame,
                    Flags = MessageHeader.EFlag.Broadcast,
                    PayloadSize = (UInt16)m_StateSubBuffer.Length
                };

                nodeState.NetworkAgent.PublishMessage(msgHdr, m_MsgBuffer);
            }
        }


        public void GatherFrameState(ulong frame)
        {
            var endPos = 0;

            bool result = 
                StoreInputState(m_StateBuffer, ref endPos) &&
                StoreTimeState(m_StateBuffer, ref endPos) &&
                StoreClusterInputState(m_StateBuffer, ref endPos) &&
                StoreRndGeneratorState(m_StateBuffer, ref endPos) &&
                StoreRPCs(m_StateBuffer, ref endPos, frame) &&
                MarkStatesEnd(m_StateBuffer, ref endPos);

            if (result)
                m_StateSubBuffer = new NativeArray<byte>(m_StateBuffer.GetSubArray(0, endPos), Allocator.Temp);
            else m_StateSubBuffer = default;
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

        private unsafe bool StoreRndGeneratorState(NativeArray<byte> buffer, ref int endPos)
        {
            if ((endPos + Marshal.SizeOf<int>() + Marshal.SizeOf<UnityEngine.Random.State>()) >= buffer.Length)
            {
                Debug.Assert(false, "destination buffer to small to hold state");
                return false;
            }

            var rndState = previousFrameState;
            previousFrameState = UnityEngine.Random.state;

            int sizePos = endPos;
            endPos += Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.CoreRandomStateID, Marshal.SizeOf<Guid>());

            var rawData = (byte*) &rndState;
            UnsafeUtility.MemCpy((byte*) buffer.GetUnsafePtr() + endPos, rawData, Marshal.SizeOf<UnityEngine.Random.State>());

            int sizeOfRandomState = Marshal.SizeOf<UnityEngine.Random.State>();
            endPos += sizeOfRandomState;
            *((int*)((byte*)buffer.GetUnsafePtr() + sizePos)) = sizeOfRandomState;

            // Debug.Log($"Seed: {UnityEngine.Random.seed}");
            return true;
        }

        private unsafe bool StoreRPCs (NativeArray<byte> buffer, ref int endPos, ulong frame)
        {
            if (RPCEmitter.RPCBufferSize == 0)
                return true;

            *((int*)((byte*)buffer.GetUnsafePtr() + endPos)) = RPCEmitter.RPCBufferSize;
            endPos += Marshal.SizeOf<int>();

            endPos = StoreStateID(buffer, endPos, AdvanceFrame.RPCStateID, Marshal.SizeOf<Guid>());

            return RPCEmitter.Latch(buffer, ref endPos, frame);
        }

        private static unsafe int StoreStateID(NativeArray<byte> buffer, int endPos, Guid id, int guidLen)
        {
            UnsafeUtility.MemCpy((byte*) buffer.GetUnsafePtr() + endPos, (byte*) &id, guidLen);
            endPos += guidLen;
            return endPos;
        }
    }
}
