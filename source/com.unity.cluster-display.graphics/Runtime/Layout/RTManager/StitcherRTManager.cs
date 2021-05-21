using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class StitcherRTManager
    {
        public abstract RTType type { get; }
        protected abstract object BlitRT(int tileCount, int tileIndex, int width, int height, GraphicsFormat format);
        protected abstract object PresentRT(int width, int height, GraphicsFormat format);
        public abstract void Release();

        public RenderTexture BlitRenderTexture (int tileCount, int tileIndex, int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
            var rt = BlitRT(tileCount, tileIndex, width, height, format);
            if (!(rt is RenderTexture))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RenderTexture;
        }

        public RTHandle BlitRTHandle (int tileCount, int tileIndex, int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
            var rt = BlitRT(tileCount, tileIndex, width, height, format);
            if (!(rt is RTHandle))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RTHandle;
        }

        public RenderTexture PresentRenderTexture (int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
            var rt = PresentRT(width, height, format);
            if (!(rt is RenderTexture))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RenderTexture;
        }

        public RTHandle PresentRTHandle (int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
            var rt = PresentRT(width, height, format);
            if (!(rt is RTHandle))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RTHandle;
        }
    }
}
