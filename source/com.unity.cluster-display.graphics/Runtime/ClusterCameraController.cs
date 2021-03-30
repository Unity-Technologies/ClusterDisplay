using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    [ExecuteAlways]
    public class ClusterCameraController : MonoBehaviour
    {
        private Camera m_cachedCamera;
        private Camera m_previouslyCachedCamera;

        public Camera CurrentCamera => m_cachedCamera;
        public bool CurrentCameraIsSceneViewCamera => CameraIsSceneViewCamera(CurrentCamera);

        public delegate void OnCameraChange(Camera previousCamera, Camera nextCamera);
        private OnCameraChange onCameraChange;

        public delegate void OnResizedRT(RenderTexture renderTexture);
        private OnResizedRT onResizedRT;

        [SerializeField] private RenderTexture renderTexture;

        public bool CameraIsSceneViewCamera (Camera camera)
        {
             return camera != null && SceneView.sceneViews.ToArray()
                .Select(sceneView => (sceneView as SceneView).camera)
                .Any(sceneViewCamera => sceneViewCamera == camera);
        }

        private void Awake()
        {
            renderTexture = new RenderTexture(Screen.width, Screen.height, 8);
            renderTexture.name = "Pre-PresentBlitRT";

            onCameraChange += (Camera previousCamera, Camera nextCamera) =>
            {
                // nextCamera.targetTexture = renderTexture;
            };

            onResizedRT += (RenderTexture renderTexture) =>
            {
            };


            onResizedRT(renderTexture);
        }

        public void SetupCameraBeforeRender ()
        {
            Camera camera = Camera.current;
            if (camera == null)
            {
                camera = Camera.main;
                if (camera == null)
                    return;
            }

            m_cachedCamera = camera;
            if (m_previouslyCachedCamera != m_cachedCamera)
            {
                if (onCameraChange != null)
                    onCameraChange(m_previouslyCachedCamera, m_cachedCamera);

                m_previouslyCachedCamera = m_cachedCamera;
            }
        }
    }
}
