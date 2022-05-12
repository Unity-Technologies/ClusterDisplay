using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TestFeature : ScriptableRendererFeature
{
    class TestPass : ScriptableRenderPass
    {
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) =>
            m_PushedAction?.Invoke(context, renderingData);
    }

    static event System.Action<ScriptableRenderContext, RenderingData> m_PushedAction;
    static RenderPassEvent m_RenderPassEvent;

    public static void PushAction (System.Action<ScriptableRenderContext, RenderingData> action, RenderPassEvent renderPassEvent)
    {
        m_PushedAction = action;
        m_RenderPassEvent = renderPassEvent;
    }

    public static void PopAction () => m_PushedAction = null;

    public override void Create() {}
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) =>
        renderer.EnqueuePass(new TestPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
        });
}
