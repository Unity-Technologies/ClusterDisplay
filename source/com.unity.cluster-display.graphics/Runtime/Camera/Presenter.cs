using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// The purpose of the <see cref="Presenter"/> is to automatically setup where
    /// renders are presented.
    /// </summary>
    class Presenter : IDisposable, ICameraEventReceiver
    {
        Camera m_Camera;
        ClusterCanvas m_ClusterCanvas;
        RenderTexture m_PresentRT;
        
        public RenderTexture PresentRT
        {
            set
            {
                if (m_ClusterCanvas == null)
                {
                    return;
                }

                m_PresentRT = value;
                m_ClusterCanvas.rawImageTexture = m_PresentRT;
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

        public void OnCameraContextChange(Camera previousCamera, Camera nextCamera)
        {
            m_Camera = nextCamera;
            if (m_Camera != null)
            {
                Initialize();
            }
        }

        public void PollCamera(Camera camera)
        {
            if (camera == m_Camera)
            {
                return;
            }

            m_Camera = camera;
            Initialize();
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
