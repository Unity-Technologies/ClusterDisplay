using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// Information about a file content as returned by <see cref="IAssetSource.GetFileContentAsync"/>.
    /// </summary>
    /// <remarks>We need this class as the method need to return the <see cref="Stream"/> and the file length because
    /// <see cref="System.IO.Stream.Length"/> for some <see cref="Stream"/> specialization can throw a
    /// <see cref="NotImplementedException"/>.</remarks>
    public class AssetSourceOpenedFile : IDisposable
    {
        public AssetSourceOpenedFile(Stream stream, long length)
        {
            Stream = stream;
            Length = length;
        }

        public Stream Stream { get; }
        public long Length { get; }

        public void Dispose()
        {
            Stream.Dispose();
        }
    }

    /// <summary>
    /// Interface for objects giving access to asset definition (LaunchCatalog.json, file data, ...).
    /// </summary>
    public interface IAssetSource
    {
        /// <summary>
        /// Fetch the content of LaunchCatalog.json.
        /// </summary>
        public Task<LaunchCatalog.Catalog> GetCatalogAsync();

        /// <summary>
        /// Returns a <see cref="Stream"/> that will give access to the content of the specified file of the asset.
        /// </summary>
        /// <param name="path">Path of the file relative to the asset root (as in <see cref="PayloadFile"/>).</param>
        public Task<AssetSourceOpenedFile> GetFileContentAsync(string path);
    }
}
