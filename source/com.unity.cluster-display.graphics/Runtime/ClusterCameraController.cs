using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    public class ClusterCameraController : ClusterRenderer.IClusterRendererEventReceiver
    {
        private Camera m_ContextCamera;
        private Matrix4x4 m_CachedNonClusterDisplayProjectionMatrix = Matrix4x4.identity;

        private Camera m_PreviousContextCamera;

        public Camera CameraContext => m_ContextCamera;
        public bool CameraContextIsSceneViewCamera => CameraIsSceneViewCamera(CameraContext);

        public delegate void OnCameraContextChange(Camera previousCamera, Camera nextCamera);
        private OnCameraContextChange onCameraChange;

        public delegate void OnChangeCameraProjectionMatrix(Camera camera, Matrix4x4 newProjectionMatrix);
        private OnChangeCameraProjectionMatrix onChangeCameraProjectionMatrix;

        public delegate void OnResizedRT(RenderTexture renderTexture);
        private OnResizedRT onResizedRT;

        [SerializeField] private RenderTexture m_RenderTexture;

        public ClusterCameraController ()
        {
            m_RenderTexture = new RenderTexture(Screen.width, Screen.height, 8);
            m_RenderTexture.name = "Pre-PresentBlitRT";

            onCameraChange += (Camera previousCamera, Camera nextCamera) =>
            {
                if (nextCamera == null)
                    return;

            };

            onResizedRT += (RenderTexture renderTexture) =>
            {
            };

            onResizedRT(m_RenderTexture);
        }

        public bool CameraIsSceneViewCamera (Camera camera)
        {
             return camera != null && SceneView.sceneViews.ToArray()
                .Select(sceneView => (sceneView as SceneView).camera)
                .Any(sceneViewCamera => sceneViewCamera == camera);
        }

        private void SetupCameraBeforeRender ()
        {
            Camera camera = Camera.current;
            if (camera == null)
            {
                camera = Camera.main;
                if (camera == null)
                    return;
            }

            if (camera.cameraType != CameraType.Game)
                return;

            if (camera != m_ContextCamera)
            {
                m_PreviousContextCamera = m_ContextCamera;
                m_ContextCamera = camera;

                m_CachedNonClusterDisplayProjectionMatrix = m_ContextCamera.projectionMatrix;

                if (onCameraChange != null)
                    onCameraChange(m_PreviousContextCamera, m_ContextCamera);
            }

            else
            {
                if (m_CachedNonClusterDisplayProjectionMatrix != m_ContextCamera.projectionMatrix)
                {
                    m_CachedNonClusterDisplayProjectionMatrix = m_ContextCamera.projectionMatrix;
                    if (onChangeCameraProjectionMatrix != null)
                        onChangeCameraProjectionMatrix(m_ContextCamera, m_ContextCamera.projectionMatrix);
                }
            }
        }

        public void OnPreLateUpdate()
        {
            SetupCameraBeforeRender();
            if (CameraContext != null)
            {
            }
        }

        public void OnPostLateUpdate()
        {
        }

        public void OnEndOfFrame()
        {
            if (m_ContextCamera == null)
                return;

            m_ContextCamera.projectionMatrix = m_CachedNonClusterDisplayProjectionMatrix;
            m_ContextCamera.cullingMatrix = m_ContextCamera.projectionMatrix * m_ContextCamera.worldToCameraMatrix;
        }
    }
}
