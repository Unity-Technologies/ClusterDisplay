#if CLUSTER_DISPLAY_URP
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Unity.ClusterDisplay.Graphics
{
    class UrpPresenter : IPresenter
    {
        const string k_CommandBufferName = "Present To Screen";

        public event Action<CommandBuffer> Present = delegate { };

        Camera m_Camera;
        Color m_ClearColor;
        private RenderTexture m_CopyBuffer;
        
        public Color ClearColor
        {
            set => m_ClearColor = value;
        }
        
        public void Disable()
        {
            InjectionPointRenderPass.ExecuteRender -= ExecuteRender;
            RenderPipelineManager.endFrameRendering -= EndFrameRendering;
        }

        public void Enable()
        {
            InjectionPointRenderPass.ExecuteRender -= ExecuteRender; // Insurances to avoid duplicate delegate registration.
            InjectionPointRenderPass.ExecuteRender += ExecuteRender;
            
            RenderPipelineManager.endFrameRendering -= EndFrameRendering;
            RenderPipelineManager.endFrameRendering += EndFrameRendering;
        }

        private void EndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            UnityEngine.Graphics.Blit(m_CopyBuffer, null as RenderTexture);
            // var cmd = CommandBufferPool.Get(k_CommandBufferName);
            // cmd.SetRenderTarget(null as RenderTexture);
            // cmd.ClearRenderTarget(true, true, m_ClearColor);
            // cmd.Blit(m_CopyBuffer, null as RenderTexture);
            // context.ExecuteCommandBuffer(cmd);
            // CommandBufferPool.Release(cmd);
        }
        
        void ExecuteRender(ScriptableRenderContext context, RenderingData renderingData)
        {
            if (m_Camera == null)
            {
                m_Camera = ClusterCameraManager.ActiveCamera;
                if (m_Camera == null)
                {
                    return;
                }
                
                m_Camera.hideFlags = HideFlags.None;
                // We use the camera to blit to screen.
                // Configure it to minimize wasteful rendering.
                // m_Camera.targetTexture = null;
                // m_Camera.cullingMask = 0;
                // m_Camera.clearFlags = CameraClearFlags.Nothing;
                // m_Camera.depthTextureMode = DepthTextureMode.None;
            }
            
            // The render pass gets invoked for all cameras so we need to filter.
            if (renderingData.cameraData.camera != m_Camera)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(k_CommandBufferName);
            
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            if (m_CopyBuffer == null ||
                m_CopyBuffer.width != descriptor.width ||
                m_CopyBuffer.height != descriptor.height ||
                m_CopyBuffer.depth != descriptor.depthBufferBits ||
                m_CopyBuffer.graphicsFormat != descriptor.graphicsFormat)
            {
                m_CopyBuffer = new RenderTexture(descriptor.width, descriptor.height, descriptor.depthBufferBits, descriptor.graphicsFormat);
            }
            
            cmd.SetRenderTarget(m_CopyBuffer);
            // cmd.ClearRenderTarget(true, true, m_ClearColor);
            
            Present.Invoke(cmd);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
#endif
