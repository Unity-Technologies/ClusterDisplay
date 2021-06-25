using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class StitcherRTManager<T> : RTManager
    {
        protected T m_PresentRT;
        protected T[] m_SourceRTs;

        public abstract T GetSourceRT(int tileCount, int tileIndex, int width, int height, GraphicsFormat format = defaultFormat);
        public abstract T GetPresentRT(int width, int height, GraphicsFormat format = defaultFormat);
        public abstract void Release();
    }
}
