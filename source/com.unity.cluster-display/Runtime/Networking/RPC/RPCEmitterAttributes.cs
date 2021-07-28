using System;

namespace Unity.ClusterDisplay.RPC
{
    public static partial class RPCEmitter
    {
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class RPCCallMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class StaticRPCCallMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class AppendRPCStringParameterValueMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class AppendRPCArrayParameterValueMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class AppendRPCNativeArrayParameterValueMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class AppendRPCValueTypeParameterValueMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class ParseStringMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method)]
        public class ParseArrayMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method)]
        public class ParseStructureMarker : Attribute {}

    }
}
