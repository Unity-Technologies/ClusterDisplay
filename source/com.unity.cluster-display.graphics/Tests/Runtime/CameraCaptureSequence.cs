using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public class CameraCaptureSequence : IDisposable
    {
        Camera m_Camera;
        RenderTexture[] m_Target;
        int m_Index;

        public void SetIndex(int value)
        {
            m_Index = value;

            if (m_Index < 0 || m_Index > m_Target.Length - 1)
            {
                throw new IndexOutOfRangeException($"Index [{m_Index}] out of range [0, {m_Target.Length - 1}]");
            }
        }
        
        public CameraCaptureSequence(Camera camera, RenderTexture[] target)
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

        void CaptureAction(RenderTargetIdentifier source, CommandBuffer cmd)
        {
            cmd.Blit(source, m_Target[m_Index]);
        }
    }
}
