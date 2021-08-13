using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using buint = System.UInt32;

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
        public class AppendRPCValueTypeParameterValueMarker : Attribute {}

        public static unsafe bool Latch (NativeArray<byte> buffer, ref buint endPos)
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
            if (rpcBufferSize + sizeof(buint) + strSize >= rpcBuffer.Length)
                throw new System.Exception("RPC Buffer is full.");

            CopyCountToRPCBuffer((buint)strSize);
            fixed (char* ptr = value)
                Encoding.ASCII.GetBytes(
                    ptr,
                    strSize,
                    (byte *)rpcBuffer.GetUnsafePtr() + rpcBufferSize,
                    strSize);

            rpcBufferSize += (buint)strSize;
        }

        private static bool CanWriteValueTypeToRPCBuffer<T> (T value, out buint structSize)
        {
            if (!AllowWrites)
            {
                structSize = 0;
                return false;
            }

            structSize = (buint)Marshal.SizeOf<T>(value);
            if (rpcBufferSize + structSize >= rpcBuffer.Length)
            {
                UnityEngine.Debug.LogError("RPC Buffer is full.");
                return false;
            }

            return true;
        }

        private static bool CanWriteBufferToRPCBuffer<T> (int count, out buint arrayByteCount) where T : unmanaged
        {
            if (!AllowWrites)
            {
                arrayByteCount = 0;
                return false;
            }

            arrayByteCount = (buint)(Marshal.SizeOf<T>() * count);

            if (arrayByteCount > m_CachedPayloadLimits.maxSingleRPCParameterByteSize)
            {
                UnityEngine.Debug.LogError($"Unable to write parameter buffer of size: {arrayByteCount} to RPC buffer, the max byte size of buffer of RPC parameter is: {m_CachedPayloadLimits.maxSingleRPCParameterByteSize} bytes.");
                return false;
            }

            if (rpcBufferSize + sizeof(buint) + arrayByteCount >= rpcBuffer.Length)
            {
                UnityEngine.Debug.LogError($"Unable to write parameter buffer of size: {arrayByteCount} to RPC Buffer because it's full.");
                return false;
            }

            return true;
        }

        private static unsafe void CopyCountToRPCBuffer (buint count)
        {
            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                UnsafeUtility.AddressOf(ref count), 
                sizeof(buint));

            rpcBufferSize += sizeof(buint);
        }

        private static unsafe void CopyBufferToRPCBuffer<T>(T* ptr, buint arrayByteCount) where T : unmanaged
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

            CopyCountToRPCBuffer((buint)buffer.Length);
            CopyBufferToRPCBuffer<T>((T*)buffer.GetUnsafePtr(), arrayByteCount);
        }

        [AppendRPCArrayParameterValueMarker]
        public static unsafe void AppendRPCArrayParameterValues<T>(T[] value) where T : unmanaged
        {
            if (value == null)
                return;

            if (!CanWriteBufferToRPCBuffer<T>(value.Length, out var arrayByteCount))
                return;

            CopyCountToRPCBuffer((buint)value.Length);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ShiftRPCExecutionStage (ushort rpcExecutionStage)
        {
            rpcExecutionStage = (ushort)(rpcExecutionStage > 0 ? rpcExecutionStage : (ushort)RPCExecutor.CurrentExecutionStage + 1);
            if (rpcExecutionStage >= (ushort)RPCExecutionStage.AfterLateUpdate)
                rpcExecutionStage = (ushort)RPCExecutionStage.AfterLateUpdate;
            return rpcExecutionStage;
        }

        [RPCCallMarker]
        public static void AppendRPCCall (
            UnityEngine.Component instance, 
            ushort rpcId, 
            ushort rpcExecutionStage, 
            // int explicitRPCExeuctionStage, // 1 == Explicit RPC Exeuction | 0 == Implicit RPC Execution.
            buint parametersPayloadSize)
        {
            if (!AllowWrites)
                return;
            if (instance.GetType().Name == "TransformWrapper")
                UnityEngine.Debug.Log("TEST");

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

            buint totalRPCCallSize = MinimumRPCPayloadSize + (buint)parametersPayloadSize;
            if (totalRPCCallSize > m_CachedPayloadLimits.maxSingleRPCByteSize)
            {
                UnityEngine.Debug.LogError($"Unable to append RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} is greater then the max RPC call size of: {m_CachedPayloadLimits.maxSingleRPCByteSize}");
                return;
            }

            if (totalRPCCallSize >= rpcBuffer.Length)
            {
                UnityEngine.Debug.LogError($"Unable to append RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} does not fit in the RPC buffer of size: {rpcBuffer.Length}");
                return;
            }

            buint startingBufferPos = rpcBufferSize;
            rpcExecutionStage = ShiftRPCExecutionStage(rpcExecutionStage);

            AppendRPCValueTypeParameterValue<ushort>((ushort)rpcId);
            AppendRPCValueTypeParameterValue<ushort>((ushort)(rpcExecutionStage));
            AppendRPCValueTypeParameterValue<ushort>((ushort)(pipeId + 1));
            AppendRPCValueTypeParameterValue<buint>((buint)parametersPayloadSize);

            #if CLUSTER_DISPLAY_VERBOSE_LOGGING
            UnityEngine.Debug.Log($"Sending RPC: (ID: {rpcId}, RPC ExecutionStage: {rpcExecutionStage}, Pipe ID: {pipeId}, Parameters Payload Byte Count: {parametersPayloadSize}, Total RPC Byte Count: {totalRPCCallSize}, Starting Buffer Position: {startingBufferPos})");
            #endif
        }

        [StaticRPCCallMarker]
        public static void AppendStaticRPCCall (
            ushort rpcId, 
            ushort rpcExecutionStage, 
            // int explicitRPCExeuctionStage, // 1 == Explicit RPC Exeuction | 0 == Implicit RPC Execution.
            buint parametersPayloadSize)
        {
            if (!AllowWrites)
                return;

            buint totalRPCCallSize = MinimumRPCPayloadSize + (buint)parametersPayloadSize;
            if (totalRPCCallSize > m_CachedPayloadLimits.maxSingleRPCByteSize)
            {
                UnityEngine.Debug.LogError($"Unable to append static RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} is greater then the max RPC call size of: {m_CachedPayloadLimits.maxSingleRPCByteSize}");
                return;
            }

            if (totalRPCCallSize >= rpcBuffer.Length)
            {
                UnityEngine.Debug.LogError($"Unable to append static RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} does not fit in the RPC buffer of size: {rpcBuffer.Length}");
                return;
            }

            buint startingBufferPos = rpcBufferSize;
            rpcExecutionStage = ShiftRPCExecutionStage(rpcExecutionStage);

            AppendRPCValueTypeParameterValue<ushort>((ushort)rpcId);
            AppendRPCValueTypeParameterValue<ushort>((ushort)(rpcExecutionStage));
            AppendRPCValueTypeParameterValue<ushort>((ushort)0);
            AppendRPCValueTypeParameterValue<buint>((buint)parametersPayloadSize);

            #if CLUSTER_DISPLAY_VERBOSE_LOGGING
            UnityEngine.Debug.Log($"Sending static RPC: (ID: {rpcId}, RPC Execution Stage: {rpcExecutionStage}, Parameters Payload Byte Count: {parametersPayloadSize}, Total RPC Byte Count: {totalRPCCallSize}, Starting Buffer Position: {startingBufferPos})");
            #endif
        }
    }
}
