using System;
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
            AllocateIfNeeded(ref m_SourceRT, "Source", width, height, format);
            return m_SourceRT;
        }

        public override RenderTexture GetPresentRT(int width, int height, GraphicsFormat format = defaultFormat)
        {
            AllocateIfNeeded(ref m_PresentRT, "Present", width, height, format);
            return m_PresentRT;
        }

        public override RenderTexture GetBackBufferRT(int width, int height, GraphicsFormat format = defaultFormat)
        {
            AllocateIfNeeded(ref m_BackBufferRT, "Backbuffer", width, height, format);
            return m_BackBufferRT;
        }

        public override void Release()
        {
            DeallocateIfNeeded(ref m_SourceRT);
            DeallocateIfNeeded(ref m_PresentRT);
            DeallocateIfNeeded(ref m_BackBufferRT);
        }
        
        static bool AllocateIfNeeded(ref RenderTexture rt, string name, int width, int height, GraphicsFormat format = defaultFormat)
        {
            if (rt == null || 
                rt.width != width || 
                rt.height != height || 
                rt.graphicsFormat != format)
            {
                if (rt != null)
                {
                    rt.Release();
                }

                rt = new RenderTexture(width, height, 1, format, 0)
                {
                    name = $"{name}-{width}X{height}"
                };
                return true;
            }

            return false;
        }

        static void DeallocateIfNeeded(ref RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
            }

            rt = null;
        }
    }
}
