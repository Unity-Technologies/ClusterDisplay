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
    public class StandardTileRTManager : TileRTManager
    {
        public override RTType type => RTType.RenderTexture;
        private RenderTexture m_BlitRT;
        private RenderTexture m_PresentRT;
        private RenderTexture m_BackBufferRT;

        protected override object BlitRT(int width, int height, GraphicsFormat format)
        {
            bool resized = 
                m_BlitRT != null && 
                (m_BlitRT.width != (int)width || 
                m_BlitRT.height != (int)height);

            if (m_BlitRT == null || resized || m_BlitRT.graphicsFormat != format)
            {
                if (m_BlitRT != null)
                    m_BlitRT.Release();

                m_BlitRT = new RenderTexture(width, height, 1, format, 0);
                m_BlitRT.name = $"Tile-RT-({m_BlitRT.width}X{m_BlitRT.height})";
                // Debug.Log("Resizing tile RT.");
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
                // Debug.Log("Resizing present RT.");
            }

            return m_PresentRT;
        }

        protected override object BackBufferRT(int width, int height, GraphicsFormat format)
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
            if (m_BlitRT != null)
                m_BlitRT.Release();

            if (m_PresentRT != null)
                m_PresentRT.Release();

            if (m_BackBufferRT != null)
                m_BackBufferRT.Release();

            m_BlitRT = null;
            m_PresentRT = null;
            m_BackBufferRT = null;
        }
    }
}
