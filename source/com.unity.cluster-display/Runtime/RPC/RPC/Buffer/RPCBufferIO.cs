using System;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using buint = System.UInt32;

namespace Unity.ClusterDisplay.RPC
{
    public static partial class RPCBufferIO
    {
        private static NativeArray<byte> rpcBuffer;

        private static buint rpcBufferSize;
        public static buint RPCBufferSize => rpcBufferSize;

        // The RPC header is represented by 3 unsigned shorts and a byte:
        //  RPC ID: ushort (2 bytes)
        //  RPC Execution Stage: ushort (2 bytes).
        //  Pipe ID: ushort (2 bytes).
        //  Parameter payload size: uint (4 bytes).
        private const buint MinimumRPCPayloadSize = sizeof(ushort) * 3 + sizeof(buint);

        public static bool AllowWrites = false;

        private static ClusterDisplayResources.PayloadLimits m_CachedPayloadLimits;

        public static void Initialize (ClusterDisplayResources.PayloadLimits payloadLimits)
        {
            m_CachedPayloadLimits = payloadLimits;

            rpcBuffer = new NativeArray<byte>((int)m_CachedPayloadLimits.maxRpcByteBufferSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            rpcBufferSize = 0;
        }
    }
}
