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
    public abstract class ProjectionPolicy : ScriptableObject
    {
        /// <summary>
        /// Called just before the frame is rendered.
        /// </summary>
        /// <param name="clusterSettings">The current cluster display settings.</param>
        /// <param name="activeCamera">The current "main" camera.
        /// </param>
        /// <param name="anchor">The localToWorld matrix of the calling object.</param>
        /// <remarks>
        /// The <paramref name="activeCamera"/> will be disabled by default so it will not be rendered
        /// normally. At this point, you can perform special logic, such as manipulating projection
        /// matrices, rendering to a <see cref="RenderTexture"/>, etc. Do not draw anything to the screen;
        /// that should happen in your <see cref="Present"/> method.
        /// The <param name="anchor"></param> parameter allows you to pass an "origin" to the projection policy,
        /// if it needs one.
        /// </remarks>
        public abstract void UpdateCluster(
            ClusterRendererSettings clusterSettings, 
            Camera activeCamera, 
            Matrix4x4 anchor);

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

        public virtual void DrawGizmos()
        {
            
        }
    }
}
