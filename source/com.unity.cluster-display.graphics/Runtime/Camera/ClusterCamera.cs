using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
#if CLUSTER_DISPLAY_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Attach this script to a Camera object if you wish for it to be rendered by
    /// the cluster.
    /// </summary>
    /// <remarks>
    /// The <see cref="ClusterRenderer"/> will take control of rendering, so the
    /// <see cref="UnityEngine.Camera"/> component will be disabled (and you cannot enable it while
    /// this script is active.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways, DisallowMultipleComponent]
    public class ClusterCamera : MonoBehaviour
    {
        struct CameraState
        {
            public RenderTexture Target;
            public bool Enabled;
#if CLUSTER_DISPLAY_HDRP
            public bool HasPersistentHistory;
#endif
        }

        CameraState m_CameraState;
        
        Camera m_Camera;
        Camera Camera
        {
            get
            {
                if (m_Camera == null)
                    m_Camera = GetComponent<Camera>();

                return m_Camera;
            }
        }
        
#if CLUSTER_DISPLAY_HDRP
        HDAdditionalCameraData m_AdditionalCameraData;
#endif
        void Update()
        {
            if (Camera.enabled && ClusterRenderer.IsActive())
            {
                // TODO Not technically breaking but unexpected from a usage perspective.
                Debug.LogError($"Camera {Camera.name} enabled while Cluster Renderer is active, this is not supported.");
            }
        }

        void OnEnable()
        {
#if CLUSTER_DISPLAY_HDRP
            m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<HDAdditionalCameraData>(gameObject);
#endif
            
            ClusterDisplayManager.onEnable += OnRendererEnabled;
            ClusterDisplayManager.onDisable += OnRendererDisabled;
            ClusterCameraManager.Register(Camera);

            // In case the renderer is already active;
            if (ClusterRenderer.IsActive())
            {
                OnRendererEnabled();
            }
        }

        void OnDisable()
        {
            ClusterCameraManager.Unregister(Camera);
            
            // In case the renderer is still active;
            if (ClusterRenderer.IsActive())
            {
                OnRendererDisabled();
            }
        }

        void OnRendererEnabled()
        {
            Assert.IsNotNull(Camera);

            // Save camera state.
            m_CameraState = new CameraState
            {
                Enabled = Camera.enabled,
                Target = Camera.targetTexture,
#if CLUSTER_DISPLAY_HDRP
                HasPersistentHistory = m_AdditionalCameraData.hasPersistentHistory
#endif
            };

            Camera.enabled = false;
#if CLUSTER_DISPLAY_HDRP
            // Since we will render this camera procedurally rendered,
            // we need to retain history buffers even if the camera is disabled.
            m_AdditionalCameraData.hasPersistentHistory = true;
#endif
        }

        void OnRendererDisabled()
        {
            // TODO What if the user alters the camera state between Enable() and here?
            // Restore camera state.
            Camera.enabled = m_CameraState.Enabled;
            Camera.targetTexture = m_CameraState.Target;
#if CLUSTER_DISPLAY_HDRP
            m_AdditionalCameraData.hasPersistentHistory = m_CameraState.HasPersistentHistory;
#endif

            ApplicationUtil.ResetCamera(Camera);
        }
    }

    public static class ClusterCameraManager
    {
        readonly static List<Camera> m_ActiveCameras = new();

        public static Camera ActiveCamera => m_ActiveCameras.ElementAtOrDefault(0);

        public static void Register(Camera camera)
        {
            if (!m_ActiveCameras.Contains(camera))
            {
                m_ActiveCameras.Add(camera);
            }
        }

        public static void Unregister(Camera camera)
        {
            m_ActiveCameras.Remove(camera);
        }
    }
}
