using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class RTManager
    {
        protected const GraphicsFormat defaultFormat = GraphicsFormat.R8G8B8A8_SRGB;
    }
}
