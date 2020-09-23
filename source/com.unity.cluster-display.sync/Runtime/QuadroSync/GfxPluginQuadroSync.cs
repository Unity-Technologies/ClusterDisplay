using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public class GfxPluginQuadroSync
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

    class GfxPluginQuadroSyncUtilities
    {
#if (UNITY_64 || UNITY_EDITOR_64 || PLATFORM_ARCH_64)
        [DllImport("GfxPluginQuadroSync", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
#else
        [DllImport("GfxPluginQuadroSync32", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
#endif
        public static extern IntPtr GetRenderEventFunc();
    }

    public static GfxPluginQuadroSync Instance
    {
        get { return instance; }
    }
    private static readonly GfxPluginQuadroSync instance = new GfxPluginQuadroSync();

    static GfxPluginQuadroSync()
    {
    }

    private GfxPluginQuadroSync()
    {
    }

    public void ExecuteQuadroSyncCommand(EQuadroSyncRenderEvent id, IntPtr data)
    {
        var cmdBuffer = new CommandBuffer();
        cmdBuffer.IssuePluginEventAndData(GfxPluginQuadroSyncUtilities.GetRenderEventFunc(), (int)id, data);
        Graphics.ExecuteCommandBuffer(cmdBuffer);
    }
}