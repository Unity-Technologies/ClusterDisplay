using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class StitcherRTManager<T>
    {
        protected T m_PresentRT;
        protected T[] m_SourceRTs;

        public abstract T GetSourceRT(int tileCount, int tileIndex, int width, int height);
        public abstract T GetPresentRT(int width, int height);
        public abstract void Release();
    }
}
