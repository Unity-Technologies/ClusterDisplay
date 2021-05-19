using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public abstract class RPCInterfaceRegistry : SingletonScriptableObject<RPCInterfaceRegistry>
    {
        public class OnTryCallMarker : Attribute {}

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

        private static readonly Queue<QueuedRPCCall> beforeFixedUpdateRPCQueue = new Queue<QueuedRPCCall>();
        private static readonly Queue<QueuedRPCCall> afterFixedUpdateRPCQueue = new Queue<QueuedRPCCall>();

        private static readonly Queue<QueuedRPCCall> beforeUpdateRPCQueue = new Queue<QueuedRPCCall>();
        private static readonly Queue<QueuedRPCCall> afterUpdateRPCQueue = new Queue<QueuedRPCCall>();

        private static readonly Queue<QueuedRPCCall> beforeLateUpdateRPCQueue = new Queue<QueuedRPCCall>();
        private static readonly Queue<QueuedRPCCall> afterLateUpdateRPCQueue = new Queue<QueuedRPCCall>();

        protected abstract bool OnTryCallInstance(
            ObjectRegistry objectRegistry,
            ushort rpcId, 
            ushort pipeId, 
            ushort parametersPayloadSize, 
            ref ushort rpcsBufferPosition);

        public static bool TryCallInstance (
            ObjectRegistry objectRegistry,
            ushort rpcId, 
            ushort pipeId, 
            ushort parametersPayloadSize, 
            ref ushort rpcsBufferPosition)
        {
            if (!TryGetInstance(out var instanceRegistry))
                return false;

            return instanceRegistry.OnTryCallInstance(
                objectRegistry,
                rpcId, 
                pipeId, 
                parametersPayloadSize, 
                ref rpcsBufferPosition);
        }

        public static bool TryCallStatic (
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ref ushort rpcsBufferPosition)
        {
            throw new System.NotImplementedException();
        }

        protected abstract void ExecuteRPCBeforeFixedUpdate(
            ObjectRegistry objectRegistry,
            ushort rpcId, 
            ushort pipeId, 
            ushort parametersPayloadSize, 
            ushort rpcBufferParameterPosition);

        protected abstract void ExecuteRPCAfterFixedUpdate(
            ObjectRegistry objectRegistry,
            ushort rpcId, 
            ushort pipeId, 
            ushort parametersPayloadSize, 
            ushort rpcBufferParameterPosition);

        protected abstract void ExecuteRPCBeforeUpdate(
            ObjectRegistry objectRegistry,
            ushort rpcId, 
            ushort pipeId, 
            ushort parametersPayloadSize, 
            ushort rpcBufferParameterPosition);

        protected abstract void ExecuteRPCAfterUpdate(
            ObjectRegistry objectRegistry,
            ushort rpcId, 
            ushort pipeId, 
            ushort parametersPayloadSize, 
            ushort rpcBufferParameterPosition);

        protected abstract void ExecuteRPCBeforeLateUpdate(
            ObjectRegistry objectRegistry,
            ushort rpcId, 
            ushort pipeId, 
            ushort parametersPayloadSize, 
            ushort rpcBufferParameterPosition);

        protected abstract void ExecuteRPCAfterLateUpdate(
            ObjectRegistry objectRegistry,
            ushort rpcId, 
            ushort pipeId, 
            ushort parametersPayloadSize, 
            ushort rpcBufferParameterPosition);

        public void BeforeFixedUpdate()
        {
            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                return;

            while (beforeFixedUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = beforeFixedUpdateRPCQueue.Dequeue();
                ExecuteRPCBeforeFixedUpdate(
                    objectRegistry,
                    queuedRPCCall.rpcId, 
                    queuedRPCCall.pipeId, 
                    queuedRPCCall.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public void AfterFixedUpdate()
        {
            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                return;

            while (afterFixedUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = afterFixedUpdateRPCQueue.Dequeue();
                ExecuteRPCBeforeFixedUpdate(
                    objectRegistry,
                    queuedRPCCall.rpcId, 
                    queuedRPCCall.pipeId, 
                    queuedRPCCall.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public void BeforeUpdate()
        {
            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                return;

            while (beforeUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = beforeUpdateRPCQueue.Dequeue();
                ExecuteRPCBeforeFixedUpdate(
                    objectRegistry,
                    queuedRPCCall.rpcId, 
                    queuedRPCCall.pipeId, 
                    queuedRPCCall.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public void AfterUpdate()
        {
            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                return;

            while (afterUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = afterUpdateRPCQueue.Dequeue();
                ExecuteRPCBeforeFixedUpdate(
                    objectRegistry,
                    queuedRPCCall.rpcId, 
                    queuedRPCCall.pipeId, 
                    queuedRPCCall.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public void BeforeLateUpdate()
        {
            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                return;

            while (beforeLateUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = beforeLateUpdateRPCQueue.Dequeue();
                ExecuteRPCBeforeFixedUpdate(
                    objectRegistry,
                    queuedRPCCall.rpcId, 
                    queuedRPCCall.pipeId, 
                    queuedRPCCall.parametersPayloadSize, 
                    queuedRPCCall.rpcsBufferParametersStartPosition);
            }
        }

        public void AfterLateUpdate()
        {
            if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
                return;

            while (afterLateUpdateRPCQueue.Count > 0)
            {
                var queuedRPCCall = afterLateUpdateRPCQueue.Dequeue();
                ExecuteRPCBeforeFixedUpdate(
                    objectRegistry,
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
        protected static void QueueBeforeFixedUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            beforeFixedUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueAfterFixedUpdateRPCMarker]
        protected static void QueueAfterFixedUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            afterFixedUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueBeforeUpdateRPCMarker]
        protected static void QueueBeforeUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            beforeUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueAfterUpdateRPCMarker]
        protected static void QueueAfterUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            afterUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueBeforeLateUpdateRPCMarker]
        protected static void QueueBeforeLateUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            beforeUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }

        [QueueAfterLateUpdateRPCMarker]
        protected static void QueueAfterLateUpdateRPC (
            ushort pipeId, 
            ushort rpcId, 
            ushort parametersPayloadSize, 
            ushort rpcsBufferParametersStartPosition)
        {
            LogQueuedRPC(pipeId, rpcId, parametersPayloadSize, rpcsBufferParametersStartPosition);
            afterUpdateRPCQueue.Enqueue(new QueuedRPCCall
            {
                rpcId = rpcId,
                pipeId = pipeId,
                parametersPayloadSize = parametersPayloadSize,
                rpcsBufferParametersStartPosition = rpcsBufferParametersStartPosition
            });
        }
    }
}
