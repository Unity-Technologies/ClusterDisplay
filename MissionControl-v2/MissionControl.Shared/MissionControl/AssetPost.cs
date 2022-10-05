// ReSharper disable PropertyCanBeMadeInitOnly.Global
namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Definition of an asset to create.
    /// </summary>
    public class AssetPost: IAssetBase
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
        /// URL to a folder that contains a LaunchCatalog.json listing all the payloads and their files.
        /// </summary>
        /// <remarks>For now this needs to be a path accessible by the computer running mission control, but we could
        /// imagine other improvements using other protocols to fetch the content in future.</remarks>
        public string Url { get; set; } = "";
    }
}
