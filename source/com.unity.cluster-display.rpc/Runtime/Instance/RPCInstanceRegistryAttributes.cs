using System;

namespace Unity.ClusterDisplay
{
    public abstract partial class RPCInterfaceRegistry
    {
        public class RPCInterfaceRegistryConstuctorMarker : Attribute {}

        public class OnTryCallMarker : Attribute {}
        public class OnTryStaticCallMarker : Attribute {}

        public class OnTryCallDelegateMarker : Attribute {}
        public class OnTryStaticCallDelegateMarker : Attribute {}

        public class OnTryCallDelegateFieldMarker : Attribute {}
        public class OnTryStaticCallDelegateFieldMarker : Attribute {}

        public class ObjectRegistryMarker : Attribute {}
        public class PipeIdMarker : Attribute {}
        public class RPCHashMarker : Attribute {}
        public class ParametersPayloadSizeMarker : Attribute {}
        public class RPCBufferPositionMarker : Attribute {}

        public class QueueBeforeFixedUpdateRPCMarker : Attribute {}
        public class QueueAfterFixedUpdateRPCMarker : Attribute {}
        public class QueueBeforeUpdateRPCMarker : Attribute {}
        public class QueueAfterUpdateRPCMarker : Attribute {}
        public class QueueBeforeLateUpdateRPCMarker : Attribute {}
        public class QueueAfterLateUpdateRPCMarker : Attribute {}

        public class ExecuteQueuedRPC : Attribute { };
        public class ExecuteQueuedRPCDelegateMarker : Attribute { };
        public class ExecuteQueuedRPCFieldDelegateMarker : Attribute { };
    }
}
