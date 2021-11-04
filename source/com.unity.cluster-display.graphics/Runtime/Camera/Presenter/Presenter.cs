using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// The purpose of the presenter is to automatically setup where
    /// renders are presented. Currently there are two presenting
    /// modes, one for XR and another for standard which uses
    /// and canvas UI.
    /// </summary>
    abstract class Presenter : IDisposable, ICameraEventReceiver
    {
        protected Camera m_Camera;
        public abstract RenderTexture PresentRT { set; }
        public abstract void Dispose();

        public void OnCameraContextChange(Camera previousCamera, Camera nextCamera)
        {
            m_Camera = nextCamera;
            if (m_Camera != null)
            {
                InitializeCamera(m_Camera);
            }
        }

        public void PollCamera(Camera camera)
        {
            if (camera == m_Camera)
            {
                return;
            }

            m_Camera = camera;
            InitializeCamera(camera);
        }

        protected abstract void InitializeCamera(Camera camera);
        protected abstract void DeinitializeCamera(Camera camera);
    }
}
