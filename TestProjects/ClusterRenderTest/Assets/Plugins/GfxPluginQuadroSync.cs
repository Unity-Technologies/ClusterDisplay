using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;


public class GfxPluginQuadroSyncUtilities
{
#if (UNITY_64 || UNITY_EDITOR_64 || PLATFORM_ARCH_64)
    [DllImport("GfxPluginQuadroSync", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
#else
    [DllImport("GfxPluginQuadroSync32", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
#endif
    public static extern IntPtr GetRenderEventFunc();
}

[ExecuteAlways]
public class GfxPluginQuadroSync : MonoBehaviour
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

    public bool UseSystem = false;
    public bool UseSwapBarrier = false;
    public bool UseSwapGroup = false;
    public bool UseSyncCounter = false;
    public bool DisplayFrameCount = false;
    public bool ResetFrameCount = false;

    QuadroSyncUI quadroSyncUI;
    bool oldUseSystem = false;
    bool oldUseSwapGroup = false;
    bool oldUseSwapBarrier = false;
    bool oldUseSyncCounter = false;

    void OnEnable()
    {
        quadroSyncUI = GetComponent<QuadroSyncUI>();
        quadroSyncUI.SetDebugText("[QuadroSync] - OnEnable.");

        ExecuteCommandBuffer(EQuadroSyncRenderEvent.QuadroSyncInitialize, new IntPtr());
    }

    void OnDisable()
    {
        quadroSyncUI.SetDebugText("[QuadroSync] - OnDisable.");

        ExecuteCommandBuffer(EQuadroSyncRenderEvent.QuadroSyncDispose, new IntPtr());
    }

    void Update()
    {
        GetKeyDown();
        ExecuteCommands();
    }

    void GetKeyDown()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            UseSystem = !UseSystem;
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            UseSwapBarrier = !UseSwapBarrier;
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            UseSwapGroup = !UseSwapGroup;
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            UseSyncCounter = !UseSyncCounter;
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            DisplayFrameCount = true;
        }

        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            ResetFrameCount = true;
        }
    }

    void ExecuteCommandBuffer(EQuadroSyncRenderEvent id, IntPtr data)
    {
        var cmdBuffer = new CommandBuffer();
        cmdBuffer.IssuePluginEventAndData(GfxPluginQuadroSyncUtilities.GetRenderEventFunc(), (int)id, data);
        Graphics.ExecuteCommandBuffer(cmdBuffer);
    }

    void ExecuteCommands()
    {
        if (oldUseSystem != UseSystem)
        {
            oldUseSystem = UseSystem;
            quadroSyncUI.SetDebugText("[QuadroSync] - UseSystem: " + UseSystem);

            IntPtr data = new IntPtr(Convert.ToInt32(UseSystem));
            var cmdBuffer = new CommandBuffer();
            cmdBuffer.IssuePluginEventAndData(GfxPluginQuadroSyncUtilities.GetRenderEventFunc(), 
                                              (int)EQuadroSyncRenderEvent.QuadroSyncEnableSystem, 
                                              data);
            Graphics.ExecuteCommandBuffer(cmdBuffer);


            ExecuteCommandBuffer(EQuadroSyncRenderEvent.QuadroSyncEnableSystem, data);

        }

        if (oldUseSwapBarrier != UseSwapBarrier)
        {
            oldUseSwapBarrier = UseSwapBarrier;
            quadroSyncUI.SetDebugText("[QuadroSync] - UseSwapBarrier: " + UseSwapBarrier);

            IntPtr data = new IntPtr(Convert.ToInt32(UseSwapBarrier));
            ExecuteCommandBuffer(EQuadroSyncRenderEvent.QuadroSyncEnableSwapBarrier, data);
        }

        if (oldUseSwapGroup != UseSwapGroup)
        {
            oldUseSwapGroup = UseSwapGroup;
            quadroSyncUI.SetDebugText("[QuadroSync] - UseSwapGroup: " + UseSwapGroup);

            IntPtr data = new IntPtr(Convert.ToInt32(UseSwapGroup));
            ExecuteCommandBuffer(EQuadroSyncRenderEvent.QuadroSyncEnableSwapGroup, data);
        }

        if (oldUseSyncCounter != UseSyncCounter)
        {
            oldUseSyncCounter = UseSyncCounter;
            quadroSyncUI.SetDebugText("[QuadroSync] - UseSyncCounter: " + UseSyncCounter);

            IntPtr data = new IntPtr(Convert.ToInt32(UseSyncCounter));
            ExecuteCommandBuffer(EQuadroSyncRenderEvent.QuadroSyncEnableSyncCounter, data);
        }

        if (DisplayFrameCount)
        {
            DisplayFrameCount = false;

            unsafe
            {
                int frameCount = 0;
                quadroSyncUI.SetDebugText("[QuadroSync] - QueryFrameCount:" + frameCount);

                IntPtr data = new IntPtr(&frameCount);
                ExecuteCommandBuffer(EQuadroSyncRenderEvent.QuadroSyncQueryFrameCount, data);
            }
        }

        if (ResetFrameCount)
        {
            ResetFrameCount = false;
            quadroSyncUI.SetDebugText("[QuadroSync] - ResetFrameCount.");

            ExecuteCommandBuffer(EQuadroSyncRenderEvent.QuadroSyncResetFrameCount, new IntPtr());
        }
    }
}
