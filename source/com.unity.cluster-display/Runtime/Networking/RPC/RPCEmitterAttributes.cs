using System;

namespace Unity.ClusterDisplay
{
    public static partial class RPCEmitter
    {
        [AttributeUsage(AttributeTargets.Method)]
        public class RPCCallMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method)]
        public class StaticRPCCallMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method)]
        public class AppendRPCStringParameterValueMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method)]
        public class AppendRPCArrayParameterValueMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method)]
        public class AppendRPCValueTypeParameterValueMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method)]
        public class ParseStringMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method)]
        public class ParseArrayMarker : Attribute {}

        [AttributeUsage(AttributeTargets.Method)]
        public class ParseStructureMarker : Attribute {}

    }
}
