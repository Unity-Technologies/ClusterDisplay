#if CLUSTER_DISPLAY_URP
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.ClusterDisplay.Graphics
{
    class UrpPresenter : IPresenter
    {
        /// <summary>
        /// A render pass whose purpose is to invoke an event at the desired stage of rendering.
        /// </summary>
        class InjectionPointRenderPass : ScriptableRenderPass
        {
            public static event Action<ScriptableRenderContext, RenderingData> ExecuteRender = delegate {};

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                ExecuteRender.Invoke(context, renderingData);
            }
        }

        /// <summary>
        /// A render feature whose purpose is to provide an event invoked at a given rendering stage.
        /// Meant to abstract away the render feature mechanism and allow for simple graphics code injection.
        /// </summary>
        internal class InjectionPointRenderFeature : ScriptableRendererFeature
        {
            InjectionPointRenderPass m_Pass;

            public override void Create()
            {
                m_Pass = new InjectionPointRenderPass
                {
                    renderPassEvent = RenderPassEvent.AfterRendering,
                };
            }

            public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
            {
                renderer.EnqueuePass(m_Pass);
            }
        }
        
        const string k_CommandBufferName = "Present To Screen";

        Camera m_Camera;
        RenderTexture m_RenderTexture;
        
        public void Dispose()
        {
            InjectionPointRenderPass.ExecuteRender -= ExecuteRender;
        }

        public void Initialize(GameObject gameObject)
        {
            m_Camera = ApplicationUtil.GetOrAddComponent<Camera>(gameObject);
            // We use the camera to blit to screen.
            m_Camera.targetTexture = null;
            m_Camera.hideFlags = HideFlags.NotEditable;
            
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