using System;
using System.Collections;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    using static GfxPluginQuadroSyncSystem;
    public class GfxPluginQuadroSyncCallbacks : MonoBehaviour
    {
        bool m_Initialized = false;

        void OnEnable() => StartCoroutine(WaitOneFrame());

        private void Init()
        {
            if (m_Initialized)
                return;
            
            GfxPluginQuadroSyncSystem.Instance.ExecuteQuadroSyncCommand(EQuadroSyncRenderEvent.QuadroSyncInitialize, new IntPtr());
            m_Initialized = true;
        }

        private IEnumerator WaitOneFrame()
        {
            // If were the emitter, wait until frame 2.
            while (ClusterDisplayState.IsEmitter && ClusterDisplayState.Frame < 1)
                yield return null;

            // Both emitter and repeater will wait one more frame.
            yield return null; 
            
            // Emitter is enabling Quadro Sync on frame 3.
            // Repeater is enabling Quadro sync on frame 1.
            
            Debug.Log($"(Frame: {ClusterDisplayState.Frame}): Initializing Quadro Sync.");
            Init();
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