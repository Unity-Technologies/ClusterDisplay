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
    public class GfxPluginQuadroSyncSystem
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
        };

        internal static class GfxPluginQuadroSyncUtilities
        {
#if UNITY_EDITOR_WIN
            const string DLLPath = "Packages/com.unity.cluster-display/Runtime/Plugins/x86_64/GfxPluginQuadroSync.dll";
#elif UNITY_STANDALONE
            const string DLLPath = "GfxPluginQuadroSync";
#else
            const string DLLPath = "";
#error "System not implemented"
#endif
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void NewLogMessageCallback(int logType, IntPtr message);

            [StructLayout(LayoutKind.Sequential)]
            public struct QuadroSyncState
            {
                public uint initializationState;
                public uint swapGroupId;
                public uint swapBarrierId;
                public ulong presentedFramesSuccess;
                public ulong presentedFramesFailed;
            }

            [DllImport(DLLPath, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
            public static extern IntPtr GetRenderEventFunc();

            [DllImport(DLLPath, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
            public static extern void SetLogCallback(
                [MarshalAs(UnmanagedType.FunctionPtr)] NewLogMessageCallback newLogMessageCallback);

            [DllImport(DLLPath, CallingConvention = CallingConvention.StdCall)]
            public static extern void GetState(ref QuadroSyncState state);
        }

        /// <summary>
        /// Gets the unique instance of the class (Singleton).
        /// </summary>
        public static GfxPluginQuadroSyncSystem Instance
        {
            get { return instance; }
        }

        static readonly GfxPluginQuadroSyncSystem instance = new GfxPluginQuadroSyncSystem();

        static GfxPluginQuadroSyncSystem() { }

        GfxPluginQuadroSyncSystem()
        {
            #if CLUSTER_DISPLAY_VERBOSE_LOGGING
            GfxPluginQuadroSyncUtilities.SetLogCallback((type, message) =>
                Debug.unityLogger.Log((LogType)type, Marshal.PtrToStringAnsi(message)));
            #endif
        }

        /// <summary>
        /// Executes a CommandBuffer related to the EQuadroSyncRenderEvent.
        /// </summary>
        /// <param name="id"> QuadroSync command to execute.</param>
        /// <param name="data"> Data bound to the executed command.</param>
        public void ExecuteQuadroSyncCommand(EQuadroSyncRenderEvent id, IntPtr data)
        {
            var cmdBuffer = new CommandBuffer();

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
            {
                cmdBuffer.IssuePluginEventAndData(GfxPluginQuadroSyncUtilities.GetRenderEventFunc(), (int) id, data);
            }

            Graphics.ExecuteCommandBuffer(cmdBuffer);
        }

        /// <summary>
        /// Fetch the state of GfxPluginQuadroSync.
        /// </summary>
        /// <returns>The state of GfxPluginQuadroSync</returns>
        /// <remarks>This is not a simple readonly property as getting this state is not "free" and fetching of the
        /// latest value is done every time the method is called.</remarks>
        public GfxPluginQuadroSyncState FetchState()
        {
            var fetchedState = new GfxPluginQuadroSyncUtilities.QuadroSyncState();
            GfxPluginQuadroSyncUtilities.GetState(ref fetchedState);

            return new GfxPluginQuadroSyncState(
                (GfxPluginQuadroSyncInitializationState)fetchedState.initializationState,
                fetchedState.swapGroupId, fetchedState.swapBarrierId, fetchedState.presentedFramesSuccess,
                fetchedState.presentedFramesFailed);
        }
    }
}
