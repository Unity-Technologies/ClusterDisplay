using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Performs custom cluster rendering logic.
    /// </summary>
    /// <remarks>
    /// Implement this abstract class to perform custom rendering and presentation operations.
    /// </remarks>
    abstract class ProjectionPolicy : ScriptableObject
    {
        /// <summary>
        /// Called just before the frame is rendered.
        /// </summary>
        /// <param name="clusterSettings">The current cluster display settings.</param>
        /// <param name="activeCamera">The current "main" camera.
        /// </param>
        /// <remarks>
        /// The <paramref name="activeCamera"/> will be disabled by default so it will not be rendered
        /// normally. At this point, you can perform special logic, such as manipulating projection
        /// matrices, rendering to a <see cref="RenderTexture"/>, etc. Do not draw anything to the screen;
        /// that should happen in your <see cref="Present"/> method.
        /// </remarks>
        public abstract void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera);

        /// <summary>
        /// Called after all rendering commands have been enqueued in the rendering pipeline.
        /// </summary>
        /// <param name="commandBuffer">A <see cref="CommandBuffer"/> belonging to the current
        /// rendering pipeline.</param>
        /// <remarks>
        /// At this point, you can enqueue any commands that are required to draw the final
        /// output to the current display output device.
        /// </remarks>
        public abstract void Present(CommandBuffer commandBuffer);

        /// <summary>
        /// Called on the <see cref="ClusterRenderer"/>'s <c>OnDrawGizmos</c> event.
        /// </summary>
        public virtual void OnDrawGizmos() { }

        /// <summary>
        /// Gets or sets the origin of the cluster display.
        /// </summary>
        public virtual Matrix4x4 Origin { get; set; }
    }
}
