using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class Presenter : ICameraEventReceiver
    {
        protected Camera m_Camera;
        public abstract RenderTexture presentRT { get; set; }
        public abstract void Dispose();

        public void OnCameraContextChange(Camera previousCamera, Camera nextCamera)
        {
            if (previousCamera != null)
                DeinitializeCamera(previousCamera);

            m_Camera = nextCamera;
            if (m_Camera != null)
                InitializeCamera(m_Camera);
        }

        public void PollCamera(Camera camera)
        {
            if (camera == m_Camera)
                return;

            m_Camera = camera;
            InitializeCamera(camera);
        }

        protected abstract void InitializeCamera(Camera camera);
        protected abstract void DeinitializeCamera(Camera camera);
    }
}
