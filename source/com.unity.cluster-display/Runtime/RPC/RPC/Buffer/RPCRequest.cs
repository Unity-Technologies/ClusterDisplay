using System;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using buint = System.UInt32;

namespace Unity.ClusterDisplay.RPC
{
    internal struct RPCRequest
    {
        public ushort rpcId;
        public RPCExecutionStage rpcExecutionStage;

        public ushort pipeId;
        public bool isStaticRPC;

        public buint parametersPayloadSize;
        public ushort assemblyIndex;
    }
}
