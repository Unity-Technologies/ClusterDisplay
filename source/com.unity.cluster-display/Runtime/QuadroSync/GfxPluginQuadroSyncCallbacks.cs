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

        int m_previousVsync;
        int m_previewFrameQueue;

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
            m_previousVsync = QualitySettings.vSyncCount;
            m_previewFrameQueue = QualitySettings.maxQueuedFrames;
            ClusterDebug.Log("Enabling VSYNC");
            QualitySettings.vSyncCount = 1;
            QualitySettings.maxQueuedFrames = 1;

            if (m_InitDelayFrames >= 0)
            {
                StartCoroutine(DelayedInit());
            }

            else
            {
                InitializeQuadroSync();
            }
        }

        void OnDisable()
        {
            if (m_Initialized)
            {
                ClusterDebug.Log("Disposing Quadro Sync.");

                Instance.ExecuteQuadroSyncCommand(EQuadroSyncRenderEvent.QuadroSyncDispose, new IntPtr());
                m_Initialized = false;

                ClusterDebug.Log($"Reverting VSYNC mode to: {m_previousVsync}");
                QualitySettings.vSyncCount = m_previousVsync;
                QualitySettings.maxQueuedFrames = m_previewFrameQueue;
                ClusterSync.Instance.HasHardwareSync = false;
            }
        }

        void InitializeQuadroSync ()
        {
            if (m_Initialized)
                return;

            ClusterDebug.Log("Initializing Quadro Sync.");
            Instance.ExecuteQuadroSyncCommand(EQuadroSyncRenderEvent.QuadroSyncInitialize, new IntPtr());
            m_Initialized = true;
            ClusterSync.Instance.HasHardwareSync = true;
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
