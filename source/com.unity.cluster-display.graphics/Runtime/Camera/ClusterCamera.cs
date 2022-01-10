using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Attach this script to a Camera object if you wish for it to be rendered by
    /// the cluster.
    /// </summary>
    /// <remarks>
    /// The <see cref="ClusterRenderer"/> will take control of rendering, so the
    /// <see cref="Camera"/> component will be disabled (and you cannot enable it while
    /// this script is active.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways, DisallowMultipleComponent]
    public class ClusterCamera : MonoBehaviour
    {
        // TODO The notion of camera state overlaps with camera scopes who could also implement save-restore mechanism.
        struct CameraState
        {
            public RenderTexture Target;
            public bool Enabled;
        }

        CameraState m_CameraState;
        Camera m_Camera;

        void Update()
        {
            m_Camera = GetComponent<Camera>();
            
            if (m_Camera.enabled && ClusterRenderer.IsActive())
            {
                // TODO Not technically breaking but unexpected from a usage perspective.
                Debug.LogError($"Camera {m_Camera.name} enabled while Cluster Renderer is active, this is not supported.");
            }
        }
        
        void OnEnable()
        {
            ClusterRenderer.Enabled += OnRendererEnabled;
            ClusterRenderer.Disabled += OnRendererDisabled;
            ClusterCameraManager.Instance.Register(m_Camera);
            
            // In case the renderer is already active;
            if (ClusterRenderer.IsActive())
            {
                OnRendererEnabled();
            }
        }

        void OnDisable()
        {
            ClusterCameraManager.Instance.Unregister(m_Camera);
            ClusterRenderer.Enabled -= OnRendererEnabled;
            ClusterRenderer.Disabled -= OnRendererDisabled;
        }

        void OnRendererEnabled()
        {
            m_CameraState = new CameraState
            {
                Enabled = m_Camera.enabled,
                Target = m_Camera.targetTexture
            };
            
            m_Camera.enabled = false;
        }

        void OnRendererDisabled()
        {
            // TODO What if the user alters the Camera state between Enabled and here?
            m_Camera.enabled = m_CameraState.Enabled;
            m_Camera.targetTexture = m_CameraState.Target;
            
            // TODO Similar code in camera scopes, DRY?
            m_Camera.ResetWorldToCameraMatrix();
            m_Camera.ResetProjectionMatrix();
            m_Camera.ResetCullingMatrix();
            m_Camera.ResetAspect();
        }
    }

    class ClusterCameraManager
    {
        readonly List<Camera> m_ActiveCameras = new();

        public static ClusterCameraManager Instance { get; } = new();

        // Programmer's note: ElementAtOrDefault() is one of the few non-allocating LINQ methods
        public Camera ActiveCamera => m_ActiveCameras.ElementAtOrDefault(0);

        public void Register(Camera camera)
        {
            if (!m_ActiveCameras.Contains(camera))
            {
                m_ActiveCameras.Add(camera);
            }
        }

        public void Unregister(Camera camera)
        {
            m_ActiveCameras.Remove(camera);
        }
    }
}
