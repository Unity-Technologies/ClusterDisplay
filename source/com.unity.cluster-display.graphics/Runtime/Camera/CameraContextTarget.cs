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
        public bool CameraReferenceIsValid => m_TargetCamera != null;

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
        private void Awake() => CacheCamera();
    #endif

        private void OnDestroy()
        {
            if (CameraContextRegistery.TryGetInstance(out var cameraContextRegistry, throwException: false))
                cameraContextRegistry.UnRegister(this);
        }
    }
}
