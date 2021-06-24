using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class StitcherRTManager<T>
    {
        protected T m_PresentRT;
        protected T[] m_SourceRTs;

        public abstract T GetSourceRT(int tileCount, int tileIndex, int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB);
        public abstract T GetPresentRT(int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB);
        public abstract void Release();
    }
}
