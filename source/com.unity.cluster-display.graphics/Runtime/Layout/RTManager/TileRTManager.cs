using System.Collections;
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
    protected abstract object BlitRT(int width, int height, GraphicsFormat format);
    protected abstract object PresentRT(int width, int height, GraphicsFormat format);
        public abstract void Release();
    public RenderTexture BlitRenderTexture (int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
        var rt = BlitRT(width, height, format);
            if (!(rt is RenderTexture))
                throw new System.Exception("Blit RT is not a RenderTexture.");
            return rt as RenderTexture;
        }

    public RTHandle BlitRTHandle (int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
        var rt = BlitRT(width, height, format);
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
