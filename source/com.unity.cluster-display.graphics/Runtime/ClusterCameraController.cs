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
        [HideInInspector][SerializeField] private Camera m_ContextCamera;
        [HideInInspector][SerializeField] private Matrix4x4 m_CachedNonClusterDisplayProjectionMatrix = Matrix4x4.identity;

        [HideInInspector][SerializeField] private Camera m_PreviousContextCamera;

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

        [SerializeField] private RTHandle m_RT;
        public RTHandle CameraContextRenderTexture
        {
            get => m_RT;
            set => m_Presenter.TargetRT = value;
        }

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
            {
                m_ContextCamera.enabled = true;
            }
        }

        public void OnTearDown()
        {
        }

        protected virtual void OnPollFrameSettings (Camera camera) {}
        private void PollFrameSettings (Camera camera)
        {
            PollFrameSettings(camera);
        }

        public void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras)
        {
        }

        public void OnBeginCameraRender (ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game)
                return;

            if (m_ContextCamera != null)
            {
                m_ContextCamera.projectionMatrix = m_CachedNonClusterDisplayProjectionMatrix;
                m_ContextCamera.cullingMatrix = m_ContextCamera.projectionMatrix * m_ContextCamera.worldToCameraMatrix;
            }

            m_Presenter.PollCamera(m_ContextCamera);

            if (camera != m_ContextCamera)
            {
                m_PreviousContextCamera = m_ContextCamera;
                m_ContextCamera = camera;

                m_CachedNonClusterDisplayProjectionMatrix = m_ContextCamera.projectionMatrix;

                PollFrameSettings(camera);

                if (onCameraChange != null)
                    onCameraChange(m_PreviousContextCamera, m_ContextCamera);
            }

            else
            {
                if (m_CachedNonClusterDisplayProjectionMatrix != m_ContextCamera.projectionMatrix)
                    m_CachedNonClusterDisplayProjectionMatrix = m_ContextCamera.projectionMatrix;
            }
        }

        public void OnEndCameraRender(ScriptableRenderContext context, Camera camera) {}

        public void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
}
