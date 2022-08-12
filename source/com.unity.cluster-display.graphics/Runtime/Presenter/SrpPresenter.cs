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
        static readonly Vector4 k_IdentityScaleBias = new Vector4(1, 1, 0, 0);

        protected bool m_Delayed;
        protected bool m_Enabled;
        protected Camera m_Camera;
        protected Color m_ClearColor;

        readonly DoubleBuffer m_Buffers = new DoubleBuffer();

        class DoubleBuffer
        {
            RTHandle m_IntermediateTargetA;
            RTHandle m_IntermediateTargetB;
            bool m_Toggle;

            public void ReAllocateIfNeeded(RenderTextureDescriptor descriptor)
            {
                RTHandlesUtil.ReAllocateIfNeeded(ref m_IntermediateTargetA, descriptor, FilterMode.Point, TextureWrapMode.Clamp);
                RTHandlesUtil.ReAllocateIfNeeded(ref m_IntermediateTargetB, descriptor, FilterMode.Point, TextureWrapMode.Clamp);
            }

            public void Dispose()
            {
                m_IntermediateTargetA?.Release();
                m_IntermediateTargetB?.Release();
            }

            public RTHandle Front => m_Toggle ? m_IntermediateTargetA : m_IntermediateTargetB;
            public RTHandle Back => m_Toggle ? m_IntermediateTargetB : m_IntermediateTargetA;

            public void Swap() => m_Toggle = !m_Toggle;
        }

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
            m_Buffers.Dispose();
        }

        protected void DoPresent(ScriptableRenderContext context, RTHandle backBuffer, bool flipY)
        {
            var cmd = CommandBufferPool.Get(k_CommandBufferName);

            RenderPresent(cmd, backBuffer, m_Camera.pixelRect, flipY);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        protected void DoPresentDelayed(ScriptableRenderContext context, RTHandle backBuffer, bool flipY)
        {
            DoPresentDelayed(context, backBuffer.rt.descriptor, backBuffer, flipY);
        }

        protected void DoPresentDelayed(ScriptableRenderContext context, RenderTextureDescriptor descriptor, RTHandle backBuffer, bool flipY)
        {
            m_Buffers.ReAllocateIfNeeded(descriptor);

            var cmd = CommandBufferPool.Get(k_CommandBufferName);

            RenderPresent(cmd, m_Buffers.Back, m_Camera.pixelRect, flipY);
            CoreUtils.SetRenderTarget(cmd, backBuffer);
            Blitter.BlitTexture(cmd, m_Buffers.Front, k_IdentityScaleBias, 0, false);

            m_Buffers.Swap();

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void RenderPresent(CommandBuffer cmd, RTHandle target, Rect pixelRect, bool flipY)
        {
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(true, true, m_ClearColor);

            Present.Invoke(new PresentArgs
            {
                CommandBuffer = cmd,
                FlipY = flipY,
                CameraPixelRect = pixelRect,
                BackBuffer = target
            });
        }

        protected abstract void Bind(bool delayed);

        protected abstract void Unbind(bool delayed);
    }
}
#endif
