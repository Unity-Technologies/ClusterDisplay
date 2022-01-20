using Unity.ClusterDisplay.Graphics;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [RequireComponent(typeof(Camera))]
    public class PresenterCamera : SingletonMonoBehaviour<PresenterCamera>
    {
        private const string k_CameraGameObjectName = "54d9641b3e07469ea37bd29c6daee1bf";
        public static Camera Camera
        {
            get
            {
                if (!TryGetInstance(out var instance, logError: true))
                {
                    var cameraGo = GameObject.Find(k_CameraGameObjectName);

                    if (cameraGo == null)
                    {
                        cameraGo = new GameObject(k_CameraGameObjectName);
                    }

                    instance = cameraGo.GetOrAddComponent<PresenterCamera>();
                    SetInstance(instance);
                    
                    instance.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    
                    // We use the camera to blit to screen.
                    // Configure it to minimize wasteful rendering.
                    instance.cam.targetTexture = null;
                    instance.cam.cullingMask = 0;
                    instance.cam.clearFlags = CameraClearFlags.Nothing;
                    instance.cam.depthTextureMode = DepthTextureMode.None;
                    instance.cam.depth = 100;
                    
                    ClusterDebug.Log($"Created {nameof(PresenterCamera)}.");
                }

                return instance.cam;
            }
        }

        private Camera m_Camera;

        private Camera cam
        {
            get
            {
                if (m_Camera == null)
                {
                    m_Camera = gameObject.GetOrAddComponent<Camera>();
                }

                return m_Camera;
            }
        }

        protected override void OnAwake()
        {
            m_Camera = gameObject.GetOrAddComponent<Camera>();
        }
    }
}
