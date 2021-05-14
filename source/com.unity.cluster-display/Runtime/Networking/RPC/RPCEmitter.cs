using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Text;

namespace Unity.ClusterDisplay
{
    public static class RPCEmitter
    {
        private static NativeArray<byte> rpcBuffer = new NativeArray<byte>(1024, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        private static int rpcBufferSize;
        public static int RPCBufferSize => rpcBufferSize;

        public const int MinimumRPCPayloadSize = sizeof(ushort) * 3;

        public static bool AllowWrites = false;

        public static unsafe bool Latch (NativeArray<byte> buffer, ref int endPos)
        {
            UnsafeUtility.MemCpy((byte*)buffer.GetUnsafePtr() + endPos, rpcBuffer.GetUnsafePtr(), rpcBufferSize);
            UnityEngine.Debug.Log($"Latched RPC Buffer with: {rpcBufferSize} bytes.");
            endPos += rpcBufferSize;
            rpcBufferSize = 0;
            return true;
        }

        public static unsafe bool Unlatch (NativeArray<byte> buffer)
        {
            UnsafeUtility.MemCpy((byte*)rpcBuffer.GetUnsafePtr(), (byte*)buffer.GetUnsafePtr(), buffer.Length);
            rpcBufferSize = buffer.Length;

            int startPos = 0;
            do
            {
                if (startPos >= buffer.Length)
                {
                    UnityEngine.Debug.Log($"Finished parsing RPC buffer of size: {buffer.Length}");
                    break;
                }

                ParsePipeID(ref startPos, out var pipeId);
                ParseRPCId(ref startPos, out var rpcId);
                ParseParametersPayloadSize(ref startPos, out var parametersPayloadSize);
                RPCInterfaceRegistry.TryCall((ushort)(pipeId - 1), rpcId, ref startPos);

            } while (true);

            return true;
        }

        public static unsafe T ParseStructure<T> (ref int startPos)
        {
            UnityEngine.Debug.Log($"Parsing structure of tyep: \"{typeof(T).Name}\" starting at: {startPos}");
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            startPos += Marshal.SizeOf<T>();
            return Marshal.PtrToStructure<T>(ptr);
        }

        private static unsafe void ParsePipeID (ref int startPos, out ushort pipeId)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            pipeId = Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort);
        }

        private static unsafe void ParseRPCId (ref int startPos, out ushort rpcId)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            rpcId = Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort);
        }

        private static unsafe void ParseParametersPayloadSize (ref int startPos, out ushort parametersPayloadSize)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            parametersPayloadSize = Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort);
        }

        public static unsafe void CopyValueToBuffer<T>(T value) where T : struct
        {
            if (!AllowWrites)
                return;

            int structSize = Marshal.SizeOf<T>(value);
            if (rpcBufferSize + structSize >= rpcBuffer.Length)
                rpcBufferSize = 0;

            if (rpcBufferSize + structSize >= rpcBuffer.Length)
                throw new System.Exception("RPC Buffer is full.");

            IntPtr structBufferPtr = Marshal.AllocHGlobal(structSize);
            Marshal.StructureToPtr(value, structBufferPtr, false);

            var rpcBufferPtr = (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize;
            UnsafeUtility.MemCpy(rpcBufferPtr, structBufferPtr.ToPointer(), structSize);

            Marshal.DestroyStructure<T>(structBufferPtr);

            rpcBufferSize += structSize;
        }

        public static void AppendRPCCall (UnityEngine.Object instance, ushort rpcId, int parameterPayloadSize)
        {
            if (!AllowWrites)
                return;

            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                return;

            if (!objectRegistry.TryGetPipeId(instance, out var pipeId))
                return;

            CopyValueToBuffer<ushort>((ushort)(pipeId + 1));
            CopyValueToBuffer<ushort>((ushort)rpcId);
            CopyValueToBuffer<ushort>((ushort)parameterPayloadSize);
        }

        public static void AppendStaticRPCCall (ushort rpcId, int parameterPayloadSize)
        {
            if (!AllowWrites)
                return;

            CopyValueToBuffer<ushort>((ushort)0);
            CopyValueToBuffer<ushort>((ushort)rpcId);
            CopyValueToBuffer<ushort>((ushort)parameterPayloadSize);
        }
    }
}
