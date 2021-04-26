using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

public class StandardTileRTManager : TileRTManager
{
    public override RTType Type => RTType.RenderTexture;
    private RenderTexture m_BlitRT;
    private RenderTexture m_PresentRT;

    protected override object BlitRT(int width, int height, GraphicsFormat format)
    {
        bool resized = 
            m_BlitRT != null && 
            (m_BlitRT.width != (int)width || 
            m_BlitRT.height != (int)height);

        if (m_BlitRT == null || resized || m_BlitRT.graphicsFormat != format)
        {
            if (m_BlitRT != null)
            {
                // if (camera.targetTexture != null && camera.targetTexture == m_BlitRT)
                //     camera.targetTexture = null;
                m_BlitRT.Release();
            }

            m_BlitRT = new RenderTexture(width, height, 1, format, 0);
            m_BlitRT.name = $"Tile-RT-({m_BlitRT.width}X{m_BlitRT.height})";
        }

        return m_BlitRT;
    }

    protected override object PresentRT(int width, int height, GraphicsFormat format)
    {
        bool resized = 
            m_PresentRT != null && 
            (m_PresentRT.width != (int)width || 
            m_PresentRT.height != (int)height);

        if (m_PresentRT == null || resized || m_PresentRT.graphicsFormat != format)
        {
            if (m_PresentRT != null)
                m_PresentRT.Release();

            m_PresentRT = new RenderTexture(width, height, 1, format, 0);
            m_PresentRT.name = $"Present-RT-({m_PresentRT.width}X{m_PresentRT.height})";
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
