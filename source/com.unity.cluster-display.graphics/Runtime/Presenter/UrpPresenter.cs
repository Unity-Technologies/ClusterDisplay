#if CLUSTER_DISPLAY_URP
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Unity.ClusterDisplay.Graphics
{
    class UrpPresenter : IPresenter
    {
        const string k_CommandBufferName = "Present To Screen";

        public event Action<PresentArgs> Present = delegate { };
        
        Color m_ClearColor = Color.black;
        private RenderTexture m_CopyBuffer;
        
        public Color ClearColor
        {
            set => m_ClearColor = value;
        }

        public Camera Camera => PresenterCamera.Camera;
        
        public void Disable()
        {
            InjectionPointRenderPass.ExecuteRender -= ExecuteRender;
        }

        public void Enable()
        {
            Camera.hideFlags = HideFlags.NotEditable;
            InjectionPointRenderPass.ExecuteRender -= ExecuteRender; // Insurances to avoid duplicate delegate registration.
            InjectionPointRenderPass.ExecuteRender += ExecuteRender;
        }

        public RenderTexture m_LastFrame = null;
        void ExecuteRender(ScriptableRenderContext context, RenderingData renderingData)
        {
            if (ClusterDisplayState.IsEmitter && ClusterDisplayState.EmitterIsHeadless)
                return;
            
            // The render pass gets invoked for all cameras so we need to filter.
            if (renderingData.cameraData.camera != Camera)
            {
                return;
            }

            var target = renderingData.cameraData.renderer.cameraColorTargetHandle;
            var cmd = CommandBufferPool.Get(k_CommandBufferName);
            var cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;
            var cameraColorTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            if (ClusterDisplayState.IsEmitter && Application.isPlaying)
            {
                ClusterDebug.Log($"Emitter presenting previous frame: {ClusterDisplayState.Frame - 1}");
                
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
                        cameraColorTargetDescriptor.graphicsFormat,
                        0);
                    m_LastFrame.antiAliasing = 1;
                    m_LastFrame.filterMode = FilterMode.Point;
                    
                    ClusterDebug.Log($"Created new buffer for storing previous frame.");
                }
				GraphicsUtil.ExecuteCaptureIfNeeded(Camera, cmd, m_ClearColor, Present.Invoke, false);
                cmd.SetRenderTarget(cameraColorTarget);
                cmd.ClearRenderTarget(true, true, m_ClearColor);
                
                cmd.Blit(m_LastFrame, cameraColorTarget);
                cmd.SetRenderTarget(m_LastFrame);
            }
            
            else
            {
                ClusterDebug.Log($"Repeater presenting current frame: {ClusterDisplayState.Frame}");
                cmd.SetRenderTarget(cameraColorTarget);
            }
            
            cmd.ClearRenderTarget(true, true, m_ClearColor);
            Present.Invoke(new PresentArgs
            {
                CommandBuffer = cmd,
                FlipY = false,
                CameraPixelRect = Camera.pixelRect
            });
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
#endif
