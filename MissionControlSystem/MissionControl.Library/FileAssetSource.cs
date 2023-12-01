using System;
using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// <see cref="IAssetSource"/> implementation that access the asset directly using the file system (on the computer
    /// running mission control).
    /// </summary>
    public class FileAssetSource : IAssetSource
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="assetRoot">Absolute path to the asset's root folder.</param>
        public FileAssetSource(string assetRoot)
        {
            m_AssetRoot = assetRoot;
            m_CatalogPath = Path.Combine(m_AssetRoot, "LaunchCatalog.json");
        }

        /// <inheritdoc/>
        public async Task<Catalog> GetCatalogAsync()
        {
            await using var loadStream = File.OpenRead(m_CatalogPath);
            var catalog = await JsonSerializer.DeserializeAsync<Catalog>(loadStream, Json.SerializerOptions);
            if (catalog == null)
            {
                throw new NullReferenceException($"Failed to deserialize content of {m_CatalogPath}");
            }
            return catalog;
        }

        /// <inheritdoc/>
        public Task<AssetSourceOpenedFile> GetFileContentAsync(string path)
        {
            string toOpenPath = Path.Combine(m_AssetRoot, path);
            FileInfo toOpenInfo = new(toOpenPath);
            AssetSourceOpenedFile ret = new(File.OpenRead(toOpenPath), toOpenInfo.Length);
            ret.Stream.Position = 0;
            return Task.FromResult(ret);
        }

        /// <summary>
        /// Absolute path to the asset's root folder.
        /// </summary>
        readonly string m_AssetRoot;

        /// <summary>
        /// Absolute path to the asset's LaunchCatalog.json.
        /// </summary>
        readonly string m_CatalogPath;
    }
}
