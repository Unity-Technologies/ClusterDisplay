using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using buint = System.UInt32;

namespace Unity.ClusterDisplay.RPC
{
    public static partial class RPCBufferIO
    {
        /// <summary>
        /// This is used by the ILPostProcessor to find and perform the call.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        internal class RPCCallMarker : Attribute {}

        /// <summary>
        /// This is used by the ILPostProcessor to find and perform the call.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        internal class StaticRPCCallMarker : Attribute {}

        /// <summary>
        /// This is used by the ILPostProcessor to find and perform the call.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        internal class AppendRPCStringParameterValueMarker : Attribute {}

        /// <summary>
        /// This is used by the ILPostProcessor to find and perform the call.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        internal class AppendRPCArrayParameterValueMarker : Attribute {}

        /// <summary>
        /// This is used by the ILPostProcessor to find and perform the call.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        internal class AppendRPCNativeArrayParameterValueMarker : Attribute {}

        /// <summary>
        /// This is used by the ILPostProcessor to find and perform the call.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        internal class AppendRPCValueTypeParameterValueMarker : Attribute {}
        
        /// <summary>
        /// This is used by the ILPostProcessor to find and perform the call.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        internal class AppendRPCCharParameterValueMarker : Attribute {}

        /// <summary>
        /// Were done writing to the RPC buffer, so latching essentialy copies the RPC buffer to our network frame buffer.
        /// </summary>
        /// <param name="buffer">The network frame buffer that contains more then just RPC data.</param>
        /// <param name="endPos">Where we want to start writing into the network frame buffer.</param>
        /// <returns></returns>
        internal static unsafe int Latch (NativeArray<byte> buffer)
        {
            UnsafeUtility.MemCpy((byte*)buffer.GetUnsafePtr(), rpcBuffer.GetUnsafePtr(), rpcBufferSize);
            int bufferLength = (int)rpcBufferSize;
            return bufferLength;
        }


        /// <summary>
        /// Converts a string to bytes and then writes the string size and bytes to the RPC buffer.
        /// </summary>
        /// <param name="value">The string we want to write to the RPC buffer</param>
        [AppendRPCStringParameterValueMarker]
        public static unsafe void AppendRPCStringParameterValue(string value)
        {
            int strSize = value.Length;
            if (rpcBufferSize + Marshal.SizeOf<buint>() + strSize >= rpcBuffer.Length)
                throw new System.Exception("RPC Buffer is full.");

            CopyCountToRPCBuffer((buint)strSize);
            var byteCount = (buint)strSize * 2 /* UTF-16 2 Bytes */;
            ClusterDebug.Log($"Byte Count:\"{Encoding.Unicode.GetBytes(value).Length}\".");
            fixed (char* ptr = value)
                Encoding.Unicode.GetBytes(
                    ptr,
                    strSize,
                    (byte *)rpcBuffer.GetUnsafePtr() + rpcBufferSize,
                    (int)byteCount);

            rpcBufferSize += byteCount;
        }

        /// <summary>
        /// Verify that we can fit the value type into the RPC buffer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="structSize"></param>
        /// <returns></returns>
        private static bool CanWriteValueTypeToRPCBuffer<T> (T value, out buint structSize)
        {
            structSize = (buint)Marshal.SizeOf<T>(value);
            if (rpcBufferSize + structSize >= rpcBuffer.Length)
            {
                ClusterDebug.LogError("RPC Buffer is full.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Will the array of value type fit in the RPC buffer?
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="count">Length of the array.</param>
        /// <param name="arrayByteCount">The output total byte count of the value type array.</param>
        /// <returns></returns>
        private static bool CanWriteBufferToRPCBuffer<T> (int count, out buint arrayByteCount) where T : unmanaged
        {
            arrayByteCount = (buint)(Marshal.SizeOf<T>() * count);

            if (arrayByteCount > k_MaxSingleRPCParameterByteSize)
            {
                ClusterDebug.LogError($"Unable to write parameter buffer of size: {arrayByteCount} to RPC buffer, the max byte size of buffer of RPC parameter is: {k_MaxSingleRPCParameterByteSize} bytes.");
                return false;
            }

            if (rpcBufferSize + Marshal.SizeOf<buint>() + arrayByteCount >= rpcBuffer.Length)
            {
                ClusterDebug.LogError($"Unable to write parameter buffer of size: {arrayByteCount} to RPC Buffer because it's full.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Store some buffer length to the RPC Buffer.
        /// </summary>
        /// <param name="count">The length of the array of what we are gonna store next in the RPC buffer.</param>
        private static unsafe void CopyCountToRPCBuffer (buint count)
        {
            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                UnsafeUtility.AddressOf(ref count), 
                Marshal.SizeOf<buint>());

            rpcBufferSize += (buint)Marshal.SizeOf<buint>();
        }

        /// <summary>
        /// Copy some valuetype buffer, which could be a NativeArray<T> or managed T[].
        /// </summary>
        /// <typeparam name="T">Buffer element value type.</typeparam>
        /// <param name="ptr">The fixed pointer to the first item in the buffer.</param>
        /// <param name="arrayByteCount">The total byte count of the buffer that were copying.</param>
        private static unsafe void CopyBufferToRPCBuffer<T>(T* ptr, buint arrayByteCount) where T : unmanaged
        {
            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                ptr, 
                arrayByteCount);

            rpcBufferSize += arrayByteCount;
        }

        /// <summary>
        /// Used to append a NativeArray<T> argument to the RPC buffer.
        /// </summary>
        /// <typeparam name="T">The element type of our NativeArray</typeparam>
        /// <param name="buffer"></param>
        [AppendRPCNativeArrayParameterValueMarker]
        public static unsafe void AppendRPCNativeArrayParameterValues<T> (NativeArray<T> buffer) where T : unmanaged
        {
            if (!CanWriteBufferToRPCBuffer<T>(buffer.Length, out var arrayByteCount))
                return;

            CopyCountToRPCBuffer((buint)buffer.Length);
            CopyBufferToRPCBuffer<T>((T*)buffer.GetUnsafePtr(), arrayByteCount);
        }

        /// <summary>
        /// Append an RPC managed array argument of type T to the RPC buffer.
        /// </summary>
        /// <typeparam name="T">The element type of the array.</typeparam>
        /// <param name="value"></param>
        [AppendRPCArrayParameterValueMarker]
        public static unsafe void AppendRPCArrayParameterValues<T>(T[] value) where T : unmanaged
        {
            if (value == null || !CanWriteBufferToRPCBuffer<T>(value.Length, out var arrayByteCount))
                return;

            CopyCountToRPCBuffer((buint)value.Length);

            // Get a fixed pointer to the first element in the array.
            fixed (T* ptr = &value[0])
                CopyBufferToRPCBuffer<T>(ptr, arrayByteCount);
        }
        
        /// <summary>
        /// Simply append a UTF-16 char RPC argument to the RPC buffer.
        /// </summary>
        /// <param name="value">The UTF-16 you want to append.</param>
        [AppendRPCCharParameterValueMarker]
        public static unsafe void AppendRPCCharParameterValue(char value)
        {
            // In this case we do not use Marshal.SizeOf with charsince Marshal assumes you use the char
            // in interop unmanaged code which it'll interpret as 1 byte. We need 2 bytes for unicode.
            buint charSize = sizeof(char);
            if (rpcBufferSize + charSize >= rpcBuffer.Length)
                return;

            // Perform the copy.
            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                UnsafeUtility.AddressOf(ref value), 
                charSize);
            
            rpcBufferSize += charSize;
        }

        /// <summary>
        /// Simply append a struct RPC argument to the RPC buffer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        [AppendRPCValueTypeParameterValueMarker]
        public static unsafe void AppendRPCValueTypeParameterValue<T>(T value) where T : struct
        {
            if (!CanWriteValueTypeToRPCBuffer(value, out var structSize))
                return;

            // Perform the copy.
            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                UnsafeUtility.AddressOf(ref value), 
                structSize);

            rpcBufferSize += structSize;
        }

        /// <summary>
        /// If RPCExecutionStage is Automatic, then we get the current stage were in throughout 
        /// the frame and add one to it to get the following stage.
        /// </summary>
        /// <param name="rpcExecutionStage"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ShiftRPCExecutionStage (ushort rpcExecutionStage)
        {
            rpcExecutionStage = (ushort)(rpcExecutionStage > 0 ? rpcExecutionStage : (ushort)RPCExecutor.CurrentExecutionStage);

            if (rpcExecutionStage > (ushort)RPCExecutionStage.AfterLateUpdate)
            {
                rpcExecutionStage = (ushort)RPCExecutionStage.AfterLateUpdate;
            }

            else if (
                rpcExecutionStage != (ushort)RPCExecutionStage.ImmediatelyOnArrival &&
                rpcExecutionStage < (ushort)RPCExecutionStage.ImmediatelyOnArrival)
            {
                rpcExecutionStage = (ushort)RPCExecutionStage.BeforeFixedUpdate;
            }

            return rpcExecutionStage;
        }

        /// <summary>
        /// This appends the RPC header to the RPC buffer and its usually called from injected IL.
        /// </summary>
        /// <param name="instance">The instance that implements the RPC.</param>
        /// <param name="rpcId">The RPC ID which is unique to the method type.</param>
        /// <param name="rpcExecutionStage">When this RPC was executed.</param>
        /// <param name="parametersPayloadSize">The total byte count of the method's arguments.</param>
        [RPCCallMarker]
        public static void AppendRPCCall (
            UnityEngine.Component instance, 
            string rpcHash, 
            ushort rpcExecutionStage, 
            buint parametersPayloadSize)
        {
            if (!RPCRegistry.RPCHashToRPCId(rpcHash, out var rpcId))
                return;

            // Get the pipe ID, which is the ID of the instance, and this ID should match the equivalant instance on a repeater node.
            if (!SceneObjectsRegistry.TryGetPipeId(instance, out var pipeId))
            {
                ClusterDebug.LogError($"Unable to append RPC call with ID: {rpcId}, the calling instance has not been registered with the {nameof(SceneObjectsRegistry)}.");
                return;
            }

            /*
            var rpcConfig = SceneObjectsRegistry.GetRPCConfig(pipeId, (ushort)rpcId);
            if (!rpcConfig.enabled)
                return;
            */

            // This is the total size of the RPC call which is the header + the byte count of the method arguments.
            buint totalRPCCallSize = MinimumRPCPayloadSize + (buint)parametersPayloadSize;
            if (totalRPCCallSize > k_MaxSingleRPCByteSize)
            {
                ClusterDebug.LogError($"Unable to append RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} is greater then the max RPC call size of: {k_MaxSingleRPCByteSize}");
                return;
            }

            // Does the RPC call fit in our buffer?
            if (totalRPCCallSize >= rpcBuffer.Length)
            {
                ClusterDebug.LogError($"Unable to append RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} does not fit in the RPC buffer of size: {rpcBuffer.Length}");
                return;
            }

            buint startingBufferPos = rpcBufferSize;
            rpcExecutionStage = ShiftRPCExecutionStage(rpcExecutionStage);

            // Here is where we write the called RPC header to the buffer.
            AppendRPCValueTypeParameterValue<ushort>((ushort)(rpcId + 1)); // Adding 1 to RPC ID for stability, see RPCBufferReader.TryParseRPC.
            AppendRPCValueTypeParameterValue<ushort>((ushort)(rpcExecutionStage));
            AppendRPCValueTypeParameterValue<ushort>((ushort)(pipeId + 1)); // Adding 1 to Pipe ID to indicate that this is not a static RPC.
            AppendRPCValueTypeParameterValue<buint>((buint)parametersPayloadSize);

            ClusterDebug.Log($"Sending RPC: (ID: {rpcId}, RPC ExecutionStage: {rpcExecutionStage}, Pipe ID: {pipeId}, Parameters Payload Byte Count: {parametersPayloadSize}, Total RPC Byte Count: {totalRPCCallSize}, Starting Buffer Position: {startingBufferPos})");
        }

        [StaticRPCCallMarker]
        public static void AppendStaticRPCCall (
            string rpcHash, 
            ushort rpcExecutionStage, 
            buint parametersPayloadSize)
        {
            if (!RPCRegistry.RPCHashToRPCId(rpcHash, out var rpcId))
                return;

            // This is the total size of the RPC call which is the header + the byte count of the method arguments.
            buint totalRPCCallSize = MinimumRPCPayloadSize + (buint)parametersPayloadSize;
            if (totalRPCCallSize > k_MaxSingleRPCByteSize)
            {
                ClusterDebug.LogError($"Unable to append RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} is greater then the max RPC call size of: {k_MaxRPCByteBufferSize}");
                return;
            }

            // Does the RPC call fit in our buffer?
            if (totalRPCCallSize >= rpcBuffer.Length)
            {
                ClusterDebug.LogError($"Unable to append RPC call with ID: {rpcId}, the total RPC byte count: {totalRPCCallSize} does not fit in the RPC buffer of size: {rpcBuffer.Length}");
                return;
            }

            buint startingBufferPos = rpcBufferSize;
            rpcExecutionStage = ShiftRPCExecutionStage(rpcExecutionStage);

            // Here is where we write the called RPC header to the buffer.
            AppendRPCValueTypeParameterValue<ushort>((ushort)(rpcId + 1)); // Adding 1 to RPC ID for stability, see RPCBufferReader.TryParseRPC.
            AppendRPCValueTypeParameterValue<ushort>((ushort)(rpcExecutionStage));
            AppendRPCValueTypeParameterValue<ushort>((ushort)(0)); // Pipe ID is zero since this is a static RPC call.
            AppendRPCValueTypeParameterValue<buint>((buint)parametersPayloadSize);

            ClusterDebug.Log($"Sending static RPC: (ID: {rpcId}, RPC Execution Stage: {rpcExecutionStage}, Parameters Payload Byte Count: {parametersPayloadSize}, Total RPC Byte Count: {totalRPCCallSize}, Starting Buffer Position: {startingBufferPos})");
        }
    }
}
