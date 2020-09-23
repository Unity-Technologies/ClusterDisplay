using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QuadroSyncUI : MonoBehaviour
{
    public int MaxCommandsDebugDisplay = 5;

    public Button UseSystem;
    public Button UseSwapBarrier;
    public Button UseSwapGroups;
    public Button UseSyncCounter;
    public Button UseDisplayFrameCount;
    public Button UseResetFrameCount;

    public Text DebugTextUI;

    GfxPluginQuadroSync m_pluginQuadroSync;
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
        m_pluginQuadroSync = GetComponent<GfxPluginQuadroSync>();
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
}
