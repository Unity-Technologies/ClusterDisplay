using System.Diagnostics;
using System.IO.Compression;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Services
{
    public static class FileBlobCacheServiceExtension
    {
        public static void AddFileBlobCacheService(this IServiceCollection services)
        {
            services.AddSingleton<FileBlobCacheService>();
        }
    }

    /// <summary>
    /// Service responsible to manage a cache of file blobs (from which we copy files to the LaunchPad folders).
    /// </summary>
    public class FileBlobCacheService
    {
        public FileBlobCacheService(ILogger<FileBlobCacheService> logger, HttpClient httpClient,
            IHostApplicationLifetime applicationLifetime, ConfigService configService)
        {
            m_Logger = logger;
            m_HttpClient = httpClient;
            m_ConfigService = configService;
            m_Cache = new(logger);

            m_Cache.FetchFileCallback += FetchFileCallback;
            m_Cache.CopyFileCallback += CopyFileCallback;

            applicationLifetime.ApplicationStopping.Register(() => {
                m_Cache.PersistStorageFolderStates();
            });

            _ = PeriodicPersistStorageFoldersState(applicationLifetime.ApplicationStopping);

            // Load current list of storage folders and save any changes done while loading
            UpdateCacheConfiguration().Wait();
            m_Cache.PersistStorageFolderStates();

            m_ConfigService.ValidateNew += ValidateNewConfig;
            m_ConfigService.Changed += Configchanged;
        }

        /// <summary>
        /// Returns a new <see cref="PayloadsManager"/> that will increase / decrease usage count on the
        /// <see cref="FileBlobCache"/> we are managing.
        /// </summary>
        /// <param name="logger">Object used to send logging messages.</param>
        /// <param name="storage">Folder in which we will store fetched payloads.</param>
        /// <remarks>I admit, this method might look like it does not belong in here on first sight, but it allows on
        /// keeping m_Cache private and under the control this class which is a good thing...</remarks>
        public PayloadsManager NewPayloadsManager(ILogger logger, string storage)
        {
            return new PayloadsManager(logger, storage, m_Cache);
        }

        /// <summary>
        /// Computes the status of the storage folders in which the <see cref="FileBlobCache"/> stores cached files.
        /// </summary>
        /// <returns>The computed status.</returns>
        public StorageFolderStatus[] GetStorageFolderStatus()
        {
            return m_Cache.GetStorageFolderStatus();
        }

        /// <summary>
        /// Fetch (if necessary) and copy the given file to the given destination.
        /// </summary>
        /// <param name="fileBlobId">FileBlob (content) identifier.</param>
        /// <param name="toPath">Complete path (directory and filename) of where to copy the file.</param>
        /// <param name="blobSource">URI from where to download file blobs if they are not already available in the
        /// cache.</param>
        /// <exception cref="ArgumentException">If no information about <paramref name="fileBlobId"/> can be found.
        /// </exception>
        /// <exception cref="InvalidOperationException">If no free space can be found to store the file in cache.
        /// </exception>
        public Task CopyFileTo(Guid fileBlobId, string toPath, string blobSource)
        {
            return m_Cache.CopyFileToAsync(fileBlobId, toPath, blobSource);
        }

        /// <summary>
        /// Callback responsible for validating a new configuration.
        /// </summary>
        /// <param name="configChange">Information about the configuration change.</param>
        void ValidateNewConfig(ConfigService.ConfigChangeSurvey configChange)
        {
            if (!configChange.Proposed.StorageFolders.Any())
            {
                configChange.Reject("At least one storage folder is needed.");
                return;
            }

            // Quick check that we can access the folders
            foreach (var folder in configChange.Proposed.StorageFolders)
            {
                string effectivePath = FileBlobCache.GetEffectiveStoragePath(folder.Path);
                if (!Directory.Exists(effectivePath))
                {
                    try
                    {
                        Directory.CreateDirectory(effectivePath);
                    }
                    catch (Exception)
                    {
                        configChange.Reject($"Can't access or create {effectivePath}.");
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Callback responsible to update our configuration when the configuration changes.
        /// </summary>
        Task Configchanged()
        {
            return UpdateCacheConfiguration();
        }

        /// <summary>
        /// Update <see cref="m_Cache"/> configurations based on <see cref="ConfigService"/>'s current state.
        /// </summary>
        async Task UpdateCacheConfiguration()
        {
            var currentStorageFolders = m_Cache.GetStorageFolderStatus();
            var newConfig = m_ConfigService.Current;

            // First, let's remove "old" folders (that are currently configured in m_Cache and that are not in the new
            // configuration) or update the ones that are still present.  We need to do the remove part first to avoid
            // potential conflicts between files that would be present in folders to be removed and new folders.
            foreach (var oldFolder in currentStorageFolders)
            {
                var newFolderConfig = newConfig.StorageFolders.Where(sf => sf.Path == oldFolder.Path).FirstOrDefault();
                if (newFolderConfig != null)
                {
                    m_Cache.UpdateStorageFolder(newFolderConfig);
                }
                else
                {
                    await m_Cache.RemoveStorageFolderAsync(oldFolder.Path);
                }
            }

            // Now let's add the new folders
            foreach (var newFolder in newConfig.StorageFolders)
            {
                var newFolderConfig = currentStorageFolders.Where(sf => sf.Path == newFolder.Path).FirstOrDefault();
                if (newFolderConfig == null)
                {
                    m_Cache.AddStorageFolder(newFolder);
                }
            }
        }

        /// <summary>
        /// Method called by <see cref="FileBlobCache"/> when a file is to be fetched.
        /// </summary>
        /// <param name="fileBlob">File blob identifier of the file to fetch.</param>
        /// <param name="fetchPath">Path of where to save that fetched content.</param>
        /// <param name="cookie">Cookie received by <see cref="FileBlobCache.CopyFileToAsync"/> -> URI from where to
        /// download the file blob.</param>
        /// <returns><see cref="Task"/> that is to be completed when fetch is completed.</returns>
        async Task FetchFileCallback(Guid fileBlob, string fetchPath, object? cookie)
        {
            Debug.Assert(cookie != null); // CopyFileTo always passes a cookie
            string blobSource = (string)cookie;

            var response = await m_HttpClient.GetAsync(new Uri(new Uri(blobSource), $"api/v1/fileBlobs/{fileBlob}"));
            response.EnsureSuccessStatusCode();
            using (var fileStream = new FileStream(fetchPath, FileMode.CreateNew))
            {
                await response.Content.CopyToAsync(fileStream);
            }
        }

        /// <summary>
        /// Method called by <see cref="FileBlobCache"/> when asked to copy a file.
        /// </summary>
        /// <param name="fromPath">Path to the file to copy.</param>
        /// <param name="toPath">Path to the destination.</param>
        /// <returns><see cref="Task"/> that is to be completed when copy is finished.</returns>
        Task CopyFileCallback(string fromPath, string toPath, object? _)
        {
            return Decompressor.Do(fromPath, toPath);
        }

        /// <summary>
        /// Small class managing decompressing a file asynchronously (using <see cref="Task"/>) and disposing of
        /// resource when done.
        /// </summary>
        class Decompressor: IDisposable
        {
            /// <summary>
            /// Start a task decompressing <paramref name="fromPath"/> to <paramref name="toPath"/>.
            /// </summary>
            /// <param name="fromPath">Compressed file to decompress.</param>
            /// <param name="toPath">Where to store the decompressed file.</param>
            /// <returns>Task executing the decompression.</returns>
            public static Task Do(string fromPath, string toPath)
            {
                return Task.Run(async () => {
                    Directory.CreateDirectory(Path.GetDirectoryName(toPath)!);
                    using (var decompressor = new Decompressor(fromPath, toPath))
                    {
                        await decompressor.m_Decompressor.CopyToAsync(decompressor.m_OutputFileStream);
                    }
                });
            }

            Decompressor(string fromPath, string toPath)
            {
                m_CompressedFileStream = File.OpenRead(fromPath);
                m_OutputFileStream = File.OpenWrite(toPath);
                m_Decompressor = new GZipStream(m_CompressedFileStream, CompressionMode.Decompress);
            }

            public void Dispose()
            {
                m_Decompressor.Dispose();
                m_OutputFileStream.Dispose();
                m_CompressedFileStream.Dispose();
            }

            FileStream m_CompressedFileStream;
            FileStream m_OutputFileStream;
            GZipStream m_Decompressor;
        }

        /// <summary>
        /// Task being executed periodically in the background to persist the current state of storage folders.
        /// </summary>
        /// <param name="cancellationToken">Indicate that we should stop saving.</param>
        async Task PeriodicPersistStorageFoldersState(CancellationToken cancellationToken)
        {
            var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                m_Cache.PersistStorageFolderStates();
            }
        }

        readonly ILogger<FileBlobCacheService> m_Logger;
        readonly HttpClient m_HttpClient;
        readonly ConfigService m_ConfigService;

        /// <summary>
        /// The object doing most of the work
        /// </summary>
        FileBlobCache m_Cache;
    }
}
