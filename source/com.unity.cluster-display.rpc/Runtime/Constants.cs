using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    internal static class Constants
    {
        public const uint MaxRPCID = ushort.MaxValue;

        public const uint DefaultMaxSingleRPCParameterByteSize = ushort.MaxValue;
        public const uint DefaultMaxSingleRPCByteSize = ushort.MaxValue;
        public const uint DefaultMaxRpcByteBufferSize = ushort.MaxValue;

        public const uint DefaultMaxFrameNetworkByteBufferSize = ushort.MaxValue;
        public const uint DefaultMaxMTUSize = ushort.MaxValue;
    }
}
