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
        }

        public void Enable()
        {
            InjectionPointRenderPass.ExecuteRender -= ExecuteRender; // Insurances to avoid duplicate delegate registration.
            InjectionPointRenderPass.ExecuteRender += ExecuteRender;
        }

        public RenderTexture m_LastFrame = null;
        void ExecuteRender(ScriptableRenderContext context, RenderingData renderingData)
        {
            if (m_Camera == null)
            {
                m_Camera = PresenterCamera.Camera;
                if (m_Camera == null)
                {
                    return;
                }
            }
            
            // The render pass gets invoked for all cameras so we need to filter.
            if (renderingData.cameraData.camera != m_Camera)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(k_CommandBufferName);
            var cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;
            var cameraColorTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            if (ClusterDisplayState.IsEmitter)
            {
                if (m_LastFrame == null ||
                    m_LastFrame.width != cameraColorTargetDescriptor.width ||
                    m_LastFrame.height != cameraColorTargetDescriptor.height ||
                    m_LastFrame.depth != cameraColorTargetDescriptor.depthBufferBits ||
                    m_LastFrame.graphicsFormat != cameraColorTargetDescriptor.graphicsFormat)
                {
                    if (m_LastFrame != null)
                    {
                        m_LastFrame.DiscardContents();
                        m_LastFrame = null;
                    }
                    
                    m_LastFrame = new RenderTexture(
                        cameraColorTargetDescriptor.width,
                        cameraColorTargetDescriptor.height, 
                        cameraColorTargetDescriptor.depthBufferBits,
                        cameraColorTargetDescriptor.graphicsFormat);
                }
                
                cmd.Blit(m_LastFrame, cameraColorTarget);
                cmd.SetRenderTarget(m_LastFrame);
            }
            
            else
            {
                cmd.SetRenderTarget(cameraColorTarget);
            }
            
            cmd.ClearRenderTarget(true, true, m_ClearColor);
            Present.Invoke(cmd);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
#endif
