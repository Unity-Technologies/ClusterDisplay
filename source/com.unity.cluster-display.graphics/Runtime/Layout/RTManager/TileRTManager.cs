using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class TileRTManager<T>
    {
        protected T m_SourceRT;
        protected T m_PresentRT;
        protected T m_BackBufferRT;

        public abstract T GetSourceRT(int width, int height, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB);
        public abstract T GetPresentRT(int width, int height, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB);
        public abstract T GetBackBufferRT(int width, int height, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB);
        public abstract void Release();

        /*
        public RenderTexture GetSourceRenderTexture (int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
            var rt = GetBlitRT(width, height, format);
            if (!(rt is RenderTexture))
                throw new System.InvalidOperationException("RT is not a RenderTexture.");
            return rt as RenderTexture;
        }

        public RTHandle GetBlitRTHandle (int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
            var rt = GetBlitRT(width, height, format);
            if (!(rt is RTHandle))
                throw new System.InvalidOperationException("RT is not a RTHandle.");
            return rt as RTHandle;
        }

        public RenderTexture GetPresentRenderTexture (int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
            var rt = GetPresentRT(width, height, format);
            if (!(rt is RenderTexture))
                throw new System.InvalidOperationException("RT is not a RenderTexture.");
            return rt as RenderTexture;
        }

        public RTHandle GetPresentRTHandle (int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
            var rt = GetPresentRT(width, height, format);
            if (!(rt is RTHandle))
                throw new System.InvalidOperationException("RT is not a RTHandle.");
            return rt as RTHandle;
        }

        public RenderTexture GetBackBufferRenderTexture (int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
            var rt = GetBackBufferRT(width, height, format);
            if (!(rt is RenderTexture))
                throw new System.InvalidOperationException("RT is not a RenderTexture.");
            return rt as RenderTexture;
        }

        public RTHandle GetBackBufferRTHandle (int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
            var rt = GetBackBufferRT(width, height, format);
            if (!(rt is RTHandle))
                throw new System.InvalidOperationException("RT is not a RTHandle.");
            return rt as RTHandle;
        }
        */
    }
}
