﻿using System;
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

        private static NativeArray<byte> rpcBuffer;

        private static int rpcBufferSize;
        public static int RPCBufferSize => rpcBufferSize;

        // The RPC header is represented by 3 unsigned shorts and a byte:
        //  RPC ID: ushort (2 bytes)
        //  RPC Execution Stage: ushort (2 bytes).
        //  Pipe ID: ushort (2 bytes).
        //  Parameter payload size: ushort (2 bytes).
        private const int MinimumRPCPayloadSize = sizeof(ushort) * 4;

        public static bool AllowWrites = false;

        public static void Initialize (uint maxRpcByteBufferSize)
        {
            rpcBuffer = new NativeArray<byte>((int)maxRpcByteBufferSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            rpcBufferSize = 0;
        }

        public static unsafe bool Latch (NativeArray<byte> buffer, ref int endPos, ulong frame)
        {
            UnsafeUtility.MemCpy((byte*)buffer.GetUnsafePtr() + endPos, rpcBuffer.GetUnsafePtr(), rpcBufferSize);
            #if CLUSTER_DISPLAY_VERBOSE_LOGGING
            UnityEngine.Debug.Log($"Latched RPC Buffer: (Frame: {frame}, Buffer Size: {rpcBufferSize})");
            #endif
            endPos += rpcBufferSize;
            rpcBufferSize = 0;
            return true;
        }

        public static unsafe bool Unlatch (NativeArray<byte> buffer, ulong frame)
        {
            ushort bufferPos = 0;
            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                goto failure;

            if (!RPCRegistry.TryGetInstance(out var rpcRegistry))
                goto failure;

            if (buffer.Length < MinimumRPCPayloadSize)
                goto success;

            UnsafeUtility.MemCpy((byte*)rpcBuffer.GetUnsafePtr(), (byte*)buffer.GetUnsafePtr(), buffer.Length);
            rpcBufferSize = buffer.Length;

            do
            {
                if (bufferPos > buffer.Length - MinimumRPCPayloadSize)
                    goto success;

                ushort startingBufferPos = bufferPos;
                ParseRPCId(ref bufferPos, out var rpcId);

                #if !CLUSTER_DISPLAY_DISABLE_VALIDATION
                if (!rpcRegistry.IsValidRPCId(rpcId))
                {
                    UnityEngine.Debug.LogError($"Recieved potentially invalid RPC data: (ID: ({rpcId} <--- No registered RPC with this ID), Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                    goto failure;
                }
                #endif

                ParseRPCExecutionStage(ref bufferPos, out var rpcExecutionStage);

                ParsePipeID(ref bufferPos, out var bufferPipeId);
                ushort pipeId = (ushort)(bufferPipeId > 0 ? bufferPipeId - 1 : 0);

                ParseParametersPayloadSize(ref bufferPos, out var parametersPayloadSize);

                #if CLUSTER_DISPLAY_VERBOSE_LOGGING
                UnityEngine.Debug.Log($"Processing RPC: (ID: {rpcId}, RPC Execution Stage: {rpcExecutionStage}, Pipe ID: {pipeId}, Parameters Payload Size: {parametersPayloadSize} Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                #endif

                switch (rpcExecutionStage)
                {
                    case RPCExecutionStage.ImmediatelyOnArrival:
                    {
                        if (bufferPipeId == 0)
                        {
                            if (!RPCInterfaceRegistry.TryCallStatic(
                                rpcId, 
                                parametersPayloadSize, 
                                ref bufferPos))
                            {
                                UnityEngine.Debug.LogError($"RPC execution failed for static RPC: (ID: {rpcId}, RPC Execution Stage: {rpcExecutionStage}, Parameters Payload Byte Count: {parametersPayloadSize} Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                                goto failure;
                            }

                            break;
                        }

                        #if !CLUSTER_DISPLAY_DISABLE_VALIDATION
                        if (objectRegistry[pipeId] == null)
                        {
                            UnityEngine.Debug.LogError($"Recieved potentially invalid RPC data: (ID: {rpcId}, RPC Execution Stage: {rpcExecutionStage}, Pipe ID: ({pipeId} <--- No Object is registered with this ID), Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                            goto failure;
                        }
                        #endif

                        ParseParametersPayloadSize(ref bufferPos, out parametersPayloadSize);
                        if (!RPCInterfaceRegistry.TryCallInstance(
                            objectRegistry,
                            rpcId, 
                            pipeId, 
                            parametersPayloadSize, 
                            ref bufferPos))
                        {
                            UnityEngine.Debug.LogError($"RPC execution failed for RPC: (ID: {rpcId}, RPC Execution Stage: {rpcExecutionStage}, Pipe ID: {pipeId}, Parameters Payload Byte Count: {parametersPayloadSize} Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                            goto failure;
                        }

                        ushort consumedParameterByteCount = (ushort)((bufferPos - startingBufferPos) - MinimumRPCPayloadSize);
                        if (parametersPayloadSize > 0 && parametersPayloadSize != consumedParameterByteCount)
                        {
                            UnityEngine.Debug.LogError($"RPC execution failed, parameter payload was not consumed for RPC: (ID: {rpcId}, RPC Execution Stage: {rpcExecutionStage}, Pipe ID: {pipeId}, Parameters Payload Byte Count: {parametersPayloadSize}, Consumed Parameter Payload Byte Count: {consumedParameterByteCount}, Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                            goto failure;
                        }

                    } break;

                    case RPCExecutionStage.BeforeFixedUpdate:
                        RPCInterfaceRegistry.QueueBeforeFixedUpdateRPC(pipeId, rpcId, parametersPayloadSize, bufferPos);
                        bufferPos += parametersPayloadSize;
                        break;

                    case RPCExecutionStage.AfterFixedUpdate:
                        RPCInterfaceRegistry.QueueAfterFixedUpdateRPC(pipeId, rpcId, parametersPayloadSize, bufferPos);
                        bufferPos += parametersPayloadSize;
                        break;

                    case RPCExecutionStage.BeforeUpdate:
                        RPCInterfaceRegistry.QueueBeforeUpdateRPC(pipeId, rpcId, parametersPayloadSize, bufferPos);
                        bufferPos += parametersPayloadSize;
                        break;

                    case RPCExecutionStage.AfterUpdate:
                        RPCInterfaceRegistry.QueueAfterUpdateRPC(pipeId, rpcId, parametersPayloadSize, bufferPos);
                        bufferPos += parametersPayloadSize;
                        break;

                    case RPCExecutionStage.BeforeLateUpdate:
                        RPCInterfaceRegistry.QueueBeforeLateUpdateRPC(pipeId, rpcId, parametersPayloadSize, bufferPos);
                        bufferPos += parametersPayloadSize;
                        break;

                    case RPCExecutionStage.AfterLateUpdate:
                        RPCInterfaceRegistry.QueueAfterLateUpdateRPC(pipeId, rpcId, parametersPayloadSize, bufferPos);
                        bufferPos += parametersPayloadSize;
                        break;

                    case RPCExecutionStage.Automatic:
                    default:
                        UnityEngine.Debug.LogError($"RPC execution failed for static RPC: (ID: {rpcId}, RPC Execution Stage: {rpcExecutionStage}, Parameters Payload Byte Count: {parametersPayloadSize} Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame}), unable to determine it's execution stage automatically.");
                        break;
                }

            } while (true);

            success:
            #if CLUSTER_DISPLAY_VERBOSE_LOGGING
            UnityEngine.Debug.Log($"Finished processing RPCs: (Frame: {frame}, Bytes Processed: {bufferPos}, Buffer Size: {buffer.Length})");
            #endif
            return true;

            failure:
            UnityEngine.Debug.LogError($"Failure occurred while processing RPCs: (Frame: {frame}, Bytes Processed: {bufferPos}, Buffer Size: {buffer.Length})");
            return false;
        }

        [ParseStringMarker]
        public static unsafe string ParseString(ref ushort startPos)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);

            ushort strLen = Marshal.PtrToStructure<ushort>(ptr);
            ptr += sizeof(ushort);

            var str = Encoding.ASCII.GetString((byte*)ptr.ToPointer(), strLen);
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

        private static unsafe void ParseRPCExecutionStage (ref ushort startPos, out RPCExecutionStage rpcExecutionStage)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            rpcExecutionStage = (RPCExecutionStage)Marshal.PtrToStructure<ushort>(ptr);
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
            if (rpcBufferSize + structSize >= rpcBuffer.Length)
                throw new System.Exception("RPC Buffer is full.");

            UnsafeUtility.MemCpy(
                (byte*)rpcBuffer.GetUnsafePtr() + rpcBufferSize, 
                UnsafeUtility.AddressOf(ref value), 
                structSize);

            rpcBufferSize += structSize;
        }

        [RPCCallMarker]
        public static void AppendRPCCall (
            UnityEngine.Object instance, 
            int rpcId, 
            int rpcExecutionStage, 
            int parametersPayloadSize)
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
            rpcExecutionStage = rpcExecutionStage > 0 ? rpcExecutionStage : ((int)RPCExecutor.CurrentExecutionStage + 1);

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
            rpcExecutionStage = rpcExecutionStage > 0 ? rpcExecutionStage : ((int)RPCExecutor.CurrentExecutionStage + 1);

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