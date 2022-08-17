using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// A struct holding arguments to present to screen.
    /// </summary>
    public struct PresentArgs
    {
        /// <summary>
        /// A <see cref="CommandBuffer"/> used to present to screen.
        /// </summary>
        public CommandBuffer CommandBuffer;

        /// <summary>
        /// Whether to flip the render along the Y axis.
        /// </summary>
        public bool FlipY;

        /// <summary>
        /// The pixel rect of the camera used to present.
        /// </summary>
        public Rect CameraPixelRect;

        public RTHandle BackBuffer;
    }
}
