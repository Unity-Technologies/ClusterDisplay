using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

using buint = System.UInt32;

namespace Unity.ClusterDisplay
{
    public class MasterEmitter
    {
        private NativeArray<byte> m_PreviousStateSubBuffer;
        private NativeArray<byte> m_CurrentStateBuffer;

        private buint m_CurrentStateBufferEndPos = 0;
        private bool m_CurrentStateResult = false;

        private byte[] m_MsgBuffer = new byte[0];
        public bool ValidRawStateData => m_PreviousStateSubBuffer != default;

        public IMasterNodeSyncState nodeState;

        public MasterEmitter (IMasterNodeSyncState nodeState, ClusterDisplayResources.PayloadLimits payloadLimits)
        {
            m_CurrentStateBuffer = new NativeArray<byte>((int)payloadLimits.maxFrameNetworkByteBufferSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            this.nodeState = nodeState;

            RPC.RPCBufferIO.Initialize(payloadLimits);
        }

        public unsafe void PublishCurrentState(ulong currentFrameId)
        {
            using (m_PreviousStateSubBuffer)
            {
                var len = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<AdvanceFrame>() + m_PreviousStateSubBuffer.Length;

                if (m_MsgBuffer.Length != len)
                    m_MsgBuffer = new byte[len];

                var msg = new AdvanceFrame() {FrameNumber = currentFrameId };
                msg.StoreInBuffer(m_MsgBuffer, Marshal.SizeOf<MessageHeader>()); // Leaver room for header

                Marshal.Copy((IntPtr) m_PreviousStateSubBuffer.GetUnsafePtr(), m_MsgBuffer, Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<AdvanceFrame>(), m_PreviousStateSubBuffer.Length);

                var msgHdr = new MessageHeader()
                {
                    MessageType = EMessageType.StartFrame,
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
                if (StoreRPCs(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos) && MarkStatesEnd(m_CurrentStateBuffer, ref m_CurrentStateBufferEndPos))
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
            
            buint sizePos = endPos;
            endPos += (buint)Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.CoreInputStateID, guidLen);

            buint bytesWritten = (buint)ClusterSerialization.SaveInputManagerState(buffer.GetSubArray((int)endPos, (int)(buffer.Length - endPos)));
            Debug.Assert(bytesWritten >= 0, "Buffer to small! Input not stored.");
            if (bytesWritten < 0)
                return false;

            endPos += bytesWritten;

            *((buint*)((byte*)buffer.GetUnsafePtr() + sizePos)) = bytesWritten;
            return true;
        }

        private static unsafe bool StoreTimeState(NativeArray<byte> buffer, ref buint endPos)
        {
            var guidLen = Marshal.SizeOf<Guid>();
            
            buint sizePos = endPos;
            endPos += (buint)Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.CoreTimeStateID, guidLen);

            buint bytesWritten = (buint)ClusterSerialization.SaveTimeManagerState(buffer.GetSubArray((int)endPos, (int)(buffer.Length - endPos)));
            Debug.Assert(bytesWritten >= 0, "Buffer to small! Time state not stored.");
            if (bytesWritten < 0)
                return false;

            endPos += bytesWritten;

            *((buint*)((byte*)buffer.GetUnsafePtr() + sizePos)) = bytesWritten;
            return true;
        }
        
        private static unsafe bool StoreClusterInputState(NativeArray<byte> buffer, ref buint endPos)
        {
            var guidLen = Marshal.SizeOf<Guid>();
            
            buint startingPos = endPos;
            endPos += (buint)Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.ClusterInputStateID, guidLen);

            var bytesWritten = (buint)ClusterSerialization.SaveClusterInputState(buffer.GetSubArray((int)endPos, (int)(buffer.Length - endPos)));
            Debug.Assert(bytesWritten >= 0, "Buffer to small. ClusterInput not stored.");
            if (bytesWritten < 0)
                return false;

            if (bytesWritten > 0)
            {
                endPos += bytesWritten;
                *((buint*) ((byte*) buffer.GetUnsafePtr() + startingPos)) = bytesWritten;
            }
            else
                endPos = startingPos;

            return true;
        }

        private static unsafe bool MarkStatesEnd(NativeArray<byte> buffer, ref buint endPos)
        {
            Debug.Assert(endPos < buffer.Length, "Buffer to small to store end marker");
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
                Debug.Assert(false, "destination buffer to small to hold state");
                return false;
            }

            var rndState = UnityEngine.Random.state;

            buint sizePos = endPos;
            endPos += (buint)Marshal.SizeOf<int>();
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.CoreRandomStateID, Marshal.SizeOf<Guid>());

            var rawData = (byte*) &rndState;
            UnsafeUtility.MemCpy((byte*) buffer.GetUnsafePtr() + endPos, rawData, Marshal.SizeOf<UnityEngine.Random.State>());

            buint sizeOfRandomState = (buint)Marshal.SizeOf<UnityEngine.Random.State>();
            endPos += sizeOfRandomState;
            *((buint*)((byte*)buffer.GetUnsafePtr() + sizePos)) = sizeOfRandomState;

            // Debug.Log($"Seed: {UnityEngine.Random.seed}");
            return true;
        }

        private unsafe bool StoreRPCs (NativeArray<byte> buffer, ref buint endPos)
        {
            if (RPC.RPCBufferIO.RPCBufferSize == 0)
                return true;

            *((buint*)((byte*)buffer.GetUnsafePtr() + endPos)) = RPC.RPCBufferIO.RPCBufferSize;
            endPos += (buint)Marshal.SizeOf<int>();

            endPos = StoreStateID(buffer, endPos, AdvanceFrame.RPCStateID, Marshal.SizeOf<Guid>());

            return RPC.RPCBufferIO.Latch(buffer, ref endPos);
        }

        private static unsafe buint StoreStateID(NativeArray<byte> buffer, buint endPos, Guid id, int guidLen)
        {
            UnsafeUtility.MemCpy((byte*) buffer.GetUnsafePtr() + endPos, (byte*) &id, guidLen);
            endPos += (buint)guidLen;
            return endPos;
        }
    }
}
