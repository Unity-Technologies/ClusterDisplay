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
#if UNITY_EDITOR_WIN
            internal const string LibD3D11 = "Packages/com.unity.cluster-display/Runtime/Plugins/x86_64/GfxPluginQuadroSyncD3D11.dll";
            internal const string LibD3D12 = "Packages/com.unity.cluster-display/Runtime/Plugins/x86_64/GfxPluginQuadroSyncD3D12.dll";
#elif UNITY_STANDALONE
        internal const string LibD3D11 = "GfxPluginQuadroSyncD3D11";
        internal const string LibD3D12 = "GfxPluginQuadroSyncD3D12";
#else
        internal const string LibD3D11 = "";
        internal const string LibD3D12 = ""
#error "System not implemented"
#endif

            [DllImport(LibD3D11, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
            public static extern IntPtr GetRenderEventFuncD3D11();

            [DllImport(LibD3D12, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
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