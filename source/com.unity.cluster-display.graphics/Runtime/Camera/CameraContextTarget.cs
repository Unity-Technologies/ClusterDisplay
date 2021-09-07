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

        private void CacheCamera() => m_TargetCamera = GetComponent<Camera>();
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

        public void PollEnableState () =>
            m_TargetCamera.enabled = !m_TargetCamera.gameObject.activeInHierarchy;

        private void OnDestroy()
        {
            if (CameraContextRegistry.TryGetInstance(out var cameraContextRegistry, logError: false))
                cameraContextRegistry.UnRegister(this);
        }

        private void Start() => m_TargetCamera.enabled = true;
        private void OnDisable()
        {
            PollEnableState();
            if (onCameraDisabled != null)
                onCameraDisabled(this);
        }

        private void OnEnable()
        {
            CacheCamera();

            if (CameraContextRegistry.TryGetInstance(out var cameraContextRegistry, logError: false))
                cameraContextRegistry.Register(m_TargetCamera, logError: false);

            if (onCameraEnabled != null)
                onCameraEnabled(this);
        }
    }
}
