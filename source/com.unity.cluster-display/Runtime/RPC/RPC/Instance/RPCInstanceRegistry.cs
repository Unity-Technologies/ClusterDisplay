using System;
using System.Collections.Generic;
using System.Linq;
using Unity.ClusterDisplay.RPC;
using UnityEngine;

using buint = System.UInt32;

namespace Unity.ClusterDisplay
{
    public abstract partial class RPCInterfaceRegistry
    {
        protected struct QueuedRPCCall
        {
            internal RPCRequest rpcRequest;
            internal buint rpcsBufferParametersStartPosition;
        }

         [OnTryCallDelegateMarker] protected delegate bool ExecuteRPCDelegate(
            ushort rpcId, 
            ushort pipeId, 
            buint parametersPayloadSize, 
            ref buint rpcBufferParameterPosition);

        [OnTryStaticCallDelegateMarker] protected delegate bool ExecuteStaticRPCDelegate(
            ushort rpcId, 
            buint parametersPayloadSize, 
            ref buint rpcBufferParameterPosition);

        [ExecuteQueuedRPCDelegateMarker] protected delegate void ExecuteQueuedRPCDelegate(
            ushort rpcId, 
            ushort pipeId, 
            buint parametersPayloadSize, 
            buint rpcBufferParameterPosition);

        private static readonly Queue<QueuedRPCCall> beforeFixedUpdateRPCQueue = new Queue<QueuedRPCCall>();
        private static readonly Queue<QueuedRPCCall> afterFixedUpdateRPCQueue = new Queue<QueuedRPCCall>();

        private static readonly Queue<QueuedRPCCall> beforeUpdateRPCQueue = new Queue<QueuedRPCCall>();
        private static readonly Queue<QueuedRPCCall> afterUpdateRPCQueue = new Queue<QueuedRPCCall>();

        private static readonly Queue<QueuedRPCCall> beforeLateUpdateRPCQueue = new Queue<QueuedRPCCall>();
        private static readonly Queue<QueuedRPCCall> afterLateUpdateRPCQueue = new Queue<QueuedRPCCall>();

        protected readonly static List<ExecuteRPCDelegate> m_OnTryCallInstanceDelegate = new List<ExecuteRPCDelegate>();
        protected readonly static List<ExecuteStaticRPCDelegate> m_OnTryStaticCallInstanceDelegate = new List<ExecuteStaticRPCDelegate>();
        protected readonly static List<ExecuteQueuedRPCDelegate> m_ExecuteQueuedRPCDelegate = new List<ExecuteQueuedRPCDelegate>();

        private readonly static List<RPCInterfaceRegistry> m_ImplementationInstances = new List<RPCInterfaceRegistry>();

        public static bool HasImplementationInAssembly (System.Reflection.Assembly assembly) => assembly.GetTypes().Any(type => type.Name == "RPCIL");

        public static bool TryCreateImplementationInstance (System.Reflection.Assembly assembly, out ushort assemblyIndex)
        {
            var rpcILType = assembly.GetTypes().FirstOrDefault(type => type.Name == "RPCIL");
            if (rpcILType == null)
            {
                // Debug.LogError($"Unable to create instance of: \"RPCIL\", it does not exist in the assembly: \"{assembly.GetName().Name}\", please verify that you've registered this assembly with RPCRegistry.");
                assemblyIndex = 0;
                return false;
            }

            RPCInterfaceRegistry instance = m_ImplementationInstances.FirstOrDefault(i => i.GetType().Assembly == assembly);
            if (instance == null)
            {
                instance = Activator.CreateInstance(rpcILType) as RPCInterfaceRegistry;
                Debug.Log($"Created instance of type: \"{rpcILType.FullName}\" in assembly: \"{assembly.GetName().Name}\".");
            }

            assemblyIndex = (ushort)m_ImplementationInstances.IndexOf(instance);
            return true;
        }

        [RPCInterfaceRegistryConstuctorMarker] protected RPCInterfaceRegistry (
            ExecuteRPCDelegate OnTryCallInstance,
            ExecuteStaticRPCDelegate OnTryStaticCallInstance,
            ExecuteQueuedRPCDelegate executeQueuedRPC)
        {
            m_ImplementationInstances.Add(this);
            m_OnTryCallInstanceDelegate.Add(OnTryCallInstance);
            m_OnTryStaticCallInstanceDelegate.Add(OnTryStaticCallInstance);
            m_ExecuteQueuedRPCDelegate.Add(executeQueuedRPC);
        }

        private static void DelegateNotRegisteredError ()
        {
            Debug.LogError($"Unable to call instance method, the delegate has not been registered!");
        }

        public static bool TryCallInstance (
            ushort assemblyIndex,
            ushort rpcId, 
            ushort pipeId, 
            buint parametersPayloadSize, 
            ref buint rpcsBufferPosition)
        {
            if (m_OnTryCallInstanceDelegate == null || m_OnTryCallInstanceDelegate[assemblyIndex] == null)
            {
                DelegateNotRegisteredError();
                return false;
            }

            return m_OnTryCallInstanceDelegate[assemblyIndex](
                rpcId, 
                pipeId, 
                parametersPayloadSize, 
                ref rpcsBufferPosition);
        }

        public static bool TryCallStatic(
            ushort assemblyIndex,
            ushort rpcId,
            buint parametersPayloadSize,
            ref buint rpcsBufferPosition)
        {
            if (m_OnTryStaticCallInstanceDelegate == null || m_OnTryStaticCallInstanceDelegate[assemblyIndex] == null)
            {
                DelegateNotRegisteredError();
                return false;
            }

            return m_OnTryStaticCallInstanceDelegate[assemblyIndex](
                rpcId, 
                parametersPayloadSize, 
                ref rpcsBufferPosition);
        }

        public static void BeforeFixedUpdate()
        {
            if (beforeFixedUpdateRPCQueue.Count == 0)
                return;

            if (m_ExecuteQueuedRPCDelegate == null)
            {
                DelegateNotRegisteredError();
                beforeFixedUpdateRPCQueue.Clear();
                return;
            }

            while (beforeFixedUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = beforeFixedUpdateRPCQueue.Dequeue();
                if (m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex] == null)
                    continue;

                m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex](
                    queuedRPCCall.rpcRequest.rpcId, 
                    queuedRPCCall.rpcRequest.pipeId, 
                    queuedRPCCall.rpcRequest.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public static void AfterFixedUpdate()
        {
            if (afterFixedUpdateRPCQueue.Count == 0)
                return;

            if (m_ExecuteQueuedRPCDelegate == null)
            {
                DelegateNotRegisteredError();
                afterFixedUpdateRPCQueue.Clear();
                return;
            }

            while (afterFixedUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = afterFixedUpdateRPCQueue.Dequeue();
                if (m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex] == null)
                    continue;

                m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex](
                    queuedRPCCall.rpcRequest.rpcId, 
                    queuedRPCCall.rpcRequest.pipeId, 
                    queuedRPCCall.rpcRequest.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public static void BeforeUpdate()
        {
            if (beforeUpdateRPCQueue.Count == 0)
                return;

            if (m_ExecuteQueuedRPCDelegate == null)
            {
                DelegateNotRegisteredError();
                beforeUpdateRPCQueue.Clear();
                return;
            }

            while (beforeUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = beforeUpdateRPCQueue.Dequeue();
                if (m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex] == null)
                    continue;

                m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex](
                    queuedRPCCall.rpcRequest.rpcId, 
                    queuedRPCCall.rpcRequest.pipeId, 
                    queuedRPCCall.rpcRequest.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public static void AfterUpdate()
        {
            if (afterUpdateRPCQueue.Count == 0)
                return;

            if (m_ExecuteQueuedRPCDelegate == null)
            {
                DelegateNotRegisteredError();
                afterUpdateRPCQueue.Clear();
                return;
            }

            while (afterUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = afterUpdateRPCQueue.Dequeue();
                if (m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex] == null)
                    continue;

                m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex](
                    queuedRPCCall.rpcRequest.rpcId, 
                    queuedRPCCall.rpcRequest.pipeId, 
                    queuedRPCCall.rpcRequest.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public static void BeforeLateUpdate()
        {
            if (beforeLateUpdateRPCQueue.Count == 0)
                return;

            if (m_ExecuteQueuedRPCDelegate == null)
            {
                DelegateNotRegisteredError();
                beforeLateUpdateRPCQueue.Clear();
                return;
            }

            while (beforeLateUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = beforeLateUpdateRPCQueue.Dequeue();
                if (m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex] == null)
                    continue;

                m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex](
                    queuedRPCCall.rpcRequest.rpcId, 
                    queuedRPCCall.rpcRequest.pipeId, 
                    queuedRPCCall.rpcRequest.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public static void AfterLateUpdate()
        {
            while (afterLateUpdateRPCQueue.Count == 0)
                return;

            if (m_ExecuteQueuedRPCDelegate == null)
            {
                DelegateNotRegisteredError();
                afterLateUpdateRPCQueue.Clear();
                return;
            }

            while (afterLateUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = afterLateUpdateRPCQueue.Dequeue();
                if (m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex] == null)
                    continue;

                m_ExecuteQueuedRPCDelegate[queuedRPCCall.rpcRequest.assemblyIndex](
                    queuedRPCCall.rpcRequest.rpcId, 
                    queuedRPCCall.rpcRequest.pipeId, 
                    queuedRPCCall.rpcRequest.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        private static void LogQueuedRPC (
           ushort  pipeId,
            ushort rpcId,
            buint parametersPayloadSize,
            buint rpcBufferParametersStartPosition) =>
            Debug.Log($"Queued RPC with pipe ID: {pipeId}, RPC ID: {rpcId}, Parameter Payload Size: {parametersPayloadSize}, RPC Buffer Parameters Start Position: {rpcBufferParametersStartPosition}");

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
