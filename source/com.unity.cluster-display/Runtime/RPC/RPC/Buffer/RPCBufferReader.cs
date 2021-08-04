using System;
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
        public class ParseStringMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class ParseArrayMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class ParseNativeArrayMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class ParseStructureMarker : Attribute {}

        private static void QueueRPC (
            ref RPCRequest rpcRequest,
            ref buint bufferPos)
        {
            switch (rpcRequest.rpcExecutionStage)
            {
                case RPCExecutionStage.AfterInitialization:
                case RPCExecutionStage.BeforeFixedUpdate:
                    RPCInterfaceRegistry.QueueBeforeFixedUpdateRPC(ref rpcRequest, bufferPos);
                    break;

                case RPCExecutionStage.AfterFixedUpdate:
                    RPCInterfaceRegistry.QueueAfterFixedUpdateRPC(ref rpcRequest, bufferPos);
                    break;

                case RPCExecutionStage.BeforeUpdate:
                    RPCInterfaceRegistry.QueueBeforeUpdateRPC(ref rpcRequest, bufferPos);
                    break;

                case RPCExecutionStage.AfterUpdate:
                    RPCInterfaceRegistry.QueueAfterUpdateRPC(ref rpcRequest, bufferPos);
                    break;

                case RPCExecutionStage.BeforeLateUpdate:
                    RPCInterfaceRegistry.QueueBeforeLateUpdateRPC(ref rpcRequest, bufferPos);
                    break;

                case RPCExecutionStage.AfterLateUpdate:
                    RPCInterfaceRegistry.QueueAfterLateUpdateRPC(ref rpcRequest, bufferPos);
                    break;
            }

            bufferPos += rpcRequest.parametersPayloadSize;
        }

        private static bool TryProcessImmediateRPC (
            ref RPCRequest rpcRequest,
            ulong frame,
            buint startingBufferPos,
            ref buint bufferPos)
        {
            if (rpcRequest.isStaticRPC)
            {
                if (!RPCInterfaceRegistry.TryCallStatic(
                    rpcRequest.assemblyIndex,
                    rpcRequest.rpcId, 
                    rpcRequest.parametersPayloadSize, 
                    ref bufferPos))
                {
                    UnityEngine.Debug.LogError($"RPC execution failed for static RPC: (ID: {rpcRequest.rpcId}, RPC Execution Stage: {rpcRequest.rpcExecutionStage}, Parameters Payload Byte Count: {rpcRequest.parametersPayloadSize} Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                    return false;
                }

                return false;
            }

            #if !CLUSTER_DISPLAY_DISABLE_VALIDATION
            if (SceneObjectsRegistry.GetInstance(rpcRequest.pipeId) == null)
            {
                UnityEngine.Debug.LogError($"Recieved potentially invalid RPC data: (ID: {rpcRequest.rpcId}, RPC Execution Stage: {rpcRequest.rpcExecutionStage}, Pipe ID: ({rpcRequest.pipeId} <--- No Object is registered with this ID), Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                return false;
            }
            #endif

            ParseParametersPayloadSize(ref bufferPos, out rpcRequest.parametersPayloadSize);
            if (!RPCInterfaceRegistry.TryCallInstance(
                rpcRequest.assemblyIndex,
                rpcRequest.rpcId, 
                rpcRequest.pipeId, 
                rpcRequest.parametersPayloadSize, 
                ref bufferPos))
            {
                UnityEngine.Debug.LogError($"RPC execution failed for RPC: (ID: {rpcRequest.rpcId}, RPC Execution Stage: {rpcRequest.rpcExecutionStage}, Pipe ID: {rpcRequest.pipeId}, Parameters Payload Byte Count: {rpcRequest.parametersPayloadSize} Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                return false;
            }

            buint consumedParameterByteCount = (bufferPos - startingBufferPos) - MinimumRPCPayloadSize;
            if (rpcRequest.parametersPayloadSize > 0 && rpcRequest.parametersPayloadSize != consumedParameterByteCount)
            {
                UnityEngine.Debug.LogError($"RPC execution failed, parameter payload was not consumed for RPC: (ID: {rpcRequest.rpcId}, RPC Execution Stage: {rpcRequest.rpcExecutionStage}, Pipe ID: {rpcRequest.pipeId}, Parameters Payload Byte Count: {rpcRequest.parametersPayloadSize}, Consumed Parameter Payload Byte Count: {consumedParameterByteCount}, Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                return false;
            }

            return true;
        }

        private static bool TryParseRPC (
            out RPCRequest rpcRequest,
            ulong frame,
            buint startingBufferPos,
            ref buint bufferPos)
        {
            ParseRPCId(ref bufferPos, out rpcRequest.rpcId);

            #if !CLUSTER_DISPLAY_DISABLE_VALIDATION
            if (!RPCRegistry.MethodRegistered(rpcRequest.rpcId))
            {
                UnityEngine.Debug.LogError($"Recieved potentially invalid RPC data: (ID: ({rpcRequest.rpcId} <--- No registered RPC with this ID), Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                rpcRequest = default(RPCRequest);
                return false;
            }
            #endif

            ParseRPCExecutionStage(ref bufferPos, out rpcRequest.rpcExecutionStage);

            ParsePipeID(ref bufferPos, out var bufferPipeId);
            rpcRequest.pipeId = (ushort)(bufferPipeId > 0 ? bufferPipeId - 1 : 0);
            rpcRequest.isStaticRPC = bufferPipeId == 0;

            ParseParametersPayloadSize(ref bufferPos, out rpcRequest.parametersPayloadSize);

            #if CLUSTER_DISPLAY_VERBOSE_LOGGING
            UnityEngine.Debug.Log($"Processing RPC: (ID: {rpcId}, RPC Execution Stage: {rpcExecutionStage}, Pipe ID: {pipeId}, Parameters Payload Size: {parametersPayloadSize} Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
            #endif

            if (!RPCRegistry.TryGetAssemblyIndex(rpcRequest.rpcId, out rpcRequest.assemblyIndex))
            {
                UnityEngine.Debug.LogError($"There is no assembly registered for RPC with ID: {rpcRequest.rpcId}");
                return false;
            }

            return true;
        }

        public static unsafe bool Unlatch (NativeArray<byte> buffer, ulong frame)
        {
            buint bufferPos = 0;
            if (buffer.Length < MinimumRPCPayloadSize)
                goto success;

            UnsafeUtility.MemCpy((byte*)rpcBuffer.GetUnsafePtr(), (byte*)buffer.GetUnsafePtr(), buffer.Length);
            rpcBufferSize = (buint)buffer.Length;

            do
            {
                if (bufferPos > buffer.Length - MinimumRPCPayloadSize)
                    goto success;

                buint startingBufferPos = bufferPos;
                if (!TryParseRPC(
                    out var rpcRequest,
                    frame,
                    startingBufferPos,
                    ref bufferPos))
                    goto failure;

                switch (rpcRequest.rpcExecutionStage)
                {
                    case RPCExecutionStage.ImmediatelyOnArrival:
                    {
                        if (!TryProcessImmediateRPC(
                            ref rpcRequest,
                            frame,
                            startingBufferPos,
                            ref bufferPos))
                            goto failure;

                    } break;

                    case RPCExecutionStage.AfterInitialization:
                    case RPCExecutionStage.BeforeFixedUpdate:
                    case RPCExecutionStage.AfterFixedUpdate:
                    case RPCExecutionStage.BeforeUpdate:
                    case RPCExecutionStage.AfterUpdate:
                    case RPCExecutionStage.BeforeLateUpdate:
                    case RPCExecutionStage.AfterLateUpdate:
                    {
                        QueueRPC(
                            ref rpcRequest,
                            ref bufferPos);
                    } break;

                    case RPCExecutionStage.Automatic:
                    default:
                        UnityEngine.Debug.LogError($"RPC execution failed for static RPC: (ID: {rpcRequest.rpcId}, RPC Execution Stage: {rpcRequest.rpcExecutionStage}, Parameters Payload Byte Count: {rpcRequest.parametersPayloadSize} Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame}), unable to determine it's execution stage automatically.");
                        goto failure;
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
        public static unsafe string ParseString(ref buint startPos)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);

            buint strLen = Marshal.PtrToStructure<buint>(ptr);
            ptr += sizeof(buint);

            var str = Encoding.ASCII.GetString((byte*)ptr.ToPointer(), (int)strLen);
            startPos += sizeof(buint) + strLen;
            return str;
        }

        [ParseArrayMarker]
        public static unsafe T[] ParseArray<T>(ref buint startPos) where T : struct
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);

            buint arrayLength = Marshal.PtrToStructure<buint>(ptr);
            ptr += sizeof(buint);

           buint arrayByteCount = (buint)(arrayLength * Marshal.SizeOf<T>());

            T[] array = new T[arrayLength];
            void * arrayPtr = UnsafeUtility.AddressOf(ref array[0]);
            UnsafeUtility.MemCpy(arrayPtr, ptr.ToPointer(), arrayByteCount);

            startPos += sizeof(buint) + arrayByteCount;
            return array;
        }

        [ParseNativeArrayMarker]
        public static unsafe NativeArray<T> ParseNativeCollection<T>(ref buint startPos) where T : struct
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);

            buint arrayLength = Marshal.PtrToStructure<buint>(ptr);
            ptr += sizeof(buint);

           buint arrayByteCount = (buint)(arrayLength * Marshal.SizeOf<T>());

            NativeArray<T> nativeArray = new NativeArray<T>((int)arrayLength, Allocator.TempJob);
            UnsafeUtility.MemCpy(nativeArray.GetUnsafePtr(), ptr.ToPointer(), arrayByteCount);

            startPos += sizeof(buint) + arrayByteCount;
            return nativeArray;
        }

        [ParseStructureMarker]
        public static unsafe T ParseStructure<T> (ref buint startPos)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            startPos += (buint)Marshal.SizeOf<T>();
            return Marshal.PtrToStructure<T>(ptr);
        }

        private static unsafe void ParsePipeID (ref buint startPos, out ushort pipeId)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            pipeId = Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort);
        }

        private static unsafe void ParseRPCId (ref buint startPos, out ushort rpcId)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            rpcId = Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort);
        }

        private static unsafe void ParseRPCExecutionStage (ref buint startPos, out RPCExecutionStage rpcExecutionStage)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            rpcExecutionStage = (RPCExecutionStage)Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort);
        }

        private static unsafe void ParseParametersPayloadSize (ref buint startPos, out buint parametersPayloadSize)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos);
            parametersPayloadSize = Marshal.PtrToStructure<uint>(ptr);
            startPos += sizeof(uint);
        }
    }
}
