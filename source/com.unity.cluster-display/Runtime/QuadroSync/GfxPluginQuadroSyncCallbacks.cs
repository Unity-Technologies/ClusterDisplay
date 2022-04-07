using System;
using System.Collections;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    using static GfxPluginQuadroSyncSystem;

    public class GfxPluginQuadroSyncCallbacks : MonoBehaviour
    {
        static GfxPluginQuadroSyncCallbacks s_Instance;
        bool m_Initialized = false;

        const int k_DefaultInitDelayFrames = 10;
        int m_InitDelayFrames;

        private int m_VSYNCMode;

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(s_Instance.gameObject);
            }

            m_InitDelayFrames = CommandLineParser.quadroSyncInitDelay != null ? CommandLineParser.quadroSyncInitDelay.Value : k_DefaultInitDelayFrames;
            DontDestroyOnLoad(gameObject);
            s_Instance = this;
        }

        void OnEnable()
        {
            if (!CommandLineParser.disableQuadroSync.Defined || !CommandLineParser.disableQuadroSync.Value)
            {
                m_VSYNCMode = QualitySettings.vSyncCount;
                ClusterDebug.Log("Enabling VSYNC");
                QualitySettings.vSyncCount = 1;

                if (m_InitDelayFrames >= 0)
                {
                    StartCoroutine(DelayedInit());
                }

                else
                {
                    InitializeQuadroSync();
                }
            }
        }

        void OnDisable()
        {
            if (m_Initialized)
            {
                ClusterDebug.Log("Disposing Quadro Sync.");

                Instance.ExecuteQuadroSyncCommand(EQuadroSyncRenderEvent.QuadroSyncDispose, new IntPtr());
                m_Initialized = false;

                ClusterDebug.Log($"Reverting VSYNC mode to: {m_VSYNCMode}");
                QualitySettings.vSyncCount = m_VSYNCMode;
            }
        }

        void InitializeQuadroSync ()
        {
            if (m_Initialized)
                return;

            ClusterDebug.Log("Initializing Quadro Sync.");
            Instance.ExecuteQuadroSyncCommand(EQuadroSyncRenderEvent.QuadroSyncInitialize, new IntPtr());
            m_Initialized = true;
        }

        IEnumerator DelayedInit()
        {
            if (!m_Initialized)
            {
                for (int i = 0; i < m_InitDelayFrames; i++)
                {
                    yield return null;
                }

                InitializeQuadroSync();
            }
        }
    }
}
