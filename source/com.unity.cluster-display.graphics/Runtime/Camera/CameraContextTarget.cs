using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraContextTarget : MonoBehaviour
    {
        [SerializeField] private Camera m_TargetCamera;
        public Camera TargetCamera => m_TargetCamera;
        public bool cameraReferenceIsValid => m_TargetCamera != null;

        public delegate void CameraActiveDelegate(CameraContextTarget cameraContextTarget);
        public CameraActiveDelegate onCameraDisabled;
        public CameraActiveDelegate onCameraEnabled;

        private void CacheCamera()
        {
            if (m_TargetCamera != null)
                return;

            m_TargetCamera = GetComponent<Camera>();
            if (m_TargetCamera == null)
            {
                Debug.LogError($"Missing {nameof(Camera)} component attached to: \"{gameObject.name}\".");
                return;
            }
        }

        public bool TryGetCamera (out Camera camera)
        {
            if (m_TargetCamera == null)
                CacheCamera();
            camera = m_TargetCamera;
            return camera != null;
        }

    #if UNITY_EDITOR
        private void OnValidate() => CacheCamera();
    #endif

        private void OnDestroy()
        {
            if (CameraContextRegistery.TryGetInstance(out var cameraContextRegistry))
                cameraContextRegistry.UnRegister(this);
        }

        private void OnDisable()
        {
            if (onCameraDisabled != null)
                onCameraDisabled(this);
        }

        private void OnEnable()
        {
            CacheCamera();

            if (CameraContextRegistery.TryGetInstance(out var cameraContextRegistry))
                cameraContextRegistry.Register(m_TargetCamera, logError: false);

            if (onCameraEnabled != null)
                onCameraEnabled(this);
        }
    }
}
