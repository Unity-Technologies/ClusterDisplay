using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Text;

namespace Unity.ClusterDisplay
{
    public static class RPCEmitter
    {
        [System.AttributeUsage(System.AttributeTargets.Method)]
        public class RPCCallMarker : Attribute {}

        [System.AttributeUsage(System.AttributeTargets.Method)]
        public class StaticRPCCallMarker : Attribute {}

        [System.AttributeUsage(System.AttributeTargets.Method)]
        public class CopyValueToBufferMarker : Attribute {}

        [System.AttributeUsage(System.AttributeTargets.Method)]
        public class ParseStructureMarker : Attribute {}

        private static NativeArray<byte> rpcBuffer = new NativeArray<byte>(1024 * 16, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        private static int rpcBufferSize;
        public static int RPCBufferSize => rpcBufferSize;

        public const int MinimumRPCPayloadSize = sizeof(ushort) * 3;

        public static bool AllowWrites = false;

        public static void Initialize ()
        {
            rpcBufferSize = 0;
        }

        public static unsafe bool Latch (NativeArray<byte> buffer, ref int endPos)
        {
            UnsafeUtility.MemCpy((byte*)buffer.GetUnsafePtr() + endPos, rpcBuffer.GetUnsafePtr(), rpcBufferSize);
            // UnityEngine.Debug.Log($"Latched RPC Buffer of size: {rpcBufferSize}");
            endPos += rpcBufferSize;
            rpcBufferSize = 0;
            return true;
        }

        public static unsafe bool Unlatch (NativeArray<byte> buffer)
        {
            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                return false;

            if (buffer.Length < sizeof(ushort) * 3)
                return true;

            UnsafeUtility.MemCpy((byte*)rpcBuffer.GetUnsafePtr(), (byte*)buffer.GetUnsafePtr(), buffer.Length);
            rpcBufferSize = buffer.Length;

            ushort bufferPos = 0;
            do
            {
                if (bufferPos > buffer.Length - sizeof(ushort) * 3)
                    break;

                ParseRPCId(ref bufferPos, out var rpcId);
                ParsePipeID(ref bufferPos, out var pipeId);
                ParseParametersPayloadSize(ref bufferPos, out var parametersPayloadSize);
                ushort parametersStartingPos = bufferPos;

                UnityEngine.Debug.Log($"Received RPC: (ID: {rpcId}, Pipe ID: {pipeId - 1}, Parameters Payload Size: {parametersPayloadSize})");

                if (pipeId == 0)
                {
                    // RPCInterfaceRegistry.TryCallStatic(rpcId, parametersPayloadSize, ref bufferPos);
                    continue;
                }

                RPCInterfaceRegistry.TryCallInstance(
                    objectRegistry,
                    rpcId, 
                    (ushort)(pipeId - 1), 
                    parametersPayloadSize, 
                    ref bufferPos);

                if (parametersPayloadSize > 0 && parametersStartingPos == bufferPos)
                {
                    UnityEngine.Debug.LogError("Unable to call RPC instance, check IL Postprocessors.");
                    break;
                }

            } while (true);

            return true;
        }

        [ParseStructureMarker]
        public static unsafe T ParseStructure<T> (ref ushort startPos)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            startPos += (ushort)Marshal.SizeOf<T>();
            return Marshal.PtrToStructure<T>(ptr);
        }

        private static unsafe void ParsePipeID (ref ushort startPos, out ushort pipeId)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            pipeId = Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort);
        }

        private static unsafe void ParseRPCId (ref ushort startPos, out ushort rpcId)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            rpcId = Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort);
        }

        private static unsafe void ParseParametersPayloadSize (ref ushort startPos, out ushort parametersPayloadSize)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            parametersPayloadSize = Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort);
        }

        [CopyValueToBufferMarker]
        public static unsafe void CopyValueToBuffer<T>(T value) where T : struct
        {
            if (!AllowWrites)
                return;

            int structSize = Marshal.SizeOf<T>(value);
            // UnityEngine.Debug.Log($"Current RPC Buffer Size: {rpcBufferSize}, Struct Size: {structSize}, Max RPC Buffer Size: {rpcBuffer.Length}");
            if (rpcBufferSize + structSize >= rpcBuffer.Length)
                throw new System.Exception("RPC Buffer is full.");

            IntPtr structBufferPtr = Marshal.AllocHGlobal(structSize);
            Marshal.StructureToPtr(value, structBufferPtr, false);

            var rpcBufferPtr = (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize;
            UnsafeUtility.MemCpy(rpcBufferPtr, structBufferPtr.ToPointer(), structSize);

            Marshal.DestroyStructure<T>(structBufferPtr);

            rpcBufferSize += structSize;
        }

        [RPCCallMarker]
        public static void AppendRPCCall (UnityEngine.Object instance, ushort rpcId, ushort parametersPayloadSize)
        {
            if (!AllowWrites)
                return;

            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                return;

            if (!objectRegistry.TryGetPipeId(instance, out var pipeId))
                return;

            CopyValueToBuffer<ushort>((ushort)rpcId);
            CopyValueToBuffer<ushort>((ushort)(pipeId + 1));
            CopyValueToBuffer<ushort>((ushort)parametersPayloadSize);

            UnityEngine.Debug.Log($"Sending RPC: (ID: {rpcId}, Pipe ID: {pipeId}, Parameters Payload Size: {parametersPayloadSize})");
        }

        [StaticRPCCallMarker]
        public static void AppendStaticRPCCall (ushort rpcId, ushort parametersPayloadSize)
        {
            if (!AllowWrites)
                return;

            CopyValueToBuffer<ushort>((ushort)rpcId);
            CopyValueToBuffer<ushort>((ushort)0);
            CopyValueToBuffer<ushort>((ushort)parametersPayloadSize);
        }
    }
}
