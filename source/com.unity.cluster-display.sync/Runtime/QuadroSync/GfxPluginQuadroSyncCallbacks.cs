using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public class GfxPluginQuadroSyncCallbacks : MonoBehaviour
{
    void OnEnable()
    {
        GfxPluginQuadroSync.Instance.ExecuteQuadroSyncCommand(GfxPluginQuadroSync.EQuadroSyncRenderEvent.QuadroSyncInitialize, new IntPtr());
    }

    void OnDisable()
    {        
        GfxPluginQuadroSync.Instance.ExecuteQuadroSyncCommand(GfxPluginQuadroSync.EQuadroSyncRenderEvent.QuadroSyncDispose, new IntPtr());
    }
}