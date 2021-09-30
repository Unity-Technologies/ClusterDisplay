using System;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Cecil;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using buint = System.UInt32;

namespace Unity.ClusterDisplay.RPC
{
    public static partial class RPCBufferIO
    {
        private static NativeArray<byte> rpcBuffer;

        /// <summary>
        /// This is the current size of our RPC buffer which grows as were writing it. The actual RPC 
        /// buffer is a fixed size. So this can also be considered as the RPC buffer write head.
        /// </summary>
        private static buint rpcBufferSize;

        public static buint RPCBufferSize => rpcBufferSize;

        // The RPC header is represented by 3 unsigned shorts and a payload size:
        private readonly static buint MinimumRPCPayloadSize = (buint)Marshal.SizeOf<ushort>() * 3 + (buint)Marshal.SizeOf<buint>();

        /// <summary>
        /// This does not become true until we've connected to the cluster display network.
        /// </summary>
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
