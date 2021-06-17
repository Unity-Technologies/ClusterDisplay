using UnityEngine;

namespace Unity.ClusterDisplay.Networking
{
    public class RPCIL : RPCInterfaceRegistry
    {
        public RPCIL ()
        {
            OnTryCallInstanceDelegate += OnTryCallInstance;
            OnTryStaticCallInstanceDelegate += OnTryStaticCallInstance;

            ExecuteRPCBeforeFixedUpdateDelegate += ExecuteRPCBeforeFixedUpdate;
            ExecuteRPCAfterFixedUpdateDelegate += ExecuteRPCAfterFixedUpdate;

            ExecuteRPCBeforeUpdateDelegate += ExecuteRPCBeforeUpdate;
            ExecuteRPCAfterUpdateDelegate += ExecuteRPCAfterUpdate;

            ExecuteRPCBeforeLateUpdateDelegate += ExecuteRPCBeforeLateUpdate;
            ExecuteRPCAfterLateUpdateDelegate += ExecuteRPCAfterLateUpdate;
        }

        [OnTryCallMarker]
        private static bool OnTryCallInstance(
            [RPCIdMarker] ushort rpcId,
            [PipeIdMarker] ushort pipeId,
            [ParametersPayloadSizeMarker] ushort parametersPayloadSize,
            [RPCBufferPositionMarker] ref ushort rpcBufferParameterPosition) => false;

        [OnTryStaticCallMarker]
        private static bool OnTryStaticCallInstance(
            [RPCIdMarker] ushort rpcId, 
            [ParametersPayloadSizeMarker] ushort parametersPayloadSize, 
            [RPCBufferPositionMarker] ref ushort rpcsBufferPosition) => false;

        [ExecuteRPCBeforeFixedUpdateMarker]
        private static void ExecuteRPCBeforeFixedUpdate(
            [RPCIdMarker] ushort rpcId, 
            [PipeIdMarker] ushort pipeId, 
            [ParametersPayloadSizeMarker] ushort parametersPayloadSize, 
            [RPCBufferPositionMarker] ushort rpcBufferParameterPosition) {}

        [ExecuteRPCAfterFixedUpdateMarker]
        private static void ExecuteRPCAfterFixedUpdate(
            [RPCIdMarker] ushort rpcId, 
            [PipeIdMarker] ushort pipeId, 
            [ParametersPayloadSizeMarker] ushort parametersPayloadSize, 
            [RPCBufferPositionMarker] ushort rpcBufferParameterPosition) {}

        [ExecuteRPCBeforeUpdateMarker]
        private static void ExecuteRPCBeforeUpdate(
            [RPCIdMarker] ushort rpcId, 
            [PipeIdMarker] ushort pipeId, 
            [ParametersPayloadSizeMarker] ushort parametersPayloadSize, 
            [RPCBufferPositionMarker] ushort rpcBufferParameterPosition) {}

        [ExecuteRPCAfterUpdateMarker]
        private static void ExecuteRPCAfterUpdate(
            [RPCIdMarker] ushort rpcId, 
            [PipeIdMarker] ushort pipeId, 
            [ParametersPayloadSizeMarker] ushort parametersPayloadSize, 
            [RPCBufferPositionMarker] ushort rpcBufferParameterPosition) {}

        [ExecuteRPCBeforeLateUpdateMarker]
        private static void ExecuteRPCBeforeLateUpdate(
            [RPCIdMarker] ushort rpcId, 
            [PipeIdMarker] ushort pipeId, 
            [ParametersPayloadSizeMarker] ushort parametersPayloadSize, 
            [RPCBufferPositionMarker] ushort rpcBufferParameterPosition) {}

        [ExecuteRPCAfterLateUpdateMarker]
        private static void ExecuteRPCAfterLateUpdate(
            [RPCIdMarker] ushort rpcId, 
            [PipeIdMarker] ushort pipeId, 
            [ParametersPayloadSizeMarker] ushort parametersPayloadSize, 
            [RPCBufferPositionMarker] ushort rpcBufferParameterPosition) {}
    }

    public partial class TransformReflector : ComponentReflector<Transform>, IRPCStatus
    {
        public Vector3 position
        {
            get => transform.position;
            [RPC] set => transform.position = value;
        }

        public Quaternion rotation
        {
            get => transform.rotation;
            [RPC] set => transform.rotation = value;
        }

        public Vector3 scale
        {
            get => transform.localScale;
            [RPC] set => transform.localScale = value;
        }
    }
}

