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
        /// Define the various QuadroSync states and the use of the SwapGroup and Swapbarrier system.
        /// </summary>
        public enum EQuadroSyncRenderEvent
        {
			/// <summary>
			/// Enable the Workstation SwapGroup and the use of
			/// the Swap Group and the Swap Barrier systems (NvAPI).
			/// </summary>
            QuadroSyncInitialize = 0,
			
			/// <summary>
			/// Query the actual frame count in Runtime for the Master Sync system
			/// or for the custom frame count system.
			/// </summary>
            QuadroSyncQueryFrameCount,
			
			/// <summary>
			/// Reset the frame count for the Master Sync system (NvAPI) or
			/// for the custom frame count system.
			/// </summary>
            QuadroSyncResetFrameCount,
			
			/// <summary>
			/// Disable the use of the Swap Group and the Swap Barrier systems
			/// and disable the Workstation SwapGroup (NvAPI)
			/// </summary>
            QuadroSyncDispose,
			
			/// <summary>
			/// Enable or disable the use of the Swap Group and the Swap Barrier systems (NvAPI).
			/// </summary>
            QuadroSyncEnableSystem,
			
			/// <summary>
			/// Enable or disable the use of the Swap Group system (NvAPI).
			/// </summary>
            QuadroSyncEnableSwapGroup,
			
			/// <summary>
			/// Enable or disable the use of the Swap Barrier system (NvAPI).
			/// </summary>
            QuadroSyncEnableSwapBarrier,
			
			/// <summary>
			/// Enable or disable the use of the Master sync counter system (NvAPI).
			/// </summary>
            QuadroSyncEnableSyncCounter
        };

        internal static class GfxPluginQuadroSyncUtilities
        {
#if (UNITY_64 || UNITY_EDITOR_64 || PLATFORM_ARCH_64)
            [DllImport("Packages/com.unity.cluster-display/Runtime/Plugins/x86_64/GfxPluginQuadroSyncD3D11.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
#endif
            public static extern IntPtr GetRenderEventFuncD3D11();

#if (UNITY_64 || UNITY_EDITOR_64 || PLATFORM_ARCH_64)
            [DllImport("Packages/com.unity.cluster-display/Runtime/Plugins/x86_64/GfxPluginQuadroSyncD3D12.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
#endif
            public static extern IntPtr GetRenderEventFuncD3D12();
        }

		/// <summary>
        /// Unique instance of the class (Singleton). 
        /// </summary>
        public static GfxPluginQuadroSyncSystem Instance
        {
            get { return instance; }
        }
        private static readonly GfxPluginQuadroSyncSystem instance = new GfxPluginQuadroSyncSystem();

        static GfxPluginQuadroSyncSystem()
        {
        }

        private GfxPluginQuadroSyncSystem()
        {
        }

		/// <summary>
        /// Execute a CommandBuffer related to the EQuadroSyncRenderEvent.
        /// </summary>
		 /// <param name="id"> QuadroSync command to execute.</param>
		 /// <param name="data"> Data bound to the executed command.</param>
        public void ExecuteQuadroSyncCommand(EQuadroSyncRenderEvent id, IntPtr data)
        {
            var cmdBuffer = new CommandBuffer();

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11)
            {
                cmdBuffer.IssuePluginEventAndData(GfxPluginQuadroSyncUtilities.GetRenderEventFuncD3D11(), (int)id, data);
            }
            else if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
            {
                cmdBuffer.IssuePluginEventAndData(GfxPluginQuadroSyncUtilities.GetRenderEventFuncD3D12(), (int)id, data);
            }

            Graphics.ExecuteCommandBuffer(cmdBuffer);
        }
    }
}