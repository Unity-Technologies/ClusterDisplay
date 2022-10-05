using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public static class AssetBaseExtensions
    {
        public static void CopyAssetBaseProperties(this IAssetBase to, IAssetBase from)
        {
            to.Name = from.Name;
            to.Description = from.Description;
        }
    }

    /// <summary>
    /// Used to have some common properties between <see cref="Asset"/> and <see cref="AssetPost"/>.
    /// </summary>
    /// <remarks>This cannot be a base class since <see cref="Asset"/> would need to inherits from both this class and
    /// <see cref="IncrementalCollectionObject"/> (and <see cref="AssetPost"/> is not an
    /// <see cref="IncrementalCollectionObject"/>).</remarks>
    public interface IAssetBase
    {
        /// <summary>
        /// Short descriptive name of the asset.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Detailed description of the asset.
        /// </summary>
        public string Description { get; set; }
    }
}
