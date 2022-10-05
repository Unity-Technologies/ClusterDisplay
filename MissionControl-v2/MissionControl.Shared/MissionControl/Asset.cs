using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Description of an asset (something that can be launched).
    /// </summary>
    public class Asset: IncrementalCollectionObject, IAssetBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object.</param>
        public Asset(Guid id): base(id)
        {
        }

        /// <summary>
        /// Short descriptive name of the asset.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Detailed description of the asset.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// <see cref="Launchable"/>s as found in LaunchCatalog.yaml (but with payload ids as GUID as opposed to
        /// arbitrary strings as in the launch catalog).
        /// </summary>
        public IEnumerable<Launchable> Launchables { get; set; } = Enumerable.Empty<Launchable>();

        /// <summary>
        /// Number of bytes used by all the files of this asset in the MissionControl storage.
        /// </summary>
        /// <remarks>Compressed size that does not take into account if file blobs are shared by multiple assets.
        /// </remarks>
        public long StorageSize { get; set; }

        public override IncrementalCollectionObject NewOfTypeWithId()
        {
            return new Asset(Id);
        }

        protected override void DeepCopyImp(IncrementalCollectionObject fromObject)
        {
            var from = (Asset)fromObject;
            Name = from.Name;
            Description = from.Description;
            // Cloning Launchables could imply a whole set of new methods to clone launchables and everything it
            // references.  However we shouldn't need to create / modify assets that often and it quite light in term of
            // processing, so let's go with the lazy serialize / deserialize trick.
            Launchables = JsonSerializer.Deserialize<IEnumerable<Launchable>>(
                JsonSerializer.Serialize(from.Launchables, Json.SerializerOptions), Json.SerializerOptions)!;
            StorageSize = from.StorageSize;
        }
    }
}
