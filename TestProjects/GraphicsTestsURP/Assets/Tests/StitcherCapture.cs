using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Tests.Universal
{
    class StitcherCapture : ICapturePresent, IDisposable
    {
        CommandBuffer m_CommandBuffer;
        RenderTexture m_Target;
        ClusterRenderer m_ClusterRenderer;

        public StitcherCapture(ClusterRenderer clusterRenderer, RenderTexture target)
        {
            m_ClusterRenderer = clusterRenderer;
            m_Target = target;
            m_CommandBuffer = CommandBufferPool.Get("Capture Stitcher.");
            m_ClusterRenderer.AddCapturePresent(this);
        }

        public void Dispose()
        {
            m_ClusterRenderer.RemoveCapturePresent(this);
            CommandBufferPool.Release(m_CommandBuffer);
        }

        public void OnBeginCapture()
        {
            m_CommandBuffer.SetRenderTarget(m_Target);
        }

        public void OnEndCapture()
        {
            UnityEngine.Graphics.ExecuteCommandBuffer(m_CommandBuffer);
            m_CommandBuffer.Clear();
        }

        public CommandBuffer GetCommandBuffer() => m_CommandBuffer;
    }
}
