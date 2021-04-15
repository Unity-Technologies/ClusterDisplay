using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StandardTileRTManager : TileRTManager
{
    public override RTType Type => RTType.RenderTexture;
    private RenderTexture m_BlitRT;
    private RenderTexture m_PresentRT;

    protected override object BlitRT(int width, int height)
    {
        bool resized = 
            m_BlitRT != null && 
            (m_BlitRT.width != (int)width || 
            m_BlitRT.height != (int)height);

        if (m_BlitRT == null || resized)
        {
            if (m_BlitRT != null)
            {
                // if (camera.targetTexture != null && camera.targetTexture == m_BlitRT)
                //     camera.targetTexture = null;
                m_BlitRT.Release();
            }

            m_BlitRT = new RenderTexture(width, height, 1, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm);
            m_BlitRT.name = $"TileLayoutRT-({m_BlitRT.width}X{m_BlitRT.height})";
        }

        return m_BlitRT;
    }

    protected override object PresentRT(int width, int height)
    {
        bool resized = 
            m_PresentRT != null && 
            (m_PresentRT.width != (int)width || 
            m_PresentRT.height != (int)height);

        if (m_PresentRT == null || resized)
        {
            if (m_PresentRT != null)
                m_PresentRT.Release();

            m_PresentRT = new RenderTexture(width, height, 1, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB);
            m_PresentRT.name = $"PresentRT-({m_PresentRT.width}X{m_PresentRT.height})";
        }

        return m_PresentRT;
    }

    public override void Release()
    {
        if (m_BlitRT != null)
            m_BlitRT.Release();
        if (m_PresentRT != null)
            m_PresentRT.Release();

        m_BlitRT = null;
        m_PresentRT = null;
    }
}
