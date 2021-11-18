using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public class XRTileRTManager : TileRTManager<RTHandle>
    {
        public override RTHandle GetSourceRT(int width, int height, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB)
        {
            bool resized = 
                m_SourceRT != null && 
                (m_SourceRT.rt.width != (int)width || 
                m_SourceRT.rt.height != (int)height);

            if (m_SourceRT == null || resized)
            {
                if (m_SourceRT != null)
                    RTHandles.Release(m_SourceRT);

                m_SourceRT = RTHandles.Alloc(
                    width: (int)width, 
                    height: (int)height, 
                    slices: 1, 
                    useDynamicScale: true, 
                    autoGenerateMips: false, 
                    enableRandomWrite: true,
                    filterMode: FilterMode.Trilinear,
                    anisoLevel: 8,
                    name: "Overscanned Target");
            }

            return m_SourceRT;
        }

        public override RTHandle GetPresentRT(int width, int height, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB)
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

        public override RTHandle GetQueuedFrameRT(int width, int height, int currentQueuedFrameCount, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB)
        {
            PollQueuedFrameCount(currentQueuedFrameCount);
            long index = backBufferRTIndex % 2;
            var backBufferRT = m_QueuedFrameRTs[index];

            bool resized = 
                backBufferRT != null && 
                (backBufferRT.rt.width != width || 
                backBufferRT.rt.height != height);

            if (backBufferRT == null || resized)
            {
                if (backBufferRT != null)
                    RTHandles.Release(backBufferRT);

                m_QueuedFrameRTs[index] = backBufferRT = RTHandles.Alloc(
                    width: (int)width, 
                    height: (int)height,
                    slices: 1,
                    useDynamicScale: true,
                    autoGenerateMips: false,
                    enableRandomWrite: true,
                    filterMode: FilterMode.Trilinear,
                    anisoLevel: 8,
                    name: $"BackBuffer-RT-({width}X{height})");
            }

            backBufferRTIndex++;
            return backBufferRT;
        }

        public override void Release()
        {
            if (m_SourceRT != null)
                RTHandles.Release(m_SourceRT);

            if (m_PresentRT != null)
                RTHandles.Release(m_PresentRT);

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
