using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.Editor.Tests")]
namespace Unity.ClusterDisplay
{
    public class GfxPluginQuadroSyncSystem
    {
        public enum EQuadroSyncRenderEvent
        {
            QuadroSyncInitialize = 0,
            QuadroSyncQueryFrameCount,
            QuadroSyncResetFrameCount,
            QuadroSyncDispose,
            QuadroSyncEnableSystem,
            QuadroSyncEnableSwapGroup,
            QuadroSyncEnableSwapBarrier,
            QuadroSyncEnableSyncCounter
        };

        internal static class GfxPluginQuadroSyncUtilities
        {
#if (UNITY_64 || UNITY_EDITOR_64 || PLATFORM_ARCH_64)
            [DllImport("GfxPluginQuadroSyncD3D11.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
#endif
            public static extern IntPtr GetRenderEventFuncD3D11();

#if (UNITY_64 || UNITY_EDITOR_64 || PLATFORM_ARCH_64)
            [DllImport("GfxPluginQuadroSyncD3D12.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
#endif
            public static extern IntPtr GetRenderEventFuncD3D12();
        }

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