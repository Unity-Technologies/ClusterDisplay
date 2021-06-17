using System.Reflection;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public abstract class RPCInterfaceRegistry
    {
        private const string RPCILFullName = "Unity.ClusterDisplay.Networking.RPCIL";
        private static RPCInterfaceRegistry instance;
        static RPCInterfaceRegistry ()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (type.FullName != RPCILFullName)
                        continue;

                    instance = (RPCInterfaceRegistry)assembly.CreateInstance(type.FullName);
                    return;
                }
            }
        }

        public class OnTryCallMarker : Attribute {}
        public class OnTryStaticCallMarker : Attribute {}

        public class ObjectRegistryMarker : Attribute {}
        public class PipeIdMarker : Attribute {}
        public class RPCIdMarker : Attribute {}
        public class ParametersPayloadSizeMarker : Attribute {}
        public class RPCBufferPositionMarker : Attribute {}

        public class QueueBeforeFixedUpdateRPCMarker : Attribute {}
        public class QueueAfterFixedUpdateRPCMarker : Attribute {}
        public class QueueBeforeUpdateRPCMarker : Attribute {}
        public class QueueAfterUpdateRPCMarker : Attribute {}
        public class QueueBeforeLateUpdateRPCMarker : Attribute {}
        public class QueueAfterLateUpdateRPCMarker : Attribute {}

        public class ExecuteRPCBeforeFixedUpdateMarker : Attribute { };
        public class ExecuteRPCAfterFixedUpdateMarker : Attribute {};
        public class ExecuteRPCBeforeUpdateMarker : Attribute {};
        public class ExecuteRPCAfterUpdateMarker : Attribute {};
        public class ExecuteRPCBeforeLateUpdateMarker : Attribute {};
        public class ExecuteRPCAfterLateUpdateMarker : Attribute {};

        protected struct QueuedRPCCall
        {
            public ushort rpcId;
            public ushort pipeId;
            public ushort parametersPayloadSize;
            public ushort rpcsBufferParametersStartPosition;
        }

        protected delegate bool ExecuteStaticRPCDelegate(
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ref ushort rpcBufferParameterPosition);

        protected delegate bool ExecuteRPCDelegate(
            ushort rpcId, 
            ushort pipeId, 
            ushort parametersPayloadSize, 
            ref ushort rpcBufferParameterPosition);

        protected delegate void ExecuteQueuedRPCDelegate(
            ushort rpcId, 
            ushort pipeId, 
            ushort parametersPayloadSize, 
            ushort rpcBufferParameterPosition);

        private static readonly Queue<QueuedRPCCall> beforeFixedUpdateRPCQueue = new Queue<QueuedRPCCall>();
        private static readonly Queue<QueuedRPCCall> afterFixedUpdateRPCQueue = new Queue<QueuedRPCCall>();

        private static readonly Queue<QueuedRPCCall> beforeUpdateRPCQueue = new Queue<QueuedRPCCall>();
        private static readonly Queue<QueuedRPCCall> afterUpdateRPCQueue = new Queue<QueuedRPCCall>();

        private static readonly Queue<QueuedRPCCall> beforeLateUpdateRPCQueue = new Queue<QueuedRPCCall>();
        private static readonly Queue<QueuedRPCCall> afterLateUpdateRPCQueue = new Queue<QueuedRPCCall>();

        protected static ExecuteRPCDelegate OnTryCallInstanceDelegate;
        protected static ExecuteStaticRPCDelegate OnTryStaticCallInstanceDelegate;

        protected static ExecuteQueuedRPCDelegate ExecuteRPCBeforeFixedUpdateDelegate;
        protected static ExecuteQueuedRPCDelegate ExecuteRPCAfterFixedUpdateDelegate;
        protected static ExecuteQueuedRPCDelegate ExecuteRPCBeforeUpdateDelegate;
        protected static ExecuteQueuedRPCDelegate ExecuteRPCAfterUpdateDelegate;
        protected static ExecuteQueuedRPCDelegate ExecuteRPCBeforeLateUpdateDelegate;
        protected static ExecuteQueuedRPCDelegate ExecuteRPCAfterLateUpdateDelegate;

        private static void DelegateNotRegisteredError ()
        {
            Debug.LogError($"Unable to call instance method, the delegate has not been registered!");
        }

        public static bool TryCallInstance (
            ushort rpcId, 
            ushort pipeId, 
            ushort parametersPayloadSize, 
            ref ushort rpcsBufferPosition)
        {
            if (OnTryCallInstanceDelegate == null)
            {
                DelegateNotRegisteredError();
                return false;
            }

            return OnTryCallInstanceDelegate(
                rpcId, 
                pipeId, 
                parametersPayloadSize, 
                ref rpcsBufferPosition);
        }

        public static bool TryCallStatic(
            ushort rpcId,
            ushort parametersPayloadSize,
            ref ushort rpcsBufferPosition)
        {
            if (OnTryStaticCallInstanceDelegate == null)
            {
                DelegateNotRegisteredError();
                return false;
            }

            return OnTryStaticCallInstanceDelegate(
                rpcId, 
                parametersPayloadSize, 
                ref rpcsBufferPosition);
        }

        public static void BeforeFixedUpdate()
        {
            if (beforeFixedUpdateRPCQueue.Count == 0)
                return;

            if (ExecuteRPCBeforeFixedUpdateDelegate == null)
            {
                DelegateNotRegisteredError();
                beforeFixedUpdateRPCQueue.Clear();
                return;
            }

            while (beforeFixedUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = beforeFixedUpdateRPCQueue.Dequeue();
                ExecuteRPCBeforeFixedUpdateDelegate(
                    queuedRPCCall.rpcId, 
                    queuedRPCCall.pipeId, 
                    queuedRPCCall.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public static void AfterFixedUpdate()
        {
            if (afterFixedUpdateRPCQueue.Count == 0)
                return;

            if (ExecuteRPCAfterFixedUpdateDelegate == null)
            {
                DelegateNotRegisteredError();
                afterFixedUpdateRPCQueue.Clear();
                return;
            }

            while (afterFixedUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = afterFixedUpdateRPCQueue.Dequeue();
                ExecuteRPCAfterFixedUpdateDelegate(
                    queuedRPCCall.rpcId, 
                    queuedRPCCall.pipeId, 
                    queuedRPCCall.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public static void BeforeUpdate()
        {
            if (beforeUpdateRPCQueue.Count == 0)
                return;

            if (ExecuteRPCBeforeUpdateDelegate == null)
            {
                DelegateNotRegisteredError();
                beforeUpdateRPCQueue.Clear();
                return;
            }

            while (beforeUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = beforeUpdateRPCQueue.Dequeue();
                ExecuteRPCBeforeUpdateDelegate(
                    queuedRPCCall.rpcId, 
                    queuedRPCCall.pipeId, 
                    queuedRPCCall.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public static void AfterUpdate()
        {
            if (afterUpdateRPCQueue.Count == 0)
                return;

            if (ExecuteRPCAfterUpdateDelegate == null)
            {
                DelegateNotRegisteredError();
                afterUpdateRPCQueue.Clear();
                return;
            }

            while (afterUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = afterUpdateRPCQueue.Dequeue();
                ExecuteRPCAfterUpdateDelegate(
                    queuedRPCCall.rpcId, 
                    queuedRPCCall.pipeId, 
                    queuedRPCCall.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public static void BeforeLateUpdate()
        {
            if (beforeLateUpdateRPCQueue.Count == 0)
                return;

            if (ExecuteRPCBeforeLateUpdateDelegate == null)
            {
                DelegateNotRegisteredError();
                beforeLateUpdateRPCQueue.Clear();
                return;
            }

            while (beforeLateUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = beforeLateUpdateRPCQueue.Dequeue();
                ExecuteRPCBeforeLateUpdateDelegate(
                    queuedRPCCall.rpcId, 
                    queuedRPCCall.pipeId, 
                    queuedRPCCall.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public static void AfterLateUpdate()
        {
            while (afterLateUpdateRPCQueue.Count == 0)
                return;

            if (ExecuteRPCAfterLateUpdateDelegate == null)
            {
                DelegateNotRegisteredError();
                afterLateUpdateRPCQueue.Clear();
                return;
            }

            while (afterLateUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = afterLateUpdateRPCQueue.Dequeue();
                ExecuteRPCAfterLateUpdateDelegate(
                    queuedRPCCall.rpcId, 
                    queuedRPCCall.pipeId, 
                    queuedRPCCall.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        private static void LogQueuedRPC (
           ushort  pipeId,
            ushort rpcId,
            ushort parametersPayloadSize,
            ushort rpcBufferParametersStartPosition) =>
            Debug.Log($"Queued RPC with pipe ID: {pipeId}, RPC ID: {rpcId}, Parameter Payload Size: {parametersPayloadSize}, RPC Buffer Parameters Start Position: {rpcBufferParametersStartPosition}");

        [QueueBeforeFixedUpdateRPCMarker]
        public static void QueueBeforeFixedUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            beforeFixedUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueAfterFixedUpdateRPCMarker]
        public static void QueueAfterFixedUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            afterFixedUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueBeforeUpdateRPCMarker]
        public static void QueueBeforeUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            beforeUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueAfterUpdateRPCMarker]
        public static void QueueAfterUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            afterUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueBeforeLateUpdateRPCMarker]
        public static void QueueBeforeLateUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            beforeLateUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueAfterLateUpdateRPCMarker]
        public static void QueueAfterLateUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            // LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            afterLateUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }
    }
}
