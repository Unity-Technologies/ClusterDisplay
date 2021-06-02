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

        /// <summary>
        /// Current rendering camera.
        /// </summary>
        public Camera contextCamera
        {
            get
            {
                if (!CameraContextRegistery.TryGetInstance(out var cameraContextRegistry))
                    return null;

                if (cameraContextRegistry.focusedCameraContextTarget == null)
                    return null;

                if (!cameraContextRegistry.focusedCameraContextTarget.TryGetCamera(out var camera))
                {
                    cameraContextRegistry.UnRegister(cameraContextRegistry.focusedCameraContextTarget, destroy: true);
                    return null;
                }

                return camera;
            }
        }

        public Camera previousContextCamera
        {
            get
            {
                if (!CameraContextRegistery.TryGetInstance(out var cameraContextRegistry))
                    return null;

                if (cameraContextRegistry.previousFocusedCameraContextTarget == null)
                    return null;

                if (!cameraContextRegistry.previousFocusedCameraContextTarget.TryGetCamera(out var camera))
                {
                    cameraContextRegistry.UnRegister(cameraContextRegistry.previousFocusedCameraContextTarget, destroy: true);
                    return null;
                }

                return camera;
            }
        }


        public bool cameraContextIsSceneViewCamera => CameraIsSceneViewCamera(contextCamera);

        public delegate void OnCameraContextChange(Camera previousCamera, Camera nextCamera);
        private OnCameraContextChange onCameraChange;

        private Presenter m_Presenter;
        public Presenter presenter
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
            var contextCamera = this.contextCamera;
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
            if (camera == contextCamera)
            {
                m_Presenter.PollCamera(contextCamera);
                return;
            }

            if (!CameraContextRegistery.TryGetInstance(out var cameraContextRegistry) ||
                !cameraContextRegistry.TryGetCameraContextTarget(camera, out var cameraContextTarget))
            {
                m_Presenter.PollCamera(contextCamera);
                return;
            }

            cameraContextRegistry.focusedCameraContextTarget = cameraContextTarget;
            OnPollFrameSettings(camera);

            if (onCameraChange != null)
                onCameraChange(previousContextCamera, contextCamera);

            m_Presenter.PollCamera(contextCamera);
        }

        public void OnEndCameraRender(ScriptableRenderContext context, Camera camera) {}

        /// <summary>
        /// Before we call Camera.Render(), we change the camera's projectionMatrix to some asymmetric projection. However, before we do that
        /// we cache what the camera's projection matrix should be using the camera's paramters before we modify the camera's projection matrix 
        /// in order to later revert it after calling Camera.Render()
        /// </summary>
        public void ResetProjectionMatrix ()
        {
            var contextCamera = this.contextCamera;
            if (contextCamera == null)
                return;

            // var projectionMatrix = contextCamera.projectionMatrix;
            var projectionMatrix = Matrix4x4.Perspective(contextCamera.fieldOfView, contextCamera.aspect, contextCamera.nearClipPlane, contextCamera.farClipPlane);
            contextCamera.projectionMatrix = projectionMatrix;
            contextCamera.cullingMatrix = projectionMatrix * contextCamera.worldToCameraMatrix;
        }
    }
}
