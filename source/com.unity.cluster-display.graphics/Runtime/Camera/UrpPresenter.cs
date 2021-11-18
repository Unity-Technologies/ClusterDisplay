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

        Camera m_Camera;
        RenderTexture m_RenderTexture;
        
        public void Disable()
        {
            InjectionPointRenderPass.ExecuteRender -= ExecuteRender;
        }

        public void Enable(GameObject gameObject)
        {
            m_Camera = gameObject.GetOrAddComponent<Camera>();
            m_Camera.hideFlags = HideFlags.NotEditable;
            // We use the camera to blit to screen.
            // Configure it to minimize wasteful rendering.
            m_Camera.targetTexture = null;
            m_Camera.cullingMask = 0;
            m_Camera.clearFlags = CameraClearFlags.Nothing;
            m_Camera.depthTextureMode = DepthTextureMode.None;
            
            InjectionPointRenderPass.ExecuteRender += ExecuteRender;
        }

        public void SetSource(RenderTexture texture) => m_RenderTexture = texture;
        
        void ExecuteRender(ScriptableRenderContext context, RenderingData renderingData)
        {
            // The render pass gets invoked for all cameras so we need to filter.
            if (renderingData.cameraData.camera != m_Camera)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(k_CommandBufferName);
            cmd.Blit(m_RenderTexture, renderingData.cameraData.renderer.cameraColorTarget);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
#endif
