using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Data common to <see cref="AssetBase"/> and <see cref="Asset"/>.
    /// </summary>
    public class AssetBase
    {
        /// <summary>
        /// Short descriptive name of the asset.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Detailed description of the asset.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Creates a complete independent of from (no data should be shared between the original and the this).
        /// </summary>
        /// <param name="from"><see cref="AssetBase"/> to copy from, must be same type as this.</param>
        public void DeepCopyFrom(AssetBase from)
        {
            Name = from.Name;
            Description = from.Description;
        }
    }
}
