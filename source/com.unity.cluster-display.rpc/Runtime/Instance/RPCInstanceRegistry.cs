using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.ClusterDisplay.RPC;
using UnityEngine;
using buint = System.UInt32;

using onTryCallInstanceFuncAlias = System.Func<string, ushort, System.UInt32, System.UInt32, bool>;
using onTryCallStaticFuncAlias = System.Func<string, System.UInt32, System.UInt32, bool>;
using onTryCallQueuedInstanceFuncAlias = System.Func<string, ushort, System.UInt32, System.UInt32, bool>;

namespace Unity.ClusterDisplay
{
    ///                     Base Class                       
    ///                    ┌──────────────────────┐             
    ///                    │                      │             
    ///                    │ RPCInterfaceRegistry ├───┐         
    ///                    │                      │   │         
    ///                    └──────────────────────┘   │         
    ///                                               │         
    ///                                           Derrives      
    /// ┌───────────────┐                             │         
    /// │               │                    ┌────────┴───────┐ 
    /// │  Assembly A   ├────────────────────► RPCIL Instance │ 
    /// │               │                    └────────┬───────┘ 
    /// └───────────────┘                             │         
    ///                                               │         
    ///                                               │         
    ///                                               │         
    /// ┌───────────────┐                             │         
    /// │               │                     ┌───────┴────────┐
    /// │  Assembly B   ├─────────────────────► RPCIL Instance │
    /// │               │                     └───────┬────────┘
    /// └───────────────┘                             │         
    ///                                               │         
    ///                                               │         
    ///                                               │         
    /// ┌───────────────┐                             │         
    /// │               │                     ┌───────┴────────┐
    /// │  Assembly C   ├─────────────────────► RPCIL Instance │
    /// │               │                     └────────────────┘
    /// └───────────────┘

    /// <summary>
    /// This class serves two purposes:<br/>
    /// - IL injected RPC classes derrive from this class for access protected members to invoke RPCs.<br/>
    /// - Manages and queuing and dequing of RPCs for deferred invocation.<br/>
    /// <br/>
    /// If you want to understand this better, I recommend opening up ILSpy and inspecting assemblies with types<br/>
    /// that have RPC members to find the RPCIL type located in the Unity.ClusterDisplay.Generated namespace.<br/>
    /// <br/>
    /// These structures exists for the following reasons:<br/>
    /// - Improved performance since we perform reflection during compile time to generate the RPC invocation IL code.<br/>
    /// - Assemblies are ILPostProcessed in parallel separately, thats why we generate one RPCIL per assembly. <br/>
    /// </summary>
    public abstract partial class RPCInterfaceRegistry
    {
        /// <summary>
        /// When we queue the future invocation of an RPC, we wrap it in this container before it goes in the queue.
        /// </summary>
        protected struct QueuedRPCCall
        {
            internal RPCRequest rpcRequest;
            internal buint rpcsBufferParametersStartPosition;
        }

        /// <summary>
        /// This delegate declaration is used by the generated RPCIL type derrived from this class and it's 
        /// used to execute the implementation of instance RPC invocations in that generated type.
        /// </summary>
        /// <param name="rpcId">The RPC ID that we want to invoke.</param>
        /// <param name="pipeId">The instance ID that we want to invoke.</param>
        /// <param name="parametersPayloadSize">The total byte count of all of the RPC's argument values.</param>
        /// <param name="rpcBufferParameterPosition">The position at which the RPC's arguments begin in the RPC buffer.</param>
        /// <returns></returns>
        [OnTryCallInstanceDelegateAttribute] protected delegate bool OnTryCallInstanceDelegate(
            string rpcId, 
            ushort pipeId, 
            buint parametersPayloadSize, 
            buint rpcBufferParameterPosition);

        /// <summary>
        /// This delegate declaration is used by the generated RPCIL type derrived from this class and it's 
        /// used to execute the implementation of static RPC invocations in that generated type.
        /// </summary>
        /// <param name="rpcId">The RPC ID that we want to invoke.</param>
        /// <param name="parametersPayloadSize">The total byte count of all of the RPC's argument values.</param>
        /// <param name="rpcBufferParameterPosition">The position at which the RPC's arguments begin in the RPC buffer.</param>
        /// <returns></returns>
        [OnTryCallStaticDelegateAttribute] protected delegate bool OnTryCallStaticDelegate(
            string rpcId, 
            buint parametersPayloadSize, 
            buint rpcBufferParameterPosition);

        /// <summary>
        /// This delegate declaration is used by the generated RPCIL type derrived from this class and it's 
        /// used to execute the implementation of queued RPC invocations in that generated type.
        /// </summary>
        /// <param name="rpcId">The RPC ID that we want to invoke.</param>
        /// <param name="pipeId">The instance ID that we want to invoke.</param>
        /// <param name="parametersPayloadSize">The total byte count of all of the RPC's argument values.</param>
        /// <param name="rpcBufferParameterPosition">The position at which the RPC's arguments begin in the RPC buffer.</param>
        /// <returns></returns>
        [OnTryCallQueuedInstanceDelegateAttribute] protected delegate bool OnTryCallQueuedInstanceDelegate(
            string rpcId, 
            ushort pipeId, 
            buint parametersPayloadSize, 
            buint rpcBufferParameterPosition);

        /// Queue for invoking RPCs BEFORE any MonoBehaviour.FixedUpdate occurs in a frame.
        static readonly Queue<QueuedRPCCall> beforeFixedUpdateRPCQueue = new Queue<QueuedRPCCall>();
        /// Queue for invoking RPCs BEFORE any MonoBehaviour.Update occurs in a frame.
        static readonly Queue<QueuedRPCCall> beforeUpdateRPCQueue = new Queue<QueuedRPCCall>();
        /// Queue for invoking RPCs BEFORE any MonoBehaviour.LateUpdate occurs in a frame.
        static readonly Queue<QueuedRPCCall> beforeLateUpdateRPCQueue = new Queue<QueuedRPCCall>();

        /// Queue for invoking RPCs AFTER any MonoBehaviour.FixedUpdate occurs in a frame.
        static readonly Queue<QueuedRPCCall> afterFixedUpdateRPCQueue = new Queue<QueuedRPCCall>();
        /// Queue for invoking RPCs AFTER any MonoBehaviour.Update occurs in a frame.
        static readonly Queue<QueuedRPCCall> afterUpdateRPCQueue = new Queue<QueuedRPCCall>();
        /// Queue for invoking RPCs AFTER any MonoBehaviour.LateUpdate occurs in a frame.
        static readonly Queue<QueuedRPCCall> afterLateUpdateRPCQueue = new Queue<QueuedRPCCall>();

        struct ImplementationInstance
        {
            /// There is one instance of a the generated RPC invocation type that implements RPCInterfaceRegistry per 
            /// assembly, and we store those instances in this list. See the diagram above at the top of this source file.
            readonly public RPCInterfaceRegistry instance;

            ///  Useful for debugging.
            readonly public Assembly assembly;

            /// This delegates represents the implementation to immediately execute instance RPC 
            /// invocations delcared in an instance of the generated RPCIL type, one per assembly.
            readonly public onTryCallInstanceFuncAlias onTryCallInstance;

            /// This delegates represents the implementation to immediately execute static RPC 
            /// invocations delcared in an instance of the generated RPCIL type, one per assembly.
            readonly public onTryCallStaticFuncAlias onTryCallStatic;

            /// This delegates represents the implementation to execute queued RPC invocations
            /// delcared in an instance of the generated RPCIL type, one per assembly. 
            readonly public onTryCallQueuedInstanceFuncAlias onTryCallQueuedInstance;

            public ImplementationInstance (
                RPCInterfaceRegistry newInstance,
                Assembly fromAssembly,
                OnTryCallInstanceDelegate onTryCallInstanceDel,
                OnTryCallStaticDelegate onTryCallStaticDel,
                OnTryCallQueuedInstanceDelegate onTryCallQueuedInstanceDel)
            {
                instance = newInstance;
                assembly = fromAssembly;
                onTryCallInstance = onTryCallInstanceDel.Invoke;
                onTryCallStatic = onTryCallStaticDel.Invoke;
                onTryCallQueuedInstance = onTryCallQueuedInstanceDel.Invoke;
            }
        }

        readonly static List<ImplementationInstance> m_ImplementationInstances = new List<ImplementationInstance>();

        /// <summary>
        /// Determine whether an assembly has a generated RPC invocation type.
        /// </summary>
        /// <param name="assembly">The assembly were lookin in.</param>
        /// <returns>Whether that generated type exists.</returns>
        internal static bool HasImplementationInAssembly (System.Reflection.Assembly assembly) => 
            assembly
                .GetTypes()
                .Any(type => type.Name == "RPCIL");

        /// <summary>
        /// Create an instance of the generated RPC invocation type and cache it so we can later use it to invoke our received RPCs.
        /// </summary>
        /// <param name="assembly">The registered assembly where we are getting the type.</param>
        /// <param name="assemblyIndex">The index of the registered RPC.</param>
        /// <returns></returns>
        internal static bool TryCreateImplementationInstance (System.Reflection.Assembly assembly, out ushort assemblyIndex)
        {
            var rpcILType = assembly
                .GetTypes()
                .FirstOrDefault(type => type.Name == "RPCIL"); // Attempt to find the generated type.

            if (rpcILType == null)
            {
                ClusterDebug.LogError($"Unable to create instance of: \"RPCIL\", it does not exist in the assembly: \"{assembly.GetName().Name}\", please verify that you've registered this assembly with RPCRegistry.");
                assemblyIndex = 0;
                return false;
            }

            // Determine the whether we already have an instance of the generated type.
            var instance = m_ImplementationInstances.FirstOrDefault(i =>
                i.instance != null && i.instance.GetType().Assembly.FullName == assembly.FullName).instance;

            if (instance == null)
            {
                // The derrived generate type adds itself to the m_ImplementationInstances in it's base constructor. That's
                // why its not happening here. Inspect the generated RPC invocation type for more info.
                instance = Activator.CreateInstance(rpcILType) as RPCInterfaceRegistry;
            }

            // The base constructor of RPCIL defined below adds the instance of RPCIL before we get to the following line:
            assemblyIndex = (ushort)m_ImplementationInstances.FindIndex(i => i.instance == instance);
            ClusterDebug.Log($"Retrieved instance of type: \"{rpcILType.FullName}\" in assembly: \"{assembly.GetName().Name}\" at index: {assemblyIndex}");
            return true;
        }

        // When we create an instance of the generated RPCIL type, it's base constructor is
        // called to register the delegates to our static list of delegates per assembly.
        [RPCInterfaceRegistryConstuctorMarker] protected RPCInterfaceRegistry (
            OnTryCallInstanceDelegate onTryCallInstance,
            OnTryCallStaticDelegate onTryCallStatic,
            OnTryCallQueuedInstanceDelegate onTryCallQueuedInstance)
        {
            var instanceType = new System.Diagnostics.StackTrace().GetFrame(1).GetMethod().DeclaringType;

            // Register the IL injected methods for RPC argument conversion and execution. 
            m_ImplementationInstances.Add(new ImplementationInstance(
                this,
                GetType().Assembly,
                onTryCallInstance,
                onTryCallStatic,
                onTryCallQueuedInstance
            ));

            ClusterDebug.Log($"Registering instance of: \"{instanceType.Name}\" in assembly: \"{instanceType.Assembly.GetName().Name}\".");
        }

        static void DelegateNotRegisteredError () =>
            ClusterDebug.LogError($"Unable to call instance method, the delegate has not been registered!");

        /// <summary>
        /// Invoke an RPC immediately.
        /// </summary>
        /// <param name="assemblyIndex">The assembly that the RPC is declared in.</param>
        /// <param name="rpcId">The RPC ID.</param>
        /// <param name="pipeId">The pipe ID that represents the instance delcaring this RPC></param>
        /// <param name="parametersPayloadSize">The total byte count of all the RPC argument values.</param>
        /// <param name="rpcsBufferPosition">The position where the RPC arguments start in the RPC buffer.</param>
        /// <returns>Whether we successfully invoked the RPC or not.</returns>
        internal static bool TryCallInstance (
            ushort assemblyIndex,
            ushort rpcId, 
            ushort pipeId, 
            buint parametersPayloadSize, 
            ref buint rpcsBufferPosition)
        {
            bool success = false;
            // Making sure nothing is broken, as this library matures we could probably wrap these in a validation scripting define.
            if (m_ImplementationInstances == null || assemblyIndex >= m_ImplementationInstances.Count)
            {
                DelegateNotRegisteredError();
                goto done;
            }
            
            if (!RPCRegistry.RPCIdToRPCHash(rpcId, out var rpcHash))
                goto done;

            // Call the IL injected method that implements the byte conversion of the RPC's arguments then subsequently invoke the RPC.
            try
            {
                success = m_ImplementationInstances[assemblyIndex].onTryCallInstance(
                    rpcHash,
                    pipeId,
                    parametersPayloadSize,
                    rpcsBufferPosition);
            }

            catch (System.Exception exception)
            {
                ClusterDebug.LogError($"The following exception occurred while attempting to execute a instance RPC: (Assembly Index: {assemblyIndex}, RPC ID: {rpcId}, Pipe ID: {pipeId}, RPC ExecutionStage: {RPCExecutionStage.ImmediatelyOnArrival}, RPC Buffer Starting Position: {rpcsBufferPosition}, Parameter Payload Size: {parametersPayloadSize})");
                ClusterDebug.LogException(exception);
                goto done;
            }

            done:

            if (!success)
            {
                ClusterDebug.LogError($"(Frame: {(ClusterDisplayState.TryGetFrameId(out var frame) ? frame : 0)}): Unable to call execute instance RPC: {rpcId}");
            }

            rpcsBufferPosition += parametersPayloadSize;
            return success;
        }

        internal static bool TryCallStatic(
            ushort assemblyIndex,
            ushort rpcId,
            buint parametersPayloadSize,
            ref buint rpcsBufferPosition)
        {
            bool success = false;
            if (m_ImplementationInstances == null || assemblyIndex >= m_ImplementationInstances.Count)
            {
                DelegateNotRegisteredError();
                goto done;
            }
            
            if (!RPCRegistry.RPCIdToRPCHash(rpcId, out var rpcHash))
                goto done;

            try
            {
                success = m_ImplementationInstances[assemblyIndex].onTryCallStatic(
                    rpcHash,
                    parametersPayloadSize,
                    rpcsBufferPosition);
            }

            catch (System.Exception exception)
            {
                ClusterDebug.LogError($"The following exception occurred while attempting to execute a static RPC: (Assembly Index: {assemblyIndex}, RPC ID: {rpcId}, RPC ExecutionStage: {RPCExecutionStage.ImmediatelyOnArrival}, RPC Buffer Starting Position: {rpcsBufferPosition}, Parameter Payload Size: {parametersPayloadSize})");
                ClusterDebug.LogException(exception);
                goto done;
            }

            done:

            if (!success)
            {
                ClusterDebug.LogError($"(Frame: {(ClusterDisplayState.TryGetFrameId(out var frame) ? frame : 0)}): Unable to call execute static RPC: {rpcId}");
            }

            rpcsBufferPosition += parametersPayloadSize;
            return success;
        }

        static void LogPreExceptionMessageForQueuedRPC (QueuedRPCCall queuedRPCCall) =>
            ClusterDebug.LogError($"The following exception occurred while attempting to execute instance RPC: (Assembly Index: {queuedRPCCall.rpcRequest.assemblyIndex}, RPC ID: {queuedRPCCall.rpcRequest.rpcId}, Pipe ID: {queuedRPCCall.rpcRequest.pipeId}, RPC ExecutionStage: {queuedRPCCall.rpcRequest.rpcExecutionStage}, RPC Buffer Starting Position: {queuedRPCCall.rpcsBufferParametersStartPosition}, Parameter Payload Size: {queuedRPCCall.rpcRequest.parametersPayloadSize})");

        static void LogCall(QueuedRPCCall queuedRPCCall) =>
            ClusterDebug.Log($"Calling method: (RPC ID: {queuedRPCCall.rpcRequest.rpcId}, Assembly Index: {queuedRPCCall.rpcRequest.assemblyIndex}");

        static void LogMissingMethod (QueuedRPCCall queuedRPCCall) =>
            ClusterDebug.LogError($"(Frame: {(ClusterDisplayState.TryGetFrameId(out var frame) ? frame : 0)}): No method available to execute RPC: {queuedRPCCall.rpcRequest.rpcId}");

        static void ExecuteQueued (QueuedRPCCall queuedRPCCall)
        {
            if (m_ImplementationInstances[queuedRPCCall.rpcRequest.assemblyIndex].onTryCallQueuedInstance == null)
                return;

            if (!RPCRegistry.RPCIdToRPCHash(queuedRPCCall.rpcRequest.rpcId, out var rpcHash))
                return;

            if (queuedRPCCall.rpcRequest.rpcId == 52)
                Debug.Log("TEST");
            
            LogCall(queuedRPCCall);

            try
            {
                bool result = m_ImplementationInstances[queuedRPCCall.rpcRequest.assemblyIndex].onTryCallQueuedInstance(
                    rpcHash,
                    queuedRPCCall.rpcRequest.pipeId,
                    queuedRPCCall.rpcRequest.parametersPayloadSize,
                    queuedRPCCall.rpcsBufferParametersStartPosition);

                if (!result)
                {
                    LogMissingMethod(queuedRPCCall);
                }
            }

            catch (System.Exception exception)
            {
                LogPreExceptionMessageForQueuedRPC(queuedRPCCall);
                ClusterDebug.LogException(exception);
            }
        }

        internal static void BeforeFixedUpdate()
        {
            if (beforeFixedUpdateRPCQueue.Count == 0)
                return;

            if (m_ImplementationInstances.Count == 0)
            {
                DelegateNotRegisteredError();
                beforeFixedUpdateRPCQueue.Clear();
                return;
            }

            while (beforeFixedUpdateRPCQueue.Count > 0)
            {
                ExecuteQueued(beforeFixedUpdateRPCQueue.Dequeue());
            }
        }

        public static void AfterFixedUpdate()
        {
            if (afterFixedUpdateRPCQueue.Count == 0)
                return;

            if (m_ImplementationInstances.Count == 0)
            {
                DelegateNotRegisteredError();
                afterFixedUpdateRPCQueue.Clear();
                return;
            }

            while (afterFixedUpdateRPCQueue.Count > 0)
            {
                ExecuteQueued(afterFixedUpdateRPCQueue.Dequeue());
            }
        }

        public static void BeforeUpdate()
        {
            if (beforeUpdateRPCQueue.Count == 0)
                return;

            if (m_ImplementationInstances.Count == 0)
            {
                DelegateNotRegisteredError();
                beforeUpdateRPCQueue.Clear();
                return;
            }

            while (beforeUpdateRPCQueue.Count > 0)
            {
                ExecuteQueued(beforeUpdateRPCQueue.Dequeue());
            }
        }

        public static void AfterUpdate()
        {
            if (afterUpdateRPCQueue.Count == 0)
                return;

            if (m_ImplementationInstances.Count == 0)
            {
                DelegateNotRegisteredError();
                afterUpdateRPCQueue.Clear();
                return;
            }

            while (afterUpdateRPCQueue.Count > 0)
            {
                ExecuteQueued(afterUpdateRPCQueue.Dequeue());
            }
        }

        public static void BeforeLateUpdate()
        {
            if (beforeLateUpdateRPCQueue.Count == 0)
                return;

            if (m_ImplementationInstances.Count == 0)
            {
                DelegateNotRegisteredError();
                beforeLateUpdateRPCQueue.Clear();
                return;
            }

            while (beforeLateUpdateRPCQueue.Count > 0)
            {
                ExecuteQueued(beforeLateUpdateRPCQueue.Dequeue());
            }
        }

        public static void AfterLateUpdate()
        {
            while (afterLateUpdateRPCQueue.Count == 0)
                return;

            if (m_ImplementationInstances.Count == 0)
            {
                DelegateNotRegisteredError();
                afterLateUpdateRPCQueue.Clear();
                return;
            }

            while (afterLateUpdateRPCQueue.Count > 0)
            {
                ExecuteQueued(afterLateUpdateRPCQueue.Dequeue());
            }
        }

        static void LogQueuedRPC (
           ushort  pipeId,
            ushort rpcId,
            buint parametersPayloadSize,
            buint rpcBufferParametersStartPosition) =>
            ClusterDebug.Log($"Queued RPC with pipe ID: {pipeId}, RPC ID: {rpcId}, Parameter Payload Size: {parametersPayloadSize}, RPC Buffer Parameters Start Position: {rpcBufferParametersStartPosition}");

        [QueueBeforeFixedUpdateRPCMarker]
        internal static void QueueBeforeFixedUpdateRPC (
            ref RPCRequest parsedRPC,
            buint rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            beforeFixedUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcRequest = parsedRPC,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueAfterFixedUpdateRPCMarker]
        internal static void QueueAfterFixedUpdateRPC (
            ref RPCRequest rpcRequest,
            buint rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            afterFixedUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcRequest = rpcRequest,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueBeforeUpdateRPCMarker]
        internal static void QueueBeforeUpdateRPC (
            ref RPCRequest rpcRequest,
            buint rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            beforeUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcRequest = rpcRequest,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueAfterUpdateRPCMarker]
        internal static void QueueAfterUpdateRPC (
            ref RPCRequest rpcRequest,
            buint rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            afterUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcRequest = rpcRequest,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueBeforeLateUpdateRPCMarker]
        internal static void QueueBeforeLateUpdateRPC (
            ref RPCRequest rpcRequest,
            buint rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            beforeLateUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcRequest = rpcRequest,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueAfterLateUpdateRPCMarker]
        internal static void QueueAfterLateUpdateRPC (
            ref RPCRequest rpcRequest,
            buint rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            afterLateUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcRequest = rpcRequest,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }
    }
}
