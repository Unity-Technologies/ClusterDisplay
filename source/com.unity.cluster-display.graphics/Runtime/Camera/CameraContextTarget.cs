using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    class CameraContextTarget : MonoBehaviour
    {
        [SerializeField]
        Camera m_TargetCamera;
        public Camera TargetCamera => m_TargetCamera;
        public bool CameraReferenceIsValid => m_TargetCamera != null;

        public event Action<CameraContextTarget> onCameraDisabled;
        public event Action<CameraContextTarget> onCameraEnabled;

        void CacheCamera()
        {
            m_TargetCamera = GetComponent<Camera>();
        }

        public bool TryGetCamera(out Camera camera)
        {
            if (m_TargetCamera == null)
            {
                CacheCamera();
            }

            camera = m_TargetCamera;
            return camera != null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            CacheCamera();
        }
#endif

        void OnDestroy()
        {
            if (CameraContextRegistry.TryGetInstance(out var cameraContextRegistry, false))
            {
                cameraContextRegistry.UnRegister(this);
            }
        }

        void OnDisable()
        {
            if (onCameraDisabled != null)
            {
                onCameraDisabled(this);
            }
        }

        void OnEnable()
        {
            CacheCamera();

            if (CameraContextRegistry.TryGetInstance(out var cameraContextRegistry))
            {
                cameraContextRegistry.Register(m_TargetCamera, false);
            }

            if (onCameraEnabled != null)
            {
                onCameraEnabled(this);
            }
        }
    }
}
