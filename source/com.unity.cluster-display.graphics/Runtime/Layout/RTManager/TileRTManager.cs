﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public enum RTType
    {
        Handle,
        RenderTexture
    }

    public abstract class TileRTManager
    {
        public abstract RTType type { get; }
        protected abstract object GetBlitRT(int width, int height, GraphicsFormat format);
        protected abstract object GetPresentRT(int width, int height, GraphicsFormat format);
        protected abstract object GetBackBufferRT(int width, int height, GraphicsFormat format);
        public abstract void Release();

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

    }
}
