using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Tests.Universal
{
    class CameraCapture : IDisposable
    {
        Camera m_Camera;
        RenderTexture m_Target;

        public CameraCapture(Camera camera, RenderTexture target)
        {
            m_Camera = camera;
            m_Target = target;
            CameraCaptureBridge.enabled = true;
            CameraCaptureBridge.AddCaptureAction(m_Camera, CaptureAction);
        }

        public void Dispose()
        {
            CameraCaptureBridge.RemoveCaptureAction(m_Camera, CaptureAction);
            CameraCaptureBridge.enabled = false;
        }

        void CaptureAction(RenderTargetIdentifier source, CommandBuffer cb)
        {
            cb.SetRenderTarget(m_Target);
            cb.ClearRenderTarget(false, true, Color.blue);
            Blitter_.BlitQuad(cb, source);
        }
    }
}
