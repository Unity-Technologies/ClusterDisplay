using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// The purpose of the <see cref="Presenter"/> is to automatically setup where
    /// renders are presented.
    /// </summary>
    class Presenter : IDisposable
    {
        ClusterCanvas m_ClusterCanvas;
        
        public RenderTexture PresentRT
        {
            set
            {
                // TODO remove lazy initialization,
                // manage lifecycle explicitely.
                if (m_ClusterCanvas == null)
                {
                    Initialize();
                }
                m_ClusterCanvas.rawImageTexture = value;
            }
        }

        public void Dispose()
        {
            if (m_ClusterCanvas != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(m_ClusterCanvas.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(m_ClusterCanvas.gameObject);
                }
            }
        }

        void Initialize()
        {
            if (!ClusterCanvas.TryGetInstance(out var clusterCanvas, false))
            {
                m_ClusterCanvas = new GameObject("ClusterCanvas").AddComponent<ClusterCanvas>();
            }
            else
            {
                m_ClusterCanvas = clusterCanvas.GetComponent<ClusterCanvas>();
            }

            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(m_ClusterCanvas.gameObject);
            }
        }
    }
}
