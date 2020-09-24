/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


public class QuadroSyncTests : MonoBehaviour
{
    public int MaxCommandsDebugDisplay = 5;

    public Button UseSystem;
    public Button UseSwapBarrier;
    public Button UseSwapGroups;
    public Button UseSyncCounter;
    public Button UseDisplayFrameCount;
    public Button UseResetFrameCount;

    public Text DebugTextUI;

    int commandsCountDisplay;

    public void SetDebugText(string text)
    {
        if (!DebugTextUI)
            return;

        if (commandsCountDisplay >= MaxCommandsDebugDisplay)
        {
            DebugTextUI.text = "";
            commandsCountDisplay = 0;
        }

        DebugTextUI.text += text + "\n";
        ++commandsCountDisplay;
    }

    public bool IsValid()
    {
        return m_pluginQuadroSync != null;
    }

    void Start()
    {
        DebugTextUI.text = "";

        UseSystem.onClick.AddListener(UseSystem_onClick);
        UseSwapBarrier.onClick.AddListener(UseSwapBarrier_onClick);
        UseSwapGroups.onClick.AddListener(UseSwapGroups_onClick);
        UseSyncCounter.onClick.AddListener(UseSyncCounter_onClick);
        UseDisplayFrameCount.onClick.AddListener(UseDisplayFrameCount_onClick);
        UseResetFrameCount.onClick.AddListener(UseResetFrameCount_onClick);

        SetColorsClick(UseSystem, m_pluginQuadroSync.UseSystem);
        SetColorsClick(UseSwapBarrier, m_pluginQuadroSync.UseSwapBarrier);
        SetColorsClick(UseSwapGroups, m_pluginQuadroSync.UseSwapGroup);
        SetColorsClick(UseSyncCounter, m_pluginQuadroSync.UseSyncCounter);
    }

    void OnEnable()
    {
        if (DebugTextUI)
        {
            DebugTextUI.text = "";
        }
    }

    void OnDisable()
    {
        if (DebugTextUI)
        {
            DebugTextUI.text = "";
        }
    }

    void SetColorsClick(Button button, bool value)
    {
        var colors = button.colors;
        colors.normalColor = (value) ? Color.green : Color.red;
        colors.selectedColor = (value) ? Color.green : Color.red;
        button.colors = colors;
    }

    void UseSystem_onClick()
    {
        if (!IsValid())
            return;

        m_pluginQuadroSync.UseSystem = !m_pluginQuadroSync.UseSystem;
        SetColorsClick(UseSystem, m_pluginQuadroSync.UseSystem);
    }

    void UseSwapBarrier_onClick()
    {
        if (!IsValid())
            return;

        m_pluginQuadroSync.UseSwapBarrier = !m_pluginQuadroSync.UseSwapBarrier;
        SetColorsClick(UseSwapBarrier, m_pluginQuadroSync.UseSwapBarrier);
    }

    void UseSwapGroups_onClick()
    {
        if (!IsValid())
            return;

        m_pluginQuadroSync.UseSwapGroup = !m_pluginQuadroSync.UseSwapGroup;
        SetColorsClick(UseSwapGroups, m_pluginQuadroSync.UseSwapGroup);
    }

    void UseSyncCounter_onClick()
    {
        if (!IsValid())
            return;

        m_pluginQuadroSync.UseSyncCounter = !m_pluginQuadroSync.UseSyncCounter;
        SetColorsClick(UseSyncCounter, m_pluginQuadroSync.UseSyncCounter);
    }

    void UseDisplayFrameCount_onClick()
    {
        if (!IsValid())
            return;

        m_pluginQuadroSync.DisplayFrameCount = !m_pluginQuadroSync.DisplayFrameCount;
    }

    void UseResetFrameCount_onClick()
    {
        if (!IsValid())
            return;

        m_pluginQuadroSync.ResetFrameCount = !m_pluginQuadroSync.ResetFrameCount;
    }
}*/

/*
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

    {
        if (oldUseSystem != UseSystem)
        {
            oldUseSystem = UseSystem;
            quadroSyncUI.SetDebugText("[QuadroSync] - UseSystem: " + UseSystem);

            IntPtr data = new IntPtr(Convert.ToInt32(UseSystem));
            var cmdBuffer = new CommandBuffer();
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
}*/