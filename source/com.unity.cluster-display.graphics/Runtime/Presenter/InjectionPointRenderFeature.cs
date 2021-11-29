#if CLUSTER_DISPLAY_URP
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
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
    [DisallowMultipleRendererFeature("InjectionPoint")]
    public class InjectionPointRenderFeature : ScriptableRendererFeature
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
}
#endif
