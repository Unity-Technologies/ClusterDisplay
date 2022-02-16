#if CLUSTER_DISPLAY_URP
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.ClusterDisplay.Graphics
{
    class UrpPresenter : SrpPresenter, IPresenter
    {
        public event Action<PresentArgs> Present = delegate { };

        bool m_Delayed;

        public Color ClearColor
        {
            set => m_ClearColor = value;
        }

        public Camera Camera => m_Camera;

        protected override Action<PresentArgs> GetPresentAction() => Present;

        public override void Disable()
        {
            if (m_Delayed)
            {
                InjectionPointRenderPass.ExecuteRender -= ExecuteRenderDelayed;
            }
            else
            {
                InjectionPointRenderPass.ExecuteRender -= ExecuteRender;
            }

            base.Disable();
        }

        public void Enable(GameObject gameObject, bool delayByOneFrame)
        {
            m_Camera = gameObject.GetOrAddComponent<Camera>();
            m_Camera.hideFlags = HideFlags.NotEditable | HideFlags.DontSave;

            // We use the camera to blit to screen.
            // Configure it to minimize wasteful rendering.
            m_Camera.targetTexture = null;
            m_Camera.cullingMask = 0;
            m_Camera.clearFlags = CameraClearFlags.Nothing;
            m_Camera.depthTextureMode = DepthTextureMode.None;

            m_Delayed = delayByOneFrame;

            if (m_Delayed)
            {
                InjectionPointRenderPass.ExecuteRender += ExecuteRenderDelayed;
            }
            else
            {
                InjectionPointRenderPass.ExecuteRender += ExecuteRender;
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
