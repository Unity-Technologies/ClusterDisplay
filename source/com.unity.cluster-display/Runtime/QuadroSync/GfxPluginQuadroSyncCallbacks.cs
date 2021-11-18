using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    using static GfxPluginQuadroSyncSystem;
    public class GfxPluginQuadroSyncCallbacks : MonoBehaviour
    {
        bool m_Initialized = false;

        void OnEnable()
        {
            if (!m_Initialized)
            {
                GfxPluginQuadroSyncSystem.Instance.ExecuteQuadroSyncCommand(EQuadroSyncRenderEvent.QuadroSyncInitialize, new IntPtr());
                m_Initialized = true;
            }
        }

        void OnDisable()
        {
            if (m_Initialized)
            {
                GfxPluginQuadroSyncSystem.Instance.ExecuteQuadroSyncCommand(EQuadroSyncRenderEvent.QuadroSyncDispose, new IntPtr());
                m_Initialized = false;
            }
        }
    }
}