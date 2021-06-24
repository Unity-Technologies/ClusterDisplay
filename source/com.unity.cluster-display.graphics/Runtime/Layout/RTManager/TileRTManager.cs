using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class TileRTManager<T>
    {
        protected T m_SourceRT;
        protected T m_PresentRT;
        protected T m_BackBufferRT;

        public abstract T GetSourceRT(int width, int height, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB);
        public abstract T GetPresentRT(int width, int height, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB);
        public abstract T GetBackBufferRT(int width, int height, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB);
        public abstract void Release();
    }
}
