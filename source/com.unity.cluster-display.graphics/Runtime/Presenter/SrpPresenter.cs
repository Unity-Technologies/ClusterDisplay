#if CLUSTER_DISPLAY_URP || CLUSTER_DISPLAY_HDRP
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Unity.ClusterDisplay.Graphics
{
    abstract class SrpPresenter
    {
        const string k_CommandBufferName = "Present To Screen";

        protected bool m_Delayed;
        protected bool m_Enabled;
        protected Camera m_Camera;
        protected Color m_ClearColor;
        protected RTHandle m_IntermediateTarget;

        public event Action<PresentArgs> Present = delegate { };

        public void SetDelayed(bool value)
        {
            if (value != m_Delayed)
            {
                if (m_Enabled)
                {
                    Unbind(m_Delayed);
                    m_Delayed = value;
                    Bind(m_Delayed);
                }
                else
                {
                    m_Delayed = value;
                }
            }
        }

        public virtual void Enable(GameObject gameObject)
        {
            m_Enabled = true;
            Bind(m_Delayed);
        }

        public virtual void Disable()
        {
            m_Enabled = false;
            Unbind(m_Delayed);
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
            //cmd.ClearRenderTarget(true, true, m_ClearColor);
            cmd.ClearRenderTarget(true, true, Color.HSVToRGB(Random.value, 0.5f, 0.5f));

            Present.Invoke(new PresentArgs
            {
                CommandBuffer = cmd,
                FlipY = flipY,
                CameraPixelRect = pixelRect
            });

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        protected abstract void Bind(bool delayed);

        protected abstract void Unbind(bool delayed);
    }
}
#endif
