using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public class XRStitcherRTManager : StitcherRTManager<RTHandle>
    {
        private void PollRTs (int tileCount)
        {
            if (m_SourceRTs != null && m_SourceRTs.Length == tileCount)
                return;
            m_SourceRTs = new RTHandle[tileCount];
        }

        public override RTHandle GetSourceRT(int tileCount, int tileIndex, int width, int height, GraphicsFormat format = defaultFormat)
        {
            PollRTs(tileCount);

            bool resized = m_SourceRTs[tileIndex] != null && 
                (m_SourceRTs[tileIndex].rt.width != (int)width || 
                m_SourceRTs[tileIndex].rt.height != (int)height);

            if (m_SourceRTs[tileIndex] == null || resized)
            {
                if (m_SourceRTs[tileIndex] != null)
                    RTHandles.Release(m_SourceRTs[tileIndex]);

                m_SourceRTs[tileIndex] = RTHandles.Alloc(
                    width: (int)width,
                    height: (int)height,
                    slices: 1,
                    dimension: TextureXR.dimension,
                    useDynamicScale: false,
                    autoGenerateMips: false,
                    enableRandomWrite: true,
                    filterMode: FilterMode.Trilinear,
                    anisoLevel: 8,
                    // msaaSamples: MSAASamples.MSAA8x,
                    name: $"Source-{tileIndex}-RT-({width}X{height})");
            }

            return m_SourceRTs[tileIndex];
        }

        public override RTHandle GetPresentRT(int width, int height, GraphicsFormat format = defaultFormat)
        {
            bool resized = 
                m_PresentRT != null && 
                (m_PresentRT.rt.width != width || 
                m_PresentRT.rt.height != height);

            if (m_PresentRT == null || resized)
            {
                if (m_PresentRT != null)
                    RTHandles.Release(m_PresentRT);

                m_PresentRT = RTHandles.Alloc(
                    width: (int)width, 
                    height: (int)height,
                    slices: 1,
                    useDynamicScale: true,
                    autoGenerateMips: false,
                    enableRandomWrite: true,
                    filterMode: FilterMode.Trilinear,
                    anisoLevel: 8,
                    name: $"Present-RT-({width}X{height})");
            }

            return m_PresentRT;
        }

        public override void Release()
        {
            if (m_PresentRT != null)
                RTHandles.Release(m_PresentRT);

            if (m_SourceRTs != null)
            {
                for (var i = 0; i != m_SourceRTs.Length; ++i)
                {
                    RTHandles.Release(m_SourceRTs[i]);
                    m_SourceRTs[i] = null;
                }
            }

            m_PresentRT = null;
            m_SourceRTs = null;
        }
    }
}
