using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class TileRTManager<T> : RTManager
    {
        protected T m_SourceRT;
        protected T m_PresentRT;
        protected T m_BackBufferRT;

        public abstract T GetSourceRT(int width, int height, GraphicsFormat format = defaultFormat);
        public abstract T GetPresentRT(int width, int height, GraphicsFormat format = defaultFormat);
        public abstract T GetBackBufferRT(int width, int height, GraphicsFormat format = defaultFormat);
        public abstract void Release();
    }
}
