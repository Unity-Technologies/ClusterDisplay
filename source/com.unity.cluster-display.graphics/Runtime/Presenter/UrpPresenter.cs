#if CLUSTER_DISPLAY_URP
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.ClusterDisplay.Graphics
{
    class UrpPresenter : SrpPresenter, IPresenter
    {
        public Color ClearColor
        {
            set => m_ClearColor = value;
        }

        public Camera Camera => m_Camera;

        public override void Enable(GameObject gameObject)
        {
            m_Camera = gameObject.GetOrAddComponent<Camera>();
            m_Camera.hideFlags = HideFlags.NotEditable | HideFlags.DontSave | HideFlags.HideInHierarchy;

            // We use the camera to blit to screen.
            // Configure it to minimize wasteful rendering.
            m_Camera.targetTexture = null;
            m_Camera.cullingMask = 0;
            m_Camera.clearFlags = CameraClearFlags.Nothing;
            m_Camera.depthTextureMode = DepthTextureMode.None;

            base.Enable(gameObject);
        }

        protected override void Bind(bool delayed)
        {
            if (delayed)
            {
                InjectionPointRenderPass.ExecuteRender += ExecuteRenderDelayed;
            }
            else
            {
                InjectionPointRenderPass.ExecuteRender += ExecuteRender;
            }
        }

        protected override void Unbind(bool delayed)
        {
            if (delayed)
            {
                InjectionPointRenderPass.ExecuteRender -= ExecuteRenderDelayed;
            }
            else
            {
                InjectionPointRenderPass.ExecuteRender -= ExecuteRender;
            }
        }

        static RTHandle GetBackBuffer(RenderingData renderingData) => renderingData.cameraData.renderer.cameraColorTargetHandle;

        void ExecuteRender(ScriptableRenderContext context, RenderingData renderingData)
        {
            // The render pass gets invoked for all cameras so we need to filter.
            if (renderingData.cameraData.camera == m_Camera)
            {
                DoPresent(context, GetBackBuffer(renderingData), false);
            }
        }

        void ExecuteRenderDelayed(ScriptableRenderContext context, RenderingData renderingData)
        {
            // The render pass gets invoked for all cameras so we need to filter.
            if (renderingData.cameraData.camera == m_Camera)
            {
                DoPresentDelayed(context, GetBackBuffer(renderingData), false);
            }
        }
    }
}
#endif
