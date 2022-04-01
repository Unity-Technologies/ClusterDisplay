using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using buint = System.UInt32;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.RPC
{
    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
    public static partial class RPCBufferIO
    {
        private static NativeArray<byte> rpcBuffer;
        private const int k_MaxRPCByteBufferSize = ushort.MaxValue;
        private const int k_MaxSingleRPCParameterByteSize = ushort.MaxValue;
        private const int k_MaxSingleRPCByteSize = ushort.MaxValue;

        private const byte k_RPCStateID = 128;

        /// <summary>
        /// This is the current size of our RPC buffer which grows as were writing it. The actual RPC 
        /// buffer is a fixed size. So this can also be considered as the RPC buffer write head.
        /// </summary>
        private static buint rpcBufferSize;

        internal static buint RPCBufferSize => rpcBufferSize;

        // The RPC header is represented by 3 unsigned shorts and a payload size:
        private readonly static buint MinimumRPCPayloadSize = (buint)Marshal.SizeOf<ushort>() * 3 + (buint)Marshal.SizeOf<buint>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize ()
        {
            ClusterDebug.Log("Initializing RPC buffer.");

            Application.quitting -= Dispose;
            Application.quitting += Dispose;

            #if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            #endif

            rpcBuffer = new NativeArray<byte>(k_MaxRPCByteBufferSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            rpcBufferSize = 0;

            RepeaterStateReader.RegisterOnRestoreCustomDataDelegate(k_RPCStateID, Unlatch);
            EmitterStateWriter.RegisterOnStoreCustomDataDelegate(StoreRPCs);
        }

        static RPCBufferIO() => Initialize();

        private static void Dispose()
        {
            if (rpcBuffer.IsCreated)
                rpcBuffer.Dispose();
        }
    }
}
