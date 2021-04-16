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
        // Matrix4x4 does not serialize so we need to serialize to Vector4s.
        [SerializeField] private Vector4 m_SerializedProjectionMatrixC1 = Vector4.zero;
        [SerializeField] private Vector4 m_SerializedProjectionMatrixC2 = Vector4.zero;
        [SerializeField] private Vector4 m_SerializedProjectionMatrixC3 = Vector4.zero;
        [SerializeField] private Vector4 m_SerializedProjectionMatrixC4 = Vector4.zero;

        public Camera ContextCamera
        {
            get
            {
                if (!CameraContextRegistery.TryGetInstance(out var cameraContextRegistry))
                    return null;

                if (cameraContextRegistry.FocusedCameraContextTarget == null)
                    return null;

                if (!cameraContextRegistry.FocusedCameraContextTarget.TryGetCamera(out var camera))
                {
                    cameraContextRegistry.UnRegister(cameraContextRegistry.FocusedCameraContextTarget, destroy: true);
                    return null;
                }

                return camera;
            }
        }

        public Camera PreviousContextCamera
        {
            get
            {
                if (!CameraContextRegistery.TryGetInstance(out var cameraContextRegistry))
                    return null;

                if (cameraContextRegistry.PreviousFocusedCameraContextTarget == null)
                    return null;

                if (!cameraContextRegistry.PreviousFocusedCameraContextTarget.TryGetCamera(out var camera))
                {
                    cameraContextRegistry.UnRegister(cameraContextRegistry.PreviousFocusedCameraContextTarget, destroy: true);
                    return null;
                }

                return camera;
            }
        }


        public bool CameraContextIsSceneViewCamera => CameraIsSceneViewCamera(ContextCamera);

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

        public bool CameraIsInContext(Camera camera)
        {
            var contextCamera = ContextCamera;
            return contextCamera != null && contextCamera == camera;
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

        protected virtual void OnPollFrameSettings (Camera camera) {}

        public void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}

        public void OnBeginCameraRender (ScriptableRenderContext context, Camera camera)
        {

            // If we are beginning to render with our context camera, do nothing.
            if (camera == ContextCamera)
            {
                m_Presenter.PollCamera(ContextCamera);
                return;
            }

            if (!CameraContextRegistery.TryGetInstance(out var cameraContextRegistry) ||
                !cameraContextRegistry.TryGetCameraContextTarget(camera, out var cameraContextTarget))
            {
                m_Presenter.PollCamera(ContextCamera);
                return;
            }

            cameraContextRegistry.FocusedCameraContextTarget = cameraContextTarget;
            OnPollFrameSettings(camera);

            if (onCameraChange != null)
                onCameraChange(PreviousContextCamera, ContextCamera);

            m_Presenter.PollCamera(ContextCamera);
        }

        public void OnEndCameraRender(ScriptableRenderContext context, Camera camera) {}

        public void CacheContextProjectionMatrix ()
        {
            var contextCamera = ContextCamera;
            if (contextCamera == null)
                return;
            var projectionMatrix = contextCamera.projectionMatrix;
            m_SerializedProjectionMatrixC1 = projectionMatrix.GetColumn(0);
            m_SerializedProjectionMatrixC2 = projectionMatrix.GetColumn(1);
            m_SerializedProjectionMatrixC3 = projectionMatrix.GetColumn(2);
            m_SerializedProjectionMatrixC4 = projectionMatrix.GetColumn(3);
        }

        public void ApplyCachedProjectionMatrixToContext ()
        {
            var contextCamera = ContextCamera;
            if (contextCamera == null)
                return;

            Matrix4x4 cachedProjectionMatrix = new Matrix4x4(
                m_SerializedProjectionMatrixC1,
                m_SerializedProjectionMatrixC2,
                m_SerializedProjectionMatrixC3,
                m_SerializedProjectionMatrixC4
            );

            contextCamera.projectionMatrix = cachedProjectionMatrix;
            contextCamera.cullingMatrix = contextCamera.projectionMatrix * contextCamera.worldToCameraMatrix;
        }
    }
}
