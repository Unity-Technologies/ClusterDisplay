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
        
        public Color ClearColor
        {
            set => m_ClearColor = value;
        }

        public Camera Camera => m_Camera;
        
        public void Disable()
        {
            InjectionPointRenderPass.ExecuteRender -= ExecuteRender;
        }

        public void Enable(GameObject gameObject)
        {
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
        
        void ExecuteRender(ScriptableRenderContext context, RenderingData renderingData)
        {
            // The render pass gets invoked for all cameras so we need to filter.
            if (renderingData.cameraData.camera != m_Camera)
            {
                return;
            }

            var target = renderingData.cameraData.renderer.cameraColorTargetHandle;
            var cmd = CommandBufferPool.Get(k_CommandBufferName);

            GraphicsUtil.ExecuteCaptureIfNeeded(m_Camera, cmd, m_ClearColor, Present.Invoke, false);
            
            cmd.SetRenderTarget(target);
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
