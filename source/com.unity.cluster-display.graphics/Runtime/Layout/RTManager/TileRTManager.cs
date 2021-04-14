using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum RTType
{
    Handle,
    RenderTexture
}

public abstract class TileRTManager
{
    public abstract RTType Type { get; }
    protected abstract object BlitRT(int width, int height);
    protected abstract object PresentRT(int width, int height);
    public abstract void Release();
    public RenderTexture BlitRenderTexture (int width, int height)
    {
        var rt = BlitRT(width, height);
        if (!(rt is RenderTexture))
            throw new System.Exception("Blit RT is not a RenderTexture.");
        return rt as RenderTexture;
    }

    public RTHandle BlitRTHandle (int width, int height)
    {
        var rt = BlitRT(width, height);
        if (!(rt is RTHandle))
            throw new System.Exception("Blit RT is not a RenderTexture.");
        return rt as RTHandle;
    }

    public RenderTexture PresentRenderTexture (int width, int height)
    {
        var rt = PresentRT(width, height);
        if (!(rt is RenderTexture))
            throw new System.Exception("Blit RT is not a RenderTexture.");
        return rt as RenderTexture;
    }

    public RTHandle PresentRTHandle (int width, int height)
    {
        var rt = PresentRT(width, height);
        if (!(rt is RTHandle))
            throw new System.Exception("Blit RT is not a RenderTexture.");
        return rt as RTHandle;
    }
}
