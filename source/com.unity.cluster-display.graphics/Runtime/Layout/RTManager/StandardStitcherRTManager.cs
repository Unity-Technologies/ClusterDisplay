using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Manages the set of RenderTextures for stitcher layout rendering. Unfortunately setting Camera.targetTexture to 
    /// RTHandle seems to be inconsitent. Therefore we use RenderTexture instead.
    /// </summary>
    public class StandardStitcherRTManager : StitcherRTManager<RenderTexture>
    {
        private void PollRTs (int tileCount)
        {
            if (m_SourceRTs != null && m_SourceRTs.Length == tileCount)
                return;
            m_SourceRTs = new RenderTexture[tileCount];
        }

        public override RenderTexture GetSourceRT(int tileCount, int tileIndex, int width, int height)
        {
            PollRTs(tileCount);

            bool resized = m_SourceRTs[tileIndex] != null && 
                (m_SourceRTs[tileIndex].width != (int)width || 
                m_SourceRTs[tileIndex].height != (int)height);

            if (m_SourceRTs[tileIndex] == null || resized)
            {
                if (m_SourceRTs[tileIndex] != null)
                    m_SourceRTs[tileIndex].Release();

                m_SourceRTs[tileIndex] = new RenderTexture(width, height, 1, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm);
                m_SourceRTs[tileIndex].name = $"Tile-{tileIndex}-RT-({m_SourceRTs[tileIndex].width}X{m_SourceRTs[tileIndex].height})";
            }

            return m_SourceRTs[tileIndex];
        }

        public override RenderTexture GetPresentRT(int width, int height)
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
            if (m_PresentRT != null)
                m_PresentRT.Release();

            if (m_SourceRTs != null)
            {
                for (var i = 0; i != m_SourceRTs.Length; ++i)
                {
                    m_SourceRTs[i].Release();
                    m_SourceRTs[i] = null;
                }
            }

            m_PresentRT = null;
            m_SourceRTs = null;
        }
    }
}
