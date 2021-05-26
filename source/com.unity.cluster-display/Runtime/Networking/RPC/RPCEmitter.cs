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
        public class AppendRPCStringParameterValueMarker : Attribute {}

        [System.AttributeUsage(System.AttributeTargets.Method)]
        public class AppendRPCArrayParameterValueMarker : Attribute {}

        [System.AttributeUsage(System.AttributeTargets.Method)]
        public class AppendRPCValueTypeParameterValueMarker : Attribute {}

        [System.AttributeUsage(System.AttributeTargets.Method)]
        public class ParseStringMarker : Attribute {}

        [System.AttributeUsage(System.AttributeTargets.Method)]
        public class ParseArrayMarker : Attribute {}

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

        public static unsafe bool Unlatch (NativeArray<byte> buffer, ulong frame)
        {
            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                return false;

            if (!RPCRegistry.TryGetInstance(out var rpcRegistry))
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

                #if !CLUSTER_DISPLAY_DISABLE_VALIDATION
                if (!rpcRegistry.IsValidRPCId(rpcId))
                {
                    UnityEngine.Debug.LogError($"There are no local RPCs registered with the ID: {rpcId}, discarding the rest of the network frame buffer for frame: {frame}");
                    break;
                }
                #endif

                ParsePipeID(ref bufferPos, out var bufferPipeId);

                ushort parametersPayloadSize;
                if (bufferPipeId == 0)
                {
                    ParseParametersPayloadSize(ref bufferPos, out parametersPayloadSize);

                    RPCInterfaceRegistry.TryCallStatic(
                        rpcId, 
                        parametersPayloadSize, 
                        ref bufferPos);

                    UnityEngine.Debug.Log($"Executed static RPC: (ID: {rpcId}, Parameters Payload Size: {parametersPayloadSize})");
                    continue;
                }

                ushort pipeId = (ushort)(bufferPipeId - 1);

                #if !CLUSTER_DISPLAY_DISABLE_VALIDATION
                if (objectRegistry[pipeId] == null)
                {
                    UnityEngine.Debug.LogError($"There are no local objects registered with pipe ID: {pipeId}, discarding the rest of the network frame buffer: {frame}");
                    break;
                }
                #endif

                ParseParametersPayloadSize(ref bufferPos, out parametersPayloadSize);

                if (!RPCInterfaceRegistry.TryCallInstance(
                    objectRegistry,
                    rpcId, 
                    pipeId, 
                    parametersPayloadSize, 
                    ref bufferPos) || (parametersPayloadSize > 0 && parametersPayloadSize == bufferPos))
                {
                    UnityEngine.Debug.LogError($"Unknown failure occurred while attempting to execute RPC for frame: {frame}, check IL Postprocessors");
                    break;
                }

                UnityEngine.Debug.Log($"Executed RPC: (ID: {rpcId}, Pipe ID: {pipeId}, Parameters Payload Size: {parametersPayloadSize})");

            } while (true);

            return true;
        }

        [ParseStringMarker]
        public static unsafe string ParseString(ref ushort startPos)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);

            ushort strLen = Marshal.PtrToStructure<ushort>(ptr);
            ptr += sizeof(ushort);

            var str = Encoding.UTF8.GetString((byte*)ptr.ToPointer(), strLen);
            startPos += (ushort)(sizeof(ushort) + strLen);
            return str;
        }

        [ParseArrayMarker]
        public static unsafe T[] ParseArray<T>(ref ushort startPos) where T : struct
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);

            ushort arrayLength = Marshal.PtrToStructure<ushort>(ptr);
            ptr += sizeof(ushort);

            ushort arrayByteCount = (ushort)(arrayLength * Marshal.SizeOf<T>());

            T[] array = new T[arrayLength];
            void * arrayPtr = UnsafeUtility.AddressOf(ref array[0]);
            UnsafeUtility.MemCpy(arrayPtr, ptr.ToPointer(), arrayByteCount);

            startPos += (ushort)(sizeof(ushort) + arrayByteCount);
            return array;
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

            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                UnsafeUtility.AddressOf(ref strSize), 
                sizeof(ushort));

            rpcBufferSize += sizeof(ushort);

            fixed (char* ptr = value)
                Encoding.ASCII.GetBytes(
                    ptr,
                    strSize,
                    (byte *)rpcBuffer.GetUnsafePtr() + rpcBufferSize,
                    strSize);

            rpcBufferSize += strSize;
        }

        [AppendRPCArrayParameterValueMarker]
        public static unsafe void AppendRPCArrayParameterValues<T>(T[] value) where T : unmanaged
        {
            if (!AllowWrites || value == null)
                return;

            int arrayByteCount = Marshal.SizeOf<T>() * value.Length;

            if (arrayByteCount > ushort.MaxValue)
                throw new System.Exception($"Max string array is: {ushort.MaxValue} characters.");

            if (rpcBufferSize + sizeof(ushort) + arrayByteCount >= rpcBuffer.Length)
                throw new System.Exception("RPC Buffer is full.");

            ushort arrayLength = (ushort)value.Length;
            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                UnsafeUtility.AddressOf(ref arrayLength), 
                sizeof(ushort));

            rpcBufferSize += sizeof(ushort);

            fixed (T* ptr = &value[0])
                UnsafeUtility.MemCpy(
                    (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                    ptr, 
                    arrayByteCount);

            rpcBufferSize += arrayByteCount;
        }

        [AppendRPCValueTypeParameterValueMarker]
        public static unsafe void AppendRPCValueTypeParameterValue<T>(T value) where T : struct
        {
            if (!AllowWrites)
                return;

            int structSize = Marshal.SizeOf<T>(value);
            // UnityEngine.Debug.Log($"Current RPC Buffer Size: {rpcBufferSize}, Struct Size: {structSize}, Max RPC Buffer Size: {rpcBuffer.Length}");
            if (rpcBufferSize + structSize >= rpcBuffer.Length)
                throw new System.Exception("RPC Buffer is full.");

            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                UnsafeUtility.AddressOf(ref value), 
                structSize);

            rpcBufferSize += structSize;
        }

        [RPCCallMarker]
        public static void AppendRPCCall (UnityEngine.Object instance, ushort rpcId, int parametersPayloadSize)
        {
            if (!AllowWrites)
                return;

            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
            {
                UnityEngine.Debug.LogError($"Unable to append RPC call with ID: {rpcId}, the singleton type instance of \"{nameof(ObjectRegistry)}\" cannot be found!");
                return;
            }

            if (!objectRegistry.TryGetPipeId(instance, out var pipeId))
            {
                UnityEngine.Debug.LogError($"Unable to append RPC call with ID: {rpcId}, the calling instance has not been registered with the {nameof(ObjectRegistry)}.");
                return;
            }

            int totalRPCCallSize = sizeof(ushort) * 3 + parametersPayloadSize;
            if (totalRPCCallSize > ushort.MaxValue)
            {
                UnityEngine.Debug.LogError($"Unable to append RPC call with ID: {rpcId}, the total RPC call size: {totalRPCCallSize} is greater then the max RPC call size of: {ushort.MaxValue}");
                return;
            }

            if (totalRPCCallSize >= rpcBuffer.Length)
            {
                UnityEngine.Debug.LogError($"Unable to append RPC call with ID: {rpcId}, the total RPC call size of: {totalRPCCallSize} does not fit in the RPC buffer of size: {rpcBuffer.Length}");
                return;
            }

            AppendRPCValueTypeParameterValue<ushort>((ushort)rpcId);
            AppendRPCValueTypeParameterValue<ushort>((ushort)(pipeId + 1));
            AppendRPCValueTypeParameterValue<ushort>((ushort)parametersPayloadSize);

            UnityEngine.Debug.Log($"Sending RPC: (ID: {rpcId}, Pipe ID: {pipeId}, Parameters Payload Size: {parametersPayloadSize})");
        }

        [StaticRPCCallMarker]
        public static void AppendStaticRPCCall (ushort rpcId, int parametersPayloadSize)
        {
            if (!AllowWrites)
                return;

            if (parametersPayloadSize > ushort.MaxValue)
            {
                UnityEngine.Debug.LogError($"Unable to append RPC call with ID: {rpcId}, the RPC's parameter payload size is larger then the max parameter payload size of: {ushort.MaxValue}");
                return;
            }


            AppendRPCValueTypeParameterValue<ushort>((ushort)rpcId);
            AppendRPCValueTypeParameterValue<ushort>((ushort)0);
            AppendRPCValueTypeParameterValue<ushort>((ushort)parametersPayloadSize);
        }
    }
}
