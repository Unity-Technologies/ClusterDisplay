using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Manages the RenderTextures for tile layout rendering. Unfortunately setting Camera.targetTexture to 
    /// RTHandle seems to be inconsitent. Therefore we use RenderTexture instead.
    /// </summary>
    public class StandardTileRTManager : TileRTManager<RenderTexture>
    {
        public override RenderTexture GetSourceRT(int width, int height, GraphicsFormat format = defaultFormat)
        {
            bool resized = 
                m_SourceRT != null && 
                (m_SourceRT.width != (int)width || 
                m_SourceRT.height != (int)height);

            if (m_SourceRT == null || resized || m_SourceRT.graphicsFormat != format)
            {
                if (m_SourceRT != null)
                    m_SourceRT.Release();

                m_SourceRT = new RenderTexture(width, height, 1, format, 0);
                m_SourceRT.name = $"Tile-RT-({m_SourceRT.width}X{m_SourceRT.height})";
                // Debug.Log("Resizing tile RT.");
            }

            return m_SourceRT;
        }

        public override RenderTexture GetPresentRT(int width, int height, GraphicsFormat format = defaultFormat)
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
                // Debug.Log("Resizing present RT.");
            }

            return m_PresentRT;
        }

        public override RenderTexture GetBackBufferRT(int width, int height, GraphicsFormat format = defaultFormat)
        {
            bool resized = 
                m_BackBufferRT != null && 
                (m_BackBufferRT.width != (int)width || 
                m_BackBufferRT.height != (int)height);

            if (m_BackBufferRT == null || resized || m_BackBufferRT.graphicsFormat != format)
            {
                if (m_BackBufferRT != null)
                    m_BackBufferRT.Release();

                m_BackBufferRT = new RenderTexture(width, height, 1, format, 0);
                m_BackBufferRT.name = $"Present-RT-({m_BackBufferRT.width}X{m_BackBufferRT.height})";
                // Debug.Log("Resizing back buffer RT.");
            }

            return m_BackBufferRT;
        }

        public override void Release()
        {
            if (m_SourceRT != null)
                m_SourceRT.Release();

            if (m_PresentRT != null)
                m_PresentRT.Release();

            if (m_BackBufferRT != null)
                m_BackBufferRT.Release();

            m_SourceRT = null;
            m_PresentRT = null;
            m_BackBufferRT = null;
        }

    }
}
