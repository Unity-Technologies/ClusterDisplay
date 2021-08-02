using System;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.ClusterDisplay.RPC
{
    public static partial class RPCBufferIO
    {
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class RPCCallMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class StaticRPCCallMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class AppendRPCStringParameterValueMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class AppendRPCArrayParameterValueMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class AppendRPCNativeArrayParameterValueMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class AppendRPCNativeSliceParameterValueMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class AppendRPCValueTypeParameterValueMarker : Attribute {}

        public static unsafe bool Latch (NativeArray<byte> buffer, ref int endPos)
        {
            UnsafeUtility.MemCpy((byte*)buffer.GetUnsafePtr() + endPos, rpcBuffer.GetUnsafePtr(), rpcBufferSize);
            endPos += rpcBufferSize;
            rpcBufferSize = 0;
            return true;
        }

        [AppendRPCStringParameterValueMarker]
        public static unsafe void AppendRPCStringParameterValue(string value)
        {
            if (!AllowWrites)
                return;

            int strSize = value.Length;
            if (strSize > ushort.MaxValue)
                throw new System.Exception($"Max string size is: {ushort.MaxValue} characters.");

            if (rpcBufferSize + sizeof(ushort) + strSize >= rpcBuffer.Length)
                throw new System.Exception("RPC Buffer is full.");

            CopyCountToRPCBuffer((ushort)strSize);
            fixed (char* ptr = value)
                Encoding.ASCII.GetBytes(
                    ptr,
                    strSize,
                    (byte *)rpcBuffer.GetUnsafePtr() + rpcBufferSize,
                    strSize);

            rpcBufferSize += strSize;
        }

        private static bool CanWriteValueTypeToRPCBuffer<T> (T value, out int structSize)
        {
            if (!AllowWrites)
            {
                structSize = 0;
                return false;
            }

            structSize = Marshal.SizeOf<T>(value);
            if (rpcBufferSize + structSize >= rpcBuffer.Length)
            {
                UnityEngine.Debug.LogError("RPC Buffer is full.");
                return false;
            }

            return true;
        }

        private static bool CanWriteBufferToRPCBuffer<T> (int count, out int arrayByteCount) where T : unmanaged
        {
            if (!AllowWrites)
            {
                arrayByteCount = 0;
                return false;
            }

            arrayByteCount = Marshal.SizeOf<T>() * count;

            if (arrayByteCount > ushort.MaxValue)
            {
                UnityEngine.Debug.LogError($"Max string array is: {ushort.MaxValue} characters.");
                return false;
            }

            if (rpcBufferSize + sizeof(ushort) + arrayByteCount >= rpcBuffer.Length)
            {
                UnityEngine.Debug.LogError("RPC Buffer is full.");
                return false;
            }

            return true;
        }

        private static unsafe void CopyCountToRPCBuffer (ushort count)
        {
            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                UnsafeUtility.AddressOf(ref count), 
                sizeof(ushort));

            rpcBufferSize += sizeof(ushort);
        }

        private static unsafe void CopyBufferToRPCBuffer<T>(T* ptr, int arrayByteCount) where T : unmanaged
        {
            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                ptr, 
                arrayByteCount);

            rpcBufferSize += arrayByteCount;
        }

        [AppendRPCNativeArrayParameterValueMarker]
        public static unsafe void AppendRPCNativeArrayParameterValues<T> (NativeArray<T> buffer) where T : unmanaged
        {
            if (!CanWriteBufferToRPCBuffer<T>(buffer.Length, out var arrayByteCount))
                return;

            CopyCountToRPCBuffer((ushort)buffer.Length);
            CopyBufferToRPCBuffer<T>((T*)buffer.GetUnsafePtr(), arrayByteCount);
        }

        [AppendRPCNativeSliceParameterValueMarker]
        public static unsafe void AppendRPCNativeSliceParameterValues<T> (NativeSlice<T> buffer) where T : unmanaged
        {
            if (!CanWriteBufferToRPCBuffer<T>(buffer.Length, out var arrayByteCount))
                return;

            CopyCountToRPCBuffer((ushort)buffer.Length);
            CopyBufferToRPCBuffer<T>((T*)buffer.GetUnsafePtr(), arrayByteCount);
        }

        [AppendRPCArrayParameterValueMarker]
        public static unsafe void AppendRPCArrayParameterValues<T>(T[] value) where T : unmanaged
        {
            if (value == null)
                return;

            if (!CanWriteBufferToRPCBuffer<T>(value.Length, out var arrayByteCount))
                return;

            CopyCountToRPCBuffer((ushort)value.Length);

            fixed (T* ptr = &value[0])
                CopyBufferToRPCBuffer<T>(ptr, arrayByteCount);
        }

        [AppendRPCValueTypeParameterValueMarker]
        public static unsafe void AppendRPCValueTypeParameterValue<T>(T value) where T : struct
        {
            if (!CanWriteValueTypeToRPCBuffer(value, out var structSize))
                return;

            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                UnsafeUtility.AddressOf(ref value), 
                structSize);

            rpcBufferSize += structSize;
        }

        [RPCCallMarker]
        public static void AppendRPCCall (
            UnityEngine.Component instance, 
            int rpcId, 
            int rpcExecutionStage, 
            // int explicitRPCExeuctionStage, // 1 == Explicit RPC Exeuction | 0 == Implicit RPC Execution.
            int parametersPayloadSize)
        {
            if (!AllowWrites)
                return;

            if (!SceneObjectsRegistry.TryGetPipeId(instance, out var pipeId))
            {
                UnityEngine.Debug.LogError($"Unable to append RPC call with ID: {rpcId}, the calling instance has not been registered with the {nameof(SceneObjectsRegistry)}.");
                return;
            }

            /*
            var rpcConfig = SceneObjectsRegistry.GetRPCConfig(pipeId, (ushort)rpcId);
            if (!rpcConfig.enabled)
                return;
            */

            int totalRPCCallSize = MinimumRPCPayloadSize + parametersPayloadSize;
            if (totalRPCCallSize > ushort.MaxValue)
            {
                UnityEngine.Debug.LogError($"Unable to append RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} is greater then the max RPC call size of: {ushort.MaxValue}");
                return;
            }

            if (totalRPCCallSize >= rpcBuffer.Length)
            {
                UnityEngine.Debug.LogError($"Unable to append RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} does not fit in the RPC buffer of size: {rpcBuffer.Length}");
                return;
            }

            int startingBufferPos = rpcBufferSize;
            rpcExecutionStage = (/*explicitRPCExeuctionStage == 0 && */rpcExecutionStage > 0) ? rpcExecutionStage : ((int)RPCExecutor.CurrentExecutionStage + 1);

            AppendRPCValueTypeParameterValue<ushort>((ushort)rpcId);
            AppendRPCValueTypeParameterValue<ushort>((ushort)(rpcExecutionStage));
            AppendRPCValueTypeParameterValue<ushort>((ushort)(pipeId + 1));
            AppendRPCValueTypeParameterValue<ushort>((ushort)parametersPayloadSize);

            #if CLUSTER_DISPLAY_VERBOSE_LOGGING
            UnityEngine.Debug.Log($"Sending RPC: (ID: {rpcId}, RPC ExecutionStage: {rpcExecutionStage}, Pipe ID: {pipeId}, Parameters Payload Byte Count: {parametersPayloadSize}, Total RPC Byte Count: {totalRPCCallSize}, Starting Buffer Position: {startingBufferPos})");
            #endif
        }

        [StaticRPCCallMarker]
        public static void AppendStaticRPCCall (
            int rpcId, 
            int rpcExecutionStage, 
            // int explicitRPCExeuctionStage, // 1 == Explicit RPC Exeuction | 0 == Implicit RPC Execution.
            int parametersPayloadSize)
        {
            if (!AllowWrites)
                return;

            int totalRPCCallSize = MinimumRPCPayloadSize + parametersPayloadSize;
            if (totalRPCCallSize > ushort.MaxValue)
            {
                UnityEngine.Debug.LogError($"Unable to append static RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} is greater then the max RPC call size of: {ushort.MaxValue}");
                return;
            }

            if (totalRPCCallSize >= rpcBuffer.Length)
            {
                UnityEngine.Debug.LogError($"Unable to append static RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} does not fit in the RPC buffer of size: {rpcBuffer.Length}");
                return;
            }

            int startingBufferPos = rpcBufferSize;
            rpcExecutionStage = (/*explicitRPCExeuctionStage == 0 && */rpcExecutionStage > 0) ? rpcExecutionStage : ((int)RPCExecutor.CurrentExecutionStage + 1);

            AppendRPCValueTypeParameterValue<ushort>((ushort)rpcId);
            AppendRPCValueTypeParameterValue<ushort>((ushort)(rpcExecutionStage));
            AppendRPCValueTypeParameterValue<ushort>((ushort)0);
            AppendRPCValueTypeParameterValue<ushort>((ushort)parametersPayloadSize);

            #if CLUSTER_DISPLAY_VERBOSE_LOGGING
            UnityEngine.Debug.Log($"Sending static RPC: (ID: {rpcId}, RPC Execution Stage: {rpcExecutionStage}, Parameters Payload Byte Count: {parametersPayloadSize}, Total RPC Byte Count: {totalRPCCallSize}, Starting Buffer Position: {startingBufferPos})");
            #endif
        }
    }
}
