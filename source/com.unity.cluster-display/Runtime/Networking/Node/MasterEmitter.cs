﻿using System;
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
        private NativeArray<byte> m_RawStateData;
        private byte[] m_MsgBuffer = new byte[0];
        public bool ValidRawStateData => m_RawStateData != default;

        public IMasterNodeSyncState nodeState;

        public MasterEmitter (IMasterNodeSyncState nodeState)
        {
            this.nodeState = nodeState;
        }

        public unsafe void PublishCurrentState(ulong currentFrameId)
        {
            using (m_RawStateData)
            {
                var len = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<AdvanceFrame>() + m_RawStateData.Length;
                if (m_MsgBuffer.Length != len)
                {
                    m_MsgBuffer = new byte[len];
                }

                var msg = new AdvanceFrame() {FrameNumber = currentFrameId };
                msg.StoreInBuffer(m_MsgBuffer, Marshal.SizeOf<MessageHeader>()); // Leaver room for header

                Marshal.Copy((IntPtr) m_RawStateData.GetUnsafePtr(), m_MsgBuffer, Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<AdvanceFrame>(), m_RawStateData.Length);

                var msgHdr = new MessageHeader()
                {
                    MessageType = EMessageType.StartFrame,
                    Flags = MessageHeader.EFlag.Broadcast,
                    PayloadSize = (UInt16)m_RawStateData.Length
                };

                nodeState.NetworkAgent.PublishMessage(msgHdr, m_MsgBuffer);
            }
        }


        public void GatherFrameState()
        {
            var endPos = 0;
            using (var buffer = new NativeArray<byte>(16 * 1024, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                if (
                    StoreInputState(buffer, ref endPos) &&
                    StoreTimeState(buffer, ref endPos) &&
                    StoreClusterInputState(buffer, ref endPos) &&
                    StoreRndGeneratorState(buffer, ref endPos) &&
                    StoreRPCs(buffer, ref endPos) &&
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

        private unsafe bool StoreRPCs (NativeArray<byte> buffer, ref int endPos)
        {
            if (RPCEmitter.RPCBufferSize == 0)
                return true;

            *((int*)((byte*)buffer.GetUnsafePtr() + endPos)) = RPCEmitter.RPCBufferSize;
            endPos += Marshal.SizeOf<int>();

            Debug.Log($"RPC Buffer Size: {RPCEmitter.RPCBufferSize}");
            endPos = StoreStateID(buffer, endPos, AdvanceFrame.RPCStateID, Marshal.SizeOf<Guid>());

            return RPCEmitter.Latch(buffer, ref endPos);
        }

        private static unsafe int StoreStateID(NativeArray<byte> buffer, int endPos, Guid id, int guidLen)
        {
            UnsafeUtility.MemCpy((byte*) buffer.GetUnsafePtr() + endPos, (byte*) &id, guidLen);
            endPos += guidLen;
            return endPos;
        }
    }
}
