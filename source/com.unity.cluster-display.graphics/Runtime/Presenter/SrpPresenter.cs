#if CLUSTER_DISPLAY_URP || CLUSTER_DISPLAY_HDRP
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    abstract class SrpPresenter
    {
        const string k_CommandBufferName = "Present To Screen";

        protected Camera m_Camera;
        protected Color m_ClearColor;
        protected RTHandle m_IntermediateTarget;

        public virtual void Disable()
        {
            m_IntermediateTarget?.Release();
        }

        protected void DoPresent(ScriptableRenderContext context, RTHandle backBuffer)
        {
            var cmd = CommandBufferPool.Get(k_CommandBufferName);

            RenderPresent(context, cmd, backBuffer, m_Camera.pixelRect);
            
            GraphicsUtil.ExecuteCaptureIfNeeded(cmd, m_Camera, m_ClearColor, GetPresentAction(), false);
        }

        protected void DoPresentDelayed(ScriptableRenderContext context, RTHandle backBuffer)
        {
            RTHandlesUtil.ReAllocateIfNeeded(ref m_IntermediateTarget, backBuffer.rt.descriptor, FilterMode.Point, TextureWrapMode.Clamp);

            var cmd = CommandBufferPool.Get(k_CommandBufferName);

            Blitter.BlitCameraTexture(cmd, m_IntermediateTarget, backBuffer);

            RenderPresent(context, cmd, m_IntermediateTarget, m_Camera.pixelRect);
            
            GraphicsUtil.ExecuteCaptureIfNeeded(cmd, m_Camera, m_IntermediateTarget);
        }

        void RenderPresent(ScriptableRenderContext context, CommandBuffer cmd, RTHandle target, Rect pixelRect)
        {
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(true, true, m_ClearColor);

            GetPresentAction().Invoke(new PresentArgs
            {
                CommandBuffer = cmd,
                FlipY = false,
                CameraPixelRect = pixelRect
            });

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        protected abstract Action<PresentArgs> GetPresentAction();
    }
}
#endif
