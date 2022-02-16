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

        protected void DoPresent(ScriptableRenderContext context, RTHandle backBuffer, bool flipY)
        {
            var cmd = CommandBufferPool.Get(k_CommandBufferName);

            RenderPresent(context, cmd, backBuffer, m_Camera.pixelRect, flipY);
        }

        protected void DoPresentDelayed(ScriptableRenderContext context, RTHandle backBuffer, bool flipY)
        {
            DoPresentDelayed(context, backBuffer.rt.descriptor, backBuffer, flipY);
        }

        protected void DoPresentDelayed(ScriptableRenderContext context, RenderTextureDescriptor intermediateTargetDescriptor, RTHandle backBuffer, bool flipY)
        {
            RTHandlesUtil.ReAllocateIfNeeded(ref m_IntermediateTarget, intermediateTargetDescriptor, FilterMode.Point, TextureWrapMode.Clamp);

            var cmd = CommandBufferPool.Get(k_CommandBufferName);

            //Blitter.BlitCameraTexture(cmd, m_IntermediateTarget, backBuffer);
            CoreUtils.SetRenderTarget(cmd, backBuffer);
            Blitter.BlitTexture(cmd, m_IntermediateTarget, new Vector4(1, 1, 0, 0), 0, false);
            RenderPresent(context, cmd, m_IntermediateTarget, m_Camera.pixelRect, flipY);
        }

        void RenderPresent(ScriptableRenderContext context, CommandBuffer cmd, RTHandle target, Rect pixelRect, bool flipY)
        {
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(true, true, m_ClearColor);

            GetPresentAction().Invoke(new PresentArgs
            {
                CommandBuffer = cmd,
                FlipY = flipY,
                CameraPixelRect = pixelRect
            });

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        protected abstract Action<PresentArgs> GetPresentAction();
    }
}
#endif
