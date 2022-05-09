using System.Collections.Generic;
using UnityEngine;

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
        [SerializeField]
        protected bool m_IsDebug;

        Material m_OverridingBlitMaterial;
        Dictionary<int, MaterialPropertyBlock> m_OverridingBlitPropertyBlocks;
        MaterialPropertyBlock m_OverridingBlitPropertyBlock;

        protected Material GetOverridingBlitMaterial() => m_OverridingBlitMaterial;
        protected MaterialPropertyBlock GetOverridingBlitPropertyBlock() => m_OverridingBlitPropertyBlock;

        protected MaterialPropertyBlock GetOverridingBlitPropertyBlock(int index)
        {
            if (m_OverridingBlitPropertyBlocks == null)
            {
                return null;
            }

            if (!m_OverridingBlitPropertyBlocks.TryGetValue(index, out var materialPropertyBlock))
            {
                throw new System.ArgumentException($"There is no: {nameof(MaterialPropertyBlock)} for index: {index}");
            }

            if (materialPropertyBlock == null)
            {
                throw new System.ArgumentException($"NULL: {nameof(MaterialPropertyBlock)} for index: {index}");
            }

            return materialPropertyBlock;
        }

        public void SetOverridingBlitMaterial(Material material, MaterialPropertyBlock materialPropertyBlock = null)
        {
            m_OverridingBlitMaterial = material;
            m_OverridingBlitPropertyBlock = materialPropertyBlock;
        }

        public void SetOverridingBlitMaterial(Material material, Dictionary<int, MaterialPropertyBlock> materialPropertyBlocks = null)
        {
            m_OverridingBlitMaterial = material;
            m_OverridingBlitPropertyBlocks = materialPropertyBlocks;
        }
        
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
        /// <param name="args">A <see cref="PresentArgs"/> struct holding present arguments.</param>
        /// <remarks>
        /// At this point, you can enqueue any commands that are required to draw the final
        /// output to the current display output device.
        /// </remarks>
        public abstract void Present(PresentArgs args);

        /// <summary>
        /// Called on the <see cref="ClusterRenderer"/>'s <c>OnDrawGizmos</c> event.
        /// </summary>
        public virtual void OnDrawGizmos() { }

        /// <summary>
        /// Gets or sets the origin of the cluster display.
        /// </summary>
        public virtual Matrix4x4 Origin { get; set; }

        /// <summary>
        /// Specifies whether debug mode is enabled.
        /// </summary>
        public bool IsDebug
        {
            set => m_IsDebug = value;
            get => m_IsDebug;
        }
    }
}
