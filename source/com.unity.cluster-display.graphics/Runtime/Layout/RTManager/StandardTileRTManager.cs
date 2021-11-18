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
                m_SourceRT.name = $"Source-RT-({m_SourceRT.width}X{m_SourceRT.height})";
                // ClusterDebug.Log("Resizing tile RT.");
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
                // ClusterDebug.Log("Resizing present RT.");
            }

            return m_PresentRT;
        }

        public override RenderTexture GetQueuedFrameRT(int width, int height, int currentQueuedFrameCount, GraphicsFormat format = defaultFormat)
        {
            PollQueuedFrameCount(currentQueuedFrameCount);
            long index = backBufferRTIndex % currentQueuedFrameCount;
            var backBufferRT = m_QueuedFrameRTs[index];

            bool resized = 
                backBufferRT != null && 
                (backBufferRT.width != (int)width || 
                backBufferRT.height != (int)height);

            if (backBufferRT == null || resized || backBufferRT.graphicsFormat != format)
            {
                if (backBufferRT != null)
                    backBufferRT.Release();

                m_QueuedFrameRTs[index] = backBufferRT = new RenderTexture(width, height, 1, format, 0);
                backBufferRT.name = $"BackBuffer-RT-({backBufferRT.width}X{backBufferRT.height})";
                // ClusterDebug.Log("Resizing back buffer RT.");
            }

            backBufferRTIndex++;
            return backBufferRT;
        }

        public override void Release()
        {
            if (m_SourceRT != null)
                m_SourceRT.Release();

            if (m_PresentRT != null)
                m_PresentRT.Release();

            if (m_QueuedFrameRTs != null && m_QueuedFrameRTs.Length != 0)
            {
                for (int i = 0; i < m_QueuedFrameRTs.Length; i++)
                {
                    if (m_QueuedFrameRTs[i] != null)
                    {
                        m_QueuedFrameRTs[i].Release();
                        m_QueuedFrameRTs[i] = null;
                    }
                }
            }

            m_SourceRT = null;
            m_PresentRT = null;
            m_QueuedFrameRTs = null;
        }

    }
}
