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

        public static unsafe void CopyValueToBuffer<T>(T value) where T : struct
        {
            int structSize = Marshal.SizeOf<T>(value);
            if (rpcBufferSize + structSize >= rpcBuffer.Length)
                rpcBufferSize = 0;

            // UnityEngine.Debug.Log($"Struct Size: {structSize}");
            if (rpcBufferSize + structSize >= rpcBuffer.Length)
                throw new System.Exception("RPC Buffer is full.");

            IntPtr structBufferPtr = Marshal.AllocHGlobal(structSize);
            Marshal.StructureToPtr(value, structBufferPtr, false);

            /*
            var structBytes = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(structBufferPtr.ToPointer(), structSize, Allocator.Persistent);
            var structBytesPtr = structBytes.GetUnsafeReadOnlyPtr();
            */
            var rpcBufferPtr = (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize;
            UnsafeUtility.MemCpy(rpcBufferPtr, structBufferPtr.ToPointer(), structSize);

            rpcBufferSize += structSize;
        }

        public static void AppendRPCCall (UnityEngine.Object instance, ushort rpcId)
        {
            // UnityEngine.Debug.Log("OpenRPCLatch");
            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                return;

            if (!objectRegistry.TryGetPipeId(instance, out var pipeId))
                return;

            CopyValueToBuffer(pipeId + 1);
            CopyValueToBuffer(rpcId);
            // CopyValueToBuffer(parameterPayloadSize);
        }

        public static void AppendStaticRPCCall (ushort rpcId)
        {
            // UnityEngine.Debug.Log("OpenStaticRPCLatch");
            CopyValueToBuffer((ushort)0);
            CopyValueToBuffer(rpcId);
            // CopyValueToBuffer(parameterPayloadSize);
        }
    }
}
