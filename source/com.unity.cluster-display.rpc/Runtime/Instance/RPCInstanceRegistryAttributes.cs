using System;

namespace Unity.ClusterDisplay
{
    public abstract partial class RPCInterfaceRegistry
    {
        public class RPCInterfaceRegistryConstuctorMarker : Attribute {}

        public class OnTryCallInstanceImplementationAttribute : Attribute {}
        public class OnTryCallStaticImplementationAttribute : Attribute {}

        public class OnTryCallInstanceDelegateAttribute : Attribute {}
        public class OnTryCallStaticDelegateAttribute : Attribute {}

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

        public class OnTryCallQueuedInstanceImplementationAttribute : Attribute { };
        public class OnTryCallQueuedInstanceDelegateAttribute : Attribute { };
        public class ExecuteQueuedRPCFieldDelegateMarker : Attribute { };
    }
}
