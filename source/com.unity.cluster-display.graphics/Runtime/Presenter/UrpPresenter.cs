#if CLUSTER_DISPLAY_URP
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.ClusterDisplay.Graphics
{
    class UrpPresenter : IPresenter
    {
        const string k_CommandBufferName = "Present To Screen";

        public event Action<PresentArgs> Present = delegate { };

        Camera m_Camera;
        Color m_ClearColor;
        bool m_DelayByOneFrame;
        
        public Color ClearColor
        {
            set => m_ClearColor = value;
        }

        public Camera Camera => m_Camera;
        
        public void Disable()
        {
            InjectionPointRenderPass.ExecuteRender -= ExecuteRender;
        }

        public void Enable(GameObject gameObject, bool delayByOneFrame)
        {
            m_DelayByOneFrame = delayByOneFrame;

            m_Camera = gameObject.GetOrAddComponent<Camera>();
            m_Camera.hideFlags = HideFlags.NotEditable | HideFlags.DontSave;
            // We use the camera to blit to screen.
            // Configure it to minimize wasteful rendering.
            m_Camera.targetTexture = null;
            m_Camera.cullingMask = 0;
            m_Camera.clearFlags = CameraClearFlags.Nothing;
            m_Camera.depthTextureMode = DepthTextureMode.None;
            
            InjectionPointRenderPass.ExecuteRender += ExecuteRender;
        }

        public RenderTexture m_LastFrame = null;
        void ExecuteRender(ScriptableRenderContext context, RenderingData renderingData)
        {
            if (ClusterDisplayState.IsEmitter && ClusterDisplayState.EmitterIsHeadless)
                return;
            
            // The render pass gets invoked for all cameras so we need to filter.
            if (renderingData.cameraData.camera != m_Camera)
            {
                return;
            }

            var target = renderingData.cameraData.renderer.cameraColorTargetHandle;
            var cmd = CommandBufferPool.Get(k_CommandBufferName);
            var cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;
            var cameraColorTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            GraphicsUtil.ExecuteCaptureIfNeeded(m_Camera, cmd, m_ClearColor, Present.Invoke, false);

            if (Application.isPlaying && m_DelayByOneFrame)
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

                    m_LastFrame.name = $"EmitterLastFrame-({m_LastFrame.width}x{m_LastFrame.height})";

                    m_LastFrame.antiAliasing = 1;
                    m_LastFrame.filterMode = FilterMode.Point;
                    
                    ClusterDebug.Log($"Created new buffer for storing previous frame.");
                }

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
                CameraPixelRect = m_Camera.pixelRect
            });
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
#endif
