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
            AllocateIfNeeded(ref m_SourceRT, "Source", width, height);
            return m_SourceRT;
        }

        public override RTHandle GetPresentRT(int width, int height, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB)
        {
            AllocateIfNeeded(ref m_PresentRT, "Present", width, height);
            return m_PresentRT;
        }

        public override RTHandle GetBackBufferRT(int width, int height, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB)
        {
            AllocateIfNeeded(ref m_BackBufferRT, "Backbuffer", width, height);
            return m_BackBufferRT;
        }

        public override void Release()
        {
            DeallocateIfNeeded(ref m_SourceRT);
            DeallocateIfNeeded(ref m_PresentRT);
            DeallocateIfNeeded(ref m_BackBufferRT);
        }
        
        static bool AllocateIfNeeded(ref RTHandle rt, string name, int width, int height)
        {
            if (rt == null || 
                rt.rt.width != width || 
                rt.rt.height != height)
            {
                if (rt != null)
                {
                    RTHandles.Release(rt);
                }

                rt = RTHandles.Alloc(
                    width,
                    height,
                    1,
                    useDynamicScale: true,
                    autoGenerateMips: false,
                    enableRandomWrite: true,
                    filterMode: FilterMode.Trilinear,
                    anisoLevel: 8,
                    name: $"{name}-({width}X{height})");
                
                return true;
            }
            
            return false;
        }

        static void DeallocateIfNeeded(ref RTHandle rt)
        {
            if (rt != null)
            {
                RTHandles.Release(rt);
            }

            rt = null;
        }
    }
}
