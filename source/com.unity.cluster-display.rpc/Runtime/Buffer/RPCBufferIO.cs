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
        public class IsEmitterMarker : Attribute {}

        static NativeArray<byte> rpcBuffer;
        const int k_MaxRPCByteBufferSize = ushort.MaxValue;
        const int k_MaxSingleRPCParameterByteSize = ushort.MaxValue;
        const int k_MaxSingleRPCByteSize = ushort.MaxValue;

        const byte k_RPCStateID = 128;

        [IsEmitterMarker]
        public static bool CaptureExecution => ClusterDisplayState.NodeRole is NodeRole.Emitter && ClusterDisplayState.IsActive || m_OverrideCaptureExecution;
        static bool m_OverrideCaptureExecution = false;

        static void OverrideCaptureExecution (bool capture)
        {
            ClusterDebug.Log($"Turned {(capture ? "on" : "off")} RPC method execution capture.");
            m_OverrideCaptureExecution = capture;
        }

        /// <summary>
        /// This is the current size of our RPC buffer which grows as were writing it. The actual RPC 
        /// buffer is a fixed size. So this can also be considered as the RPC buffer write head.
        /// </summary>
        static buint rpcBufferSize;

        internal static buint RPCBufferSize => rpcBufferSize;

        // The RPC header is represented by 3 unsigned shorts and a payload size:
        readonly static buint MinimumRPCPayloadSize = (buint)Marshal.SizeOf<ushort>() * 3 + (buint)Marshal.SizeOf<buint>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Initialize (bool overrideCaptureExecution = false)
        {
            ClusterDebug.Log("Initializing RPC buffer.");
            SetupBuffer();
            SetupDelegates();

            OverrideCaptureExecution(overrideCaptureExecution);
        }

        static void SetupDelegates()
        {
            Application.quitting -= Dispose;
            Application.quitting += Dispose;

            #if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            #endif

            RepeaterStateReader.RegisterOnLoadDataDelegate(k_RPCStateID, Unlatch);
            EmitterStateWriter.RegisterOnStoreCustomDataDelegate(k_RPCStateID, Latch);
        }

        static void SetupBuffer ()
        {
            if (rpcBuffer.IsCreated)
                rpcBuffer.Dispose();

            rpcBuffer = new NativeArray<byte>(k_MaxRPCByteBufferSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            rpcBufferSize = 0;
        }

        static RPCBufferIO() => Initialize();

        internal static void Dispose()
        {
            if (rpcBuffer.IsCreated)
                rpcBuffer.Dispose();
        }
    }
}
