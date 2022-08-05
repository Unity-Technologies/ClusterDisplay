using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
#if CLUSTER_DISPLAY_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

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
        struct CameraState
        {
            public bool Enabled;
#if CLUSTER_DISPLAY_HDRP
            public bool HasPersistentHistory;
#endif
        }

        CameraState m_CameraState;
        Camera m_Camera;
#if CLUSTER_DISPLAY_HDRP
        HDAdditionalCameraData m_AdditionalCameraData;
#endif
        bool m_RendererEnabled;

        void Update()
        {
            if (m_Camera.enabled && ClusterRenderer.IsActive())
            {
                // TODO Not technically breaking but unexpected from a usage perspective.
                Debug.LogError($"Camera {m_Camera.name} enabled while Cluster Renderer is active, this is not supported.");
            }
        }

        void OnEnable()
        {
            m_Camera = GetComponent<Camera>();
#if CLUSTER_DISPLAY_HDRP
            m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<HDAdditionalCameraData>(gameObject);
#endif

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

            // In case the renderer is still active;
            if (ClusterRenderer.IsActive())
            {
                OnRendererDisabled();
            }
        }

        void OnRendererEnabled()
        {
            Assert.IsNotNull(m_Camera);
            if (m_RendererEnabled)
            {
                // It is entirely possible that OnRendererEnabled has already been called.  This can happen if OnEnable
                // of the ClusterCamera is called before OnEnable of ClusterRenderer.  We get called twice because even
                // if OnEnable of the ClusterRenderer hasn't been called yet, isActiveAndEnabled is returning true and
                // so ClusterCamera.OnEnable called this method.
                return;
            }

            // Save camera state.
            m_CameraState = new CameraState
            {
                Enabled = m_Camera.enabled,
#if CLUSTER_DISPLAY_HDRP
                HasPersistentHistory = m_AdditionalCameraData.hasPersistentHistory
#endif
            };

            m_RendererEnabled = true;

            m_Camera.enabled = false;
        }

        void OnRendererDisabled()
        {
            Assert.IsTrue(m_RendererEnabled);

            // TODO What if the user alters the camera state between Enable() and here?
            // Restore camera state.
            m_Camera.enabled = m_CameraState.Enabled;
#if CLUSTER_DISPLAY_HDRP
            m_AdditionalCameraData.hasPersistentHistory = m_CameraState.HasPersistentHistory;
#endif

            m_RendererEnabled = false;
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
