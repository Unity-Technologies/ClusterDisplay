using System;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using buint = System.UInt32;

namespace Unity.ClusterDisplay.RPC
{
    /// <summary>
    /// In this portion of RPCBufferIO, we read the RPC buffer and interpret it to either immediately or later invoke received RPCs.
    /// </summary>
    public static partial class RPCBufferIO
    {
        /// <summary>
        /// This is used by the ILPostProcessor to find and perform the call.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class ParseStringMarker : Attribute {}

        /// <summary>
        /// This is used by the ILPostProcessor to find and perform the call.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class ParseArrayMarker : Attribute {}

        /// <summary>
        /// This is used by the ILPostProcessor to find and perform the call.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class ParseNativeArrayMarker : Attribute {}

        /// <summary>
        /// This is used by the ILPostProcessor to find and perform the call.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class ParseStructureMarker : Attribute {}

        /// <summary>
        /// Pipe our RPC invocation request into a specific queue to be later executed when we reach that point in the frame.
        /// </summary>
        /// <param name="rpcRequest">The RPC the repeater is supposed to invoke.</param>
        /// <param name="rpcBufferRPCArgumentsStartPosition">The starting position of the RPC's arguments in the RPC buffer.</param>
        private static void QueueRPC (
            ref RPCRequest rpcRequest,
            ref buint rpcBufferRPCArgumentsStartPosition)
        {
            switch (rpcRequest.rpcExecutionStage)
            {
                case RPCExecutionStage.AfterInitialization:
                case RPCExecutionStage.BeforeFixedUpdate:
                    RPCInterfaceRegistry.QueueBeforeFixedUpdateRPC(ref rpcRequest, rpcBufferRPCArgumentsStartPosition);
                    break;

                case RPCExecutionStage.AfterFixedUpdate:
                    RPCInterfaceRegistry.QueueAfterFixedUpdateRPC(ref rpcRequest, rpcBufferRPCArgumentsStartPosition);
                    break;

                case RPCExecutionStage.BeforeUpdate:
                    RPCInterfaceRegistry.QueueBeforeUpdateRPC(ref rpcRequest, rpcBufferRPCArgumentsStartPosition);
                    break;

                case RPCExecutionStage.AfterUpdate:
                    RPCInterfaceRegistry.QueueAfterUpdateRPC(ref rpcRequest, rpcBufferRPCArgumentsStartPosition);
                    break;

                case RPCExecutionStage.BeforeLateUpdate:
                    RPCInterfaceRegistry.QueueBeforeLateUpdateRPC(ref rpcRequest, rpcBufferRPCArgumentsStartPosition);
                    break;

                case RPCExecutionStage.AfterLateUpdate:
                    RPCInterfaceRegistry.QueueAfterLateUpdateRPC(ref rpcRequest, rpcBufferRPCArgumentsStartPosition);
                    break;
            }

             // Shift our RPC read head by the total byte count of the RPC's arguments to the next RPC.
            rpcBufferRPCArgumentsStartPosition += rpcRequest.parametersPayloadSize;
        }

        /// <summary>
        /// The emitter has explitly stated that we should invoke the RPC immediatly after we received it, so lets do that!
        /// </summary>
        /// <param name="rpcRequest">Parsed information about the RPC.</param>
        /// <param name="frame">The frame which we are executing the RPC></param>
        /// <param name="rpcBufferStartPosition">The starting position in the RPC buffer where the RPC's bytes begin.</param>
        /// <param name="bufferPos">After we've finished converting the RPC's arguments and invoking the RPC, we shift the buffer read head to the next RPC in the RPC buffer.</param>
        /// <returns></returns>
        private static bool TryProcessImmediateRPC (
            ref RPCRequest rpcRequest,
            ulong frame,
            buint rpcBufferStartPosition,
            ref buint bufferPos)
        {
            // We have a branch for static RPC methods.
            if (rpcRequest.isStaticRPC)
            {
                if (!RPCInterfaceRegistry.TryCallStatic(
                    rpcRequest.assemblyIndex,
                    rpcRequest.rpcId, 
                    rpcRequest.parametersPayloadSize, 
                    ref bufferPos))
                {
                    UnityEngine.Debug.LogError($"RPC execution failed for static RPC: (ID: {rpcRequest.rpcId}, RPC Execution Stage: {rpcRequest.rpcExecutionStage}, Parameters Payload Byte Count: {rpcRequest.parametersPayloadSize} Starting Buffer Position: {rpcBufferStartPosition}, Bytes Processed: {bufferPos}, Frame: {frame})");
                    return false;
                }

                return false;
            }

            #if !CLUSTER_DISPLAY_DISABLE_VALIDATION
            // Validate whether an instance exists to invoke our RPC on. This is important as the injected IL
            // will happily attempt to invoke this RPC on a null instance.
            if (SceneObjectsRegistry.GetInstance(rpcRequest.pipeId) == null)
            {
                UnityEngine.Debug.LogError($"Recieved potentially invalid RPC data: (ID: {rpcRequest.rpcId}, RPC Execution Stage: {rpcRequest.rpcExecutionStage}, Pipe ID: ({rpcRequest.pipeId} <--- No Object is registered with this ID), Starting Buffer Position: {rpcBufferStartPosition}, Bytes Processed: {bufferPos}, Frame: {frame})");
                return false;
            }
            #endif

            // Invoke the RPC on our instance.
            if (!RPCInterfaceRegistry.TryCallInstance(
                rpcRequest.assemblyIndex,
                rpcRequest.rpcId, 
                rpcRequest.pipeId, 
                rpcRequest.parametersPayloadSize, 
                ref bufferPos))
            {
                UnityEngine.Debug.LogError($"RPC execution failed for RPC: (ID: {rpcRequest.rpcId}, RPC Execution Stage: {rpcRequest.rpcExecutionStage}, Pipe ID: {rpcRequest.pipeId}, Parameters Payload Byte Count: {rpcRequest.parametersPayloadSize} Starting Buffer Position: {rpcBufferStartPosition}, Bytes Processed: {bufferPos}, Frame: {frame})");
                return false;
            }

            // Determine whether all arguments were converted from bytes into the expected type instances.
            buint consumedParameterByteCount = (bufferPos - rpcBufferStartPosition) - MinimumRPCPayloadSize;

            // Something went wrong, the current read head is stuck somewhere in the middle of an RPC in the RPC buffer.
            if (rpcRequest.parametersPayloadSize > 0 && rpcRequest.parametersPayloadSize != consumedParameterByteCount)
            {
                UnityEngine.Debug.LogError($"RPC execution failed, parameter payload was not consumed for RPC: (ID: {rpcRequest.rpcId}, RPC Execution Stage: {rpcRequest.rpcExecutionStage}, Pipe ID: {rpcRequest.pipeId}, Parameters Payload Byte Count: {rpcRequest.parametersPayloadSize}, Consumed Parameter Payload Byte Count: {consumedParameterByteCount}, Starting Buffer Position: {rpcBufferStartPosition}, Bytes Processed: {bufferPos}, Frame: {frame})");
                return false;
            }

            return true;
        }

        /// <summary>
        /// This converts bytes in the RPC buffer to an RPC header which is a fixed size.
        /// </summary>
        /// <param name="rpcRequest">The ourput RPC header.</param>
        /// <param name="frame">The frame which the RPC is going to be invoked.</param>
        /// <param name="startingBufferPos">The starting position which this RPC begins in the RPC buffer.</param>
        /// <param name="bufferPos">We modify our buffer read head to the start of the RPC's arguments position in the RPC buffer after we've read the RPC header.</param>
        /// <returns></returns>
        private static bool TryParseRPC (
            out RPCRequest rpcRequest,
            ulong frame,
            buint startingBufferPos,
            ref buint bufferPos)
        {
            ParseRPCId(ref bufferPos, out rpcRequest.rpcId);

            #if !CLUSTER_DISPLAY_DISABLE_VALIDATION
            // Validate whether the received RPC has even been registered with the repeater node. If the RPC buffer is incorrectly read and we lose track of
            // where the read head is in the RPC buffer, this may result in some bizzare RPC ids since it's reading aross random bytes.
            if (!RPCRegistry.MethodRegistered(rpcRequest.rpcId))
            {
                UnityEngine.Debug.LogError($"Recieved potentially invalid RPC data: (ID: ({rpcRequest.rpcId} <--- No registered RPC with this ID), Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
                rpcRequest = default(RPCRequest);
                return false;
            }
            #endif

            ParseRPCExecutionStage(ref bufferPos, out rpcRequest.rpcExecutionStage);
            ParsePipeID(ref bufferPos, out var bufferPipeId);

            // Since pipe ID is a unsigned short, this means that an isntance could potentially have a pipe ID of 0 and we can't
            // have represent negative one (-1) to be a static RPC method. Therefore, when the emitter
            // writes the pipe ID, it adds 1 to the pipe ID allowing the pipe ID of value: 0 to represent that the RPC is a static
            // method. After the repeater has determined whether it's static or instance, we subtract 1 to get the pipe ID.
            rpcRequest.pipeId = (ushort)(bufferPipeId > 0 ? bufferPipeId - 1 : 0);
            rpcRequest.isStaticRPC = bufferPipeId == 0;

            ParseParametersPayloadSize(ref bufferPos, out rpcRequest.parametersPayloadSize);

            #if CLUSTER_DISPLAY_VERBOSE_LOGGING
            UnityEngine.Debug.Log($"Processing RPC: (ID: {rpcRequest.rpcId}, RPC Execution Stage: {rpcRequest.rpcExecutionStage}, Pipe ID: {rpcRequest.pipeId}, Parameters Payload Size: {rpcRequest.parametersPayloadSize} Starting Buffer Position: {startingBufferPos}, Bytes Processed: {bufferPos}, Frame: {frame})");
            #endif

            // Determine whether we've associated an assembly with an RPC id and store the assembly index to later use when we invoke the RPC.
            if (!RPCRegistry.TryGetAssemblyIndex(rpcRequest.rpcId, out rpcRequest.assemblyIndex))
            {
                UnityEngine.Debug.LogError($"There is no assembly registered for RPC with ID: {rpcRequest.rpcId}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Using Latch & Unlatch terminology where the emitter latches after it finishes writing to the RPC buffer, 
        /// and the repeater unlatches when it begins reading the received RPC buffer. In this case, we are unlatching
        /// and interpreting the RPC buffer. However, there is no latch indicator in the byte buffer.
        /// </summary>
        /// <param name="buffer">This is a copy of the network frame buffer slice of bytes that represents the RPC buffer.</param>
        /// <param name="frame">The current frame were on for logging/debugging purposes.</param>
        /// <returns></returns>
        public static unsafe bool Unlatch (NativeArray<byte> buffer, ulong frame)
        {
            buint bufferPos = 0;
            // If this is true, then there are no RPCs to invoke in the buffer.
            if (buffer.Length < MinimumRPCPayloadSize)
                goto success;

            // Copy our buffer into our pre-initialized RPC buffer class member.
            // TODO: This should probably be refactored so we don't have to do this again.
            UnsafeUtility.MemCpy((byte*)rpcBuffer.GetUnsafePtr(), (byte*)buffer.GetUnsafePtr(), buffer.Length);

            // This is the number of bytes we've received in the RPC buffer.
            rpcBufferSize = (buint)buffer.Length;

            do
            {
                // Have we reached the end of the buffer?
                if (bufferPos > buffer.Length - MinimumRPCPayloadSize)
                    goto success;

                // Store the start position of the next RPC in the RPC buffer.
                buint startingBufferPos = bufferPos;

                // Convert the bytes into a usable RPC header.
                if (!TryParseRPC(
                    out var rpcRequest,
                    frame,
                    startingBufferPos,
                    ref bufferPos))
                    goto failure;

                switch (rpcRequest.rpcExecutionStage)
                {
                    // Exsecute the received RPC immediately if the emitter explicitly specified that this should happen.
                    case RPCExecutionStage.ImmediatelyOnArrival:
                    {
                        if (!TryProcessImmediateRPC(
                            ref rpcRequest,
                            frame,
                            startingBufferPos,
                            ref bufferPos))
                            goto failure;

                    } break;

                    // Queue the RPC to be executed sometime later throughout the frame.
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

                    // The emitter node should never send an RPC with it's RPCExecutionStage set to Automatic, since the
                    // emitter is supposed to determine when the repeater node should indeed execute the RPC.
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

        /// <summary>
        /// This converts bytes in the RPC buffer to a string.
        /// </summary>
        /// <param name="startPos">The position at which we've stored the length of the string in the RPC buffer.</param>
        /// <returns></returns>
        [ParseStringMarker]
        public static unsafe string ParseString(ref buint startPos)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos); // Get a pointer to our read head position in the RPC buffer.

            // Get the string length from the buffer.
            buint strLen = Marshal.PtrToStructure<buint>(ptr);
            ptr += sizeof(buint);

            // Extract the string using the length we've retrieved.
            var str = Encoding.ASCII.GetString((byte*)ptr.ToPointer(), (int)strLen);
            startPos += sizeof(buint) + strLen; // Move our read head.

            // Return our converted string.
            return str;
        }

        /// <summary>
        /// This converts bytes in the RPC buffer to an array of an expected type. 
        /// </summary>
        /// <typeparam name="T">The element type of the array that we are expecting.</typeparam>
        /// <param name="startPos">The position at which we've stored the count of the array in the RPC buffer.</param>
        /// <returns></returns>
        [ParseArrayMarker]
        public static unsafe T[] ParseArray<T>(ref buint startPos) where T : struct
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos); // Get a pointer to our read head position in the RPC buffer.

            // Get the array length from the buffer.
            buint arrayLength = Marshal.PtrToStructure<buint>(ptr);
            ptr += sizeof(buint);

            // Get the total expected byte count of the array.
           buint arrayByteCount = (buint)(arrayLength * Marshal.SizeOf<T>());

            // Create a new array of our expected type.
            T[] array = new T[arrayLength];

            // Pin a pointer of the first element of our array.
            void * arrayPtr = UnsafeUtility.AddressOf(ref array[0]);

            // Copy over the bytes the RPC buffer to our array.
            UnsafeUtility.MemCpy(arrayPtr, ptr.ToPointer(), arrayByteCount);

            startPos += sizeof(buint) + arrayByteCount; // Move our read head.

            // Return our converted array.
            return array;
        }

        /// <summary>
        /// Convert RPC buffer bytes to NativeArray of an expected type.
        /// </summary>
        /// <typeparam name="T">The element type of NativeArray that we expect.</typeparam>
        /// <param name="startPos">The position at which we've stored the count of the NativeArray in the RPC buffer.</param>
        /// <returns></returns>
        [ParseNativeArrayMarker]
        public static unsafe NativeArray<T> ParseNativeCollection<T>(ref buint startPos) where T : struct
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos); // Get a pointer to our read head position in the RPC buffer.

            // Get the NativeArray length.
            buint arrayLength = Marshal.PtrToStructure<buint>(ptr);
            ptr += sizeof(buint);

            // Get the total byte count of the NativeArray of our expected type.
           buint arrayByteCount = (buint)(arrayLength * Marshal.SizeOf<T>());

             // TODO, think about how we can be more explicit about the alloation type. At the moment NativeArray arguments can only be used in jobs.
            NativeArray<T> nativeArray = new NativeArray<T>((int)arrayLength, Allocator.TempJob);
            // Copy our bytes into the native array.
            UnsafeUtility.MemCpy(nativeArray.GetUnsafePtr(), ptr.ToPointer(), arrayByteCount);

            startPos += sizeof(buint) + arrayByteCount; // Move our read head.

            // Return our converted native array.
            return nativeArray;
        }

        /// <summary>
        /// Simply convert RPC buffer bytes to an expected struct. 
        /// </summary>
        /// <typeparam name="T">The expected struct type.</typeparam>
        /// <param name="startPos">The byte position in our RPC buffer that we expect the struct to start.</param>
        /// <returns></returns>
        [ParseStructureMarker]
        public static unsafe T ParseStructure<T> (ref buint startPos)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos); // Get a pointer to our read head position in the RPC buffer.
            startPos += (buint)Marshal.SizeOf<T>(); // Move our read head.
            // Convert the bytes to our struct.
            return Marshal.PtrToStructure<T>(ptr);
        }

        /// <summary>
        /// Conver the RPC header's pipe/instance ID byte representation to ushort.
        /// </summary>
        /// <param name="startPos">The position at which we expect the bytes for the RPC pipe ID to start in the RPC buffer.</param>
        /// <param name="pipeId">The resulting pipe ID</param>
        private static unsafe void ParsePipeID (ref buint startPos, out ushort pipeId)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos); // Get a pointer to our read head position in the RPC buffer.
            // Convert the bytes to the expected Pipe ID of the RPC.
            pipeId = Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort); // Move the read head.
        }

        /// <summary>
        /// Convert RPC header's RPC ID byte representation to ushort.
        /// </summary>
        /// <param name="startPos">The position at which we expect the bytes for the RPC ID to start in the RPC buffer.</param>
        /// <param name="rpcId">The resulting RPC ID.</param>
        private static unsafe void ParseRPCId (ref buint startPos, out ushort rpcId)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos); // Get a pointer to our read head position in the RPC buffer.
            // Convert the bytes to the expected RPC ID of the RPC.
            rpcId = Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort); // Move the read head.
        }

        /// <summary>
        /// Convert RPC header's RPC Execution Stage byte representation to the enumeration.
        /// </summary>
        /// <param name="startPos">The position at which we expect the bytes for the RPC execution stage to start in the RPC buffer.</param>
        /// <param name="rpcExecutionStage">The resulting RPC Execution Stage</param>
        private static unsafe void ParseRPCExecutionStage (ref buint startPos, out RPCExecutionStage rpcExecutionStage)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos); // Get a pointer to our read head position in the RPC buffer.
            // Convert the bytes to the expected RPCExecutionStage of the RPC.
            rpcExecutionStage = (RPCExecutionStage)Marshal.PtrToStructure<ushort>(ptr);
            startPos += sizeof(ushort); // Move the read head.
        }

        /// <summary>
        /// Convert RPC header's parameter payload size byte representation to a buffer size type.
        /// </summary>
        /// <param name="startPos">The position at which we expect the bytes for the parameter payload size to start in the RPC buffer.</param>
        /// <param name="parametersPayloadSize">The resulting parameter payload size.</param>
        private static unsafe void ParseParametersPayloadSize (ref buint startPos, out buint parametersPayloadSize)
        {
            var ptr = new IntPtr((byte*)rpcBuffer.GetUnsafePtr() + startPos); // Get a pointer to our read head position in the RPC buffer.
            // Convert the bytes to our expected RPC total arguments byte count.
            parametersPayloadSize = Marshal.PtrToStructure<uint>(ptr);
            startPos += sizeof(uint); // Move the read head.
        }
    }
}
