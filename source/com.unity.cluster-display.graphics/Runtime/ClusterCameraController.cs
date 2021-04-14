using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    public interface ICameraEventReceiver
    {
        void OnCameraContextChange(Camera previousCamera, Camera nextCamera);
    }

    [System.Serializable]
    public class ClusterCameraController : ClusterRenderer.IClusterRendererEventReceiver
    {
        [SerializeField] private Camera m_ContextCamera;
        [SerializeField] private Camera m_PreviousContextCamera;

        // Matrix4x4 does not serialize so we need to serialize to Vector4s.
        [SerializeField] private Vector4 m_SerializedProjectionMatrixC1 = Vector4.zero;
        [SerializeField] private Vector4 m_SerializedProjectionMatrixC2 = Vector4.zero;
        [SerializeField] private Vector4 m_SerializedProjectionMatrixC3 = Vector4.zero;
        [SerializeField] private Vector4 m_SerializedProjectionMatrixC4 = Vector4.zero;

        public Camera CameraContext => m_ContextCamera;
        public Camera PreviousCameraContext => m_PreviousContextCamera;
        public bool CameraContextIsSceneViewCamera => CameraIsSceneViewCamera(CameraContext);

        public delegate void OnCameraContextChange(Camera previousCamera, Camera nextCamera);
        private OnCameraContextChange onCameraChange;

        private Presenter m_Presenter;
        public Presenter Presenter
        {
            get => m_Presenter;
            set
            {
                if (m_Presenter != null)
                {
                    UnRegisterCameraEventReceiver(m_Presenter);
                    m_Presenter.Dispose();
                }

                m_Presenter = value;
                if (m_Presenter != null)
                    RegisterCameraEventReceiver(m_Presenter);
            }
        }

        public bool CameraIsInContext(Camera camera) => m_ContextCamera != null && m_ContextCamera == camera;

        public bool CameraIsSceneViewCamera (Camera camera)
        {
#if UNITY_EDITOR
            return camera != null && SceneView.sceneViews.ToArray()
                .Select(sceneView => (sceneView as SceneView).camera)
                .Any(sceneViewCamera => sceneViewCamera == camera);
#else
        return false;
#endif
        }

        public void RegisterCameraEventReceiver (ICameraEventReceiver cameraEventReceiver) => onCameraChange += cameraEventReceiver.OnCameraContextChange;
        public void UnRegisterCameraEventReceiver (ICameraEventReceiver cameraEventReceiver) => onCameraChange -= cameraEventReceiver.OnCameraContextChange;

        public void OnSetup()
        {
            if (m_ContextCamera != null)
                m_ContextCamera.enabled = true;
        }

        public void OnTearDown() {}

        protected virtual void OnPollFrameSettings (Camera camera) {}

        public void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}

        private void PollCameraContext (Camera camera)
        {
            // If we are beginning to render with our context camera, do nothing.
            if (camera == m_ContextCamera)
                return;

            m_PreviousContextCamera = m_ContextCamera;
            m_ContextCamera = camera;

            OnPollFrameSettings(m_ContextCamera);

            if (onCameraChange != null)
                onCameraChange(m_PreviousContextCamera, m_ContextCamera);
        }

        public void OnBeginCameraRender (ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game)
                return;

            PollCameraContext(camera);
            m_Presenter.PollCamera(m_ContextCamera);
        }

        public void OnEndCameraRender(ScriptableRenderContext context, Camera camera) {}

        public void CacheContextProjectionMatrix ()
        {
            var projectionMatrix = m_ContextCamera.projectionMatrix;
            m_SerializedProjectionMatrixC1 = projectionMatrix.GetColumn(0);
            m_SerializedProjectionMatrixC2 = projectionMatrix.GetColumn(1);
            m_SerializedProjectionMatrixC3 = projectionMatrix.GetColumn(2);
            m_SerializedProjectionMatrixC4 = projectionMatrix.GetColumn(3);
        }

        public void ApplyCachedProjectionMatrixToContext ()
        {
            Matrix4x4 cachedProjectionMatrix = new Matrix4x4(
                m_SerializedProjectionMatrixC1,
                m_SerializedProjectionMatrixC2,
                m_SerializedProjectionMatrixC3,
                m_SerializedProjectionMatrixC4
            );

            m_ContextCamera.projectionMatrix = cachedProjectionMatrix;
            m_ContextCamera.cullingMatrix = m_ContextCamera.projectionMatrix * m_ContextCamera.worldToCameraMatrix;
        }
    }
}
