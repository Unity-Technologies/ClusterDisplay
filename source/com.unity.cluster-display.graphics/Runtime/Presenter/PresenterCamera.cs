using Unity.ClusterDisplay.Graphics;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [RequireComponent(typeof(Camera))]
    public class PresenterCamera : SingletonMonoBehaviour<PresenterCamera>
    {
        public static Camera Camera
        {
            get
            {
                if (!TryGetInstance(out var instance, logError: false))
                {
                    instance = new GameObject("PresenterCamera").AddComponent<PresenterCamera>();
                    // instance.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                    instance.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    
                    // We use the camera to blit to screen.
                    // Configure it to minimize wasteful rendering.
                    instance.camera.targetTexture = null;
                    instance.camera.cullingMask = 0;
                    instance.camera.clearFlags = CameraClearFlags.Nothing;
                    instance.camera.depthTextureMode = DepthTextureMode.None;
                    
                    ClusterDebug.Log($"Setup {nameof(PresenterCamera)}.");
                }
                
                return instance.camera;
            }
        }

        private Camera m_Camera;

        private Camera camera
        {
            get
            {
                if (m_Camera == null)
                {
                    m_Camera = GetComponent<Camera>();
                    if (m_Camera == null)
                        m_Camera = gameObject.AddComponent<Camera>();
                }

                m_Camera.gameObject.hideFlags = HideFlags.DontSave;
                return m_Camera;
            }
        }

        protected override void OnAwake()
        {
            m_Camera = gameObject.AddComponent<Camera>();
        }
    }
}
