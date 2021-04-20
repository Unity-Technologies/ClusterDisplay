using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardStitcherRTManager : StitcherRTManager
    {
        public override RTType Type => RTType.RenderTexture;
        private RenderTexture m_PresentRT;
        private RenderTexture[] m_BlitRTs;

        private void PollRTs (int tileCount)
        {
            if (m_BlitRTs != null && m_BlitRTs.Length == tileCount)
                return;
            m_BlitRTs = new RenderTexture[tileCount];
        }

        protected override object BlitRT(int tileCount, int tileIndex, int width, int height, GraphicsFormat format)
        {
            PollRTs(tileCount);

            bool resized = m_BlitRTs[tileIndex] != null && 
                (m_BlitRTs[tileIndex].width != (int)width || 
                m_BlitRTs[tileIndex].height != (int)height);

            if (m_BlitRTs[tileIndex] == null || resized || m_BlitRTs[tileIndex].graphicsFormat != format)
            {
                if (m_BlitRTs[tileIndex] != null)
                    m_BlitRTs[tileIndex].Release();

                m_BlitRTs[tileIndex] = new RenderTexture(width, height, 1, format);
                m_BlitRTs[tileIndex].name = $"Tile-{tileIndex}-RT-({m_BlitRTs[tileIndex].width}X{m_BlitRTs[tileIndex].height})";
            }

            return m_BlitRTs[tileIndex];
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

                m_PresentRT = new RenderTexture(width, height, 1, format);
                m_PresentRT.name = $"PresentRT-({m_PresentRT.width}X{m_PresentRT.height})";
            }

            return m_PresentRT;
        }

        public override void Release()
        {
            if (m_PresentRT != null)
                m_PresentRT.Release();

            if (m_BlitRTs != null)
            {
                for (var i = 0; i != m_BlitRTs.Length; ++i)
                {
                    m_BlitRTs[i].Release();
                    m_BlitRTs[i] = null;
                }
            }

            m_PresentRT = null;
            m_BlitRTs = null;
        }
    }
}
