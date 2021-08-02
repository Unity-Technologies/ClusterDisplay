using System;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.ClusterDisplay.RPC
{
    public static partial class RPCBufferIO
    {
        private static NativeArray<byte> rpcBuffer;

        private static int rpcBufferSize;
        public static int RPCBufferSize => rpcBufferSize;

        // The RPC header is represented by 3 unsigned shorts and a byte:
        //  RPC ID: ushort (2 bytes)
        //  RPC Execution Stage: ushort (2 bytes).
        //  Pipe ID: ushort (2 bytes).
        //  Parameter payload size: ushort (2 bytes).
        private const int MinimumRPCPayloadSize = sizeof(ushort) * 4;

        public static bool AllowWrites = false;

        public static void Initialize (uint maxRpcByteBufferSize)
        {
            rpcBuffer = new NativeArray<byte>((int)maxRpcByteBufferSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            rpcBufferSize = 0;
        }
    }
}
