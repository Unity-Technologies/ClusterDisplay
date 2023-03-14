using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.Editor.Tests")]

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// This class is responsible for controlling the QuadroSync technology.
    /// </summary>
    public static class GfxPluginQuadroSyncSystem
    {
        /// <summary>
        ///  Parameters to define the various QuadroSync states and the use of the SwapGroup and SwapBarrier system.
        /// </summary>
        public enum EQuadroSyncRenderEvent
        {
            /// <summary>
            /// Enables the Workstation SwapGroup and the use of
            /// the Swap Group and the Swap Barrier systems (NvAPI).
            /// </summary>
            QuadroSyncInitialize = 0,

            /// <summary>
            /// Queries the actual frame count in Runtime for the Emitter Sync system
            /// or for the custom frame count system.
            /// </summary>
            QuadroSyncQueryFrameCount,

            /// <summary>
            /// Resets the frame count for the Emitter Sync system (NvAPI) or
            /// for the custom frame count system.
            /// </summary>
            QuadroSyncResetFrameCount,

            /// <summary>
            /// Disables the use of the Swap Group and the Swap Barrier systems
            /// and disables the Workstation SwapGroup (NvAPI)
            /// </summary>
            QuadroSyncDispose,

            /// <summary>
            /// Enables or disables the use of the Swap Group and the Swap Barrier systems (NvAPI).
            /// </summary>
            QuadroSyncEnableSystem,

            /// <summary>
            /// Enables or disables the use of the Swap Group system (NvAPI).
            /// </summary>
            QuadroSyncEnableSwapGroup,

            /// <summary>
            /// Enables or disables the use of the Swap Barrier system (NvAPI).
            /// </summary>
            QuadroSyncEnableSwapBarrier,

            /// <summary>
            /// Enables or disables the use of the Emitter sync counter system (NvAPI).
            /// </summary>
            QuadroSyncEnableSyncCounter
        }

        /// <summary>
        /// How QuadroSync must behave following a call to the BarrierWarmupCallback.
        /// </summary>
        public enum BarrierWarmupAction
        {
            /// <summary>
            /// QuadroSync should re-present the previous frame.
            /// </summary>
            RepeatPresent = 0,
            /// <summary>
            /// Present should be considered done and QuadroSync should proceed to the next frame.
            /// </summary>
            ContinueToNextFrame,
            /// <summary>
            /// Consider QuadroSync Barrier as warmed up and ready to be used (so the warmup callback will not be called
            /// anymore).
            /// </summary>
            BarrierWarmedUp
        }

        internal static class GfxPluginQuadroSyncUtilities
        {
#if UNITY_EDITOR_WIN
            const string k_DLLPath = "Packages/com.unity.cluster-display/Runtime/Plugins/x86_64/GfxPluginQuadroSync.dll";
#elif UNITY_STANDALONE
            const string k_DLLPath = "GfxPluginQuadroSync";
#else
            const string k_DLLPath = "";
#error "System not implemented"
#endif
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void NewLogMessageCallback(int logType, IntPtr message);

            [DllImport(k_DLLPath, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
            public static extern IntPtr GetRenderEventFunc();

            [DllImport(k_DLLPath, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
            public static extern void SetLogCallback(
                [MarshalAs(UnmanagedType.FunctionPtr)] NewLogMessageCallback newLogMessageCallback);

            [DllImport(k_DLLPath, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
            public static extern void SetBarrierWarmupCallback(IntPtr barrierWarmupCallback);

            [DllImport(k_DLLPath, CallingConvention = CallingConvention.StdCall)]
            public static extern void GetState(ref GfxPluginQuadroSyncState state);
        }

        static GfxPluginQuadroSyncSystem()
        {
            EnableLogging();
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ClearCallbacks;
#endif
        }

        static void EnableLogging()
        {
#if CLUSTER_DISPLAY_VERBOSE_LOGGING
            GfxPluginQuadroSyncUtilities.SetLogCallback((type, message) =>
                Debug.unityLogger.Log((LogType)type, Marshal.PtrToStringAnsi(message)));
#endif
        }

        public static void DisableLogging()
        {
            GfxPluginQuadroSyncUtilities.SetLogCallback(null);
        }

        static void ClearCallbacks()
        {
            GfxPluginQuadroSyncUtilities.SetLogCallback(null);
            GfxPluginQuadroSyncUtilities.SetBarrierWarmupCallback(IntPtr.Zero);
        }

        /// <summary>
        /// Executes a CommandBuffer related to the EQuadroSyncRenderEvent.
        /// </summary>
        /// <param name="id"> QuadroSync command to execute.</param>
        /// <param name="data"> Data bound to the executed command.</param>
        public static void ExecuteQuadroSyncCommand(EQuadroSyncRenderEvent id, IntPtr data)
        {
            var cmdBuffer = new CommandBuffer();

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
            {
                cmdBuffer.IssuePluginEventAndData(GfxPluginQuadroSyncUtilities.GetRenderEventFunc(), (int)id, data);
            }

            Graphics.ExecuteCommandBuffer(cmdBuffer);
        }

        /// <summary>
        /// Sets the callback to call to ensure all nodes are properly synchronized while quadro sync barrier is warming
        /// up.
        /// </summary>
        /// <param name="func">The callback</param>
        public static void SetBarrierWarmupCallback(Func<BarrierWarmupAction> func)
        {
            // We have to keep a managed copy of the delegate because (from GetFunctionPointerForDelegate remarks):
            // You must manually keep the delegate from being collected by the garbage collector from managed code. The
            // garbage collector does not track references to unmanaged code.
            s_SetBarrierWarmupCallback = func;
            GfxPluginQuadroSyncUtilities.SetBarrierWarmupCallback(
                func != null ? Marshal.GetFunctionPointerForDelegate(func) : IntPtr.Zero);
        }
        // ReSharper disable once NotAccessedField.Local -> See comment in SetBarrierWarmupCallback
        static Func<BarrierWarmupAction> s_SetBarrierWarmupCallback;

        /// <summary>
        /// Fetch the state of GfxPluginQuadroSync.
        /// </summary>
        /// <returns>The state of GfxPluginQuadroSync</returns>
        /// <remarks>This is not a simple readonly property as getting this state is not "free" and fetching of the
        /// latest value is done every time the method is called.</remarks>
        public static GfxPluginQuadroSyncState FetchState()
        {
            var toReturn = new GfxPluginQuadroSyncState();
            GfxPluginQuadroSyncUtilities.GetState(ref toReturn);
            return toReturn;
        }
    }
}
