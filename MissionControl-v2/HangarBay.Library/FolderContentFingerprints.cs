using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Library
{
    /// <summary>
    /// Class used to save the "fingerprints" (sufficient evidence to identify it is the same file and it has not
    /// changed) of everything in a folder so that we can then remove everything that has changed.
    /// </summary>
    public class FolderContentFingerprints
    {
        /// <summary>
        /// Store the fingerprints of all the files in given folder and create a new
        /// <see cref="FolderContentFingerprints"/> from it.
        /// </summary>
        /// <param name="path">Path to the folder from which to build the <see cref="FolderContentFingerprints"/>.
        /// </param>
        /// <param name="payload">List of files in <paramref name="path"/>.</param>
        /// <returns>Newly created <see cref="FolderContentFingerprints"/>.</returns>
        public static FolderContentFingerprints BuildFrom(string path, Payload payload)
        {
            FolderContentFingerprints ret = new ();

            var files = GetAllFilesIn(path);
            var payloadFiles = IndexPayload(path, payload);

            // Fill entries
            foreach (var filePath in files)
            {
                string entryKey = GetEntriesKey(filePath, path);
                if (payloadFiles.TryGetValue(entryKey, out var payloadFile))
                {
                    ret.m_Entries.Add(entryKey, new Entry(filePath, payloadFile.FileBlob));
                }
            }

            return ret;
        }

        /// <summary>
        /// Load a <see cref="FolderContentFingerprints"/> that was previously saved with the <see cref="SaveTo"/> method.
        /// </summary>
        /// <param name="path">Path of the file to load.</param>
        /// <returns>Loaded <see cref="FolderContentFingerprints"/>.</returns>
        public static FolderContentFingerprints LoadFrom(string path)
        {
            FolderContentFingerprints ret = new ();

            using (var loadStream = File.Open(path, FileMode.Open))
            {
                var deserialized = JsonSerializer.Deserialize<SerializeWrapper>(loadStream);
                if (deserialized == null)
                {
                    throw new NullReferenceException("Deserialize returned null");
                }
                ret.m_Entries = deserialized.Entries;
            }

            return ret;
        }

        /// <summary>
        /// Save the content of the <see cref="FolderContentFingerprints"/> to the given json file.
        /// </summary>
        /// <param name="path">Path to the json file.</param>
        public void SaveTo(string path)
        {
            var serializeWrapperWrapper = new SerializeWrapper();
            serializeWrapperWrapper.Entries = m_Entries;
            using FileStream serializeStream = File.Create(path);
            JsonSerializer.Serialize(serializeStream, serializeWrapperWrapper);
        }

        /// <summary>
        /// Prepare the given folder to receive the specified payload.  This method will remove any new file or file 
        /// that changed since the fingerprints were taken or that contains a different blob then the one from the
        /// provided payload.
        /// </summary>
        /// <param name="path">Path to the folder to clean.</param>
        /// <param name="payload">List of files that we want to prepare the folder for.</param>
        /// <param name="logger">Logger in case of problem.</param>
        public void PrepareForPayload(string path, Payload payload, ILogger logger)
        {
            var cleanedPath = Path.GetFullPath(path);
            var files = GetAllFilesIn(cleanedPath);
            var payloadFiles = IndexPayload(path, payload);

            // Delete new, modified or files with the wrong content
            HashSet<string> potentiallyEmptyFolders = new();
            foreach (var filePath in files)
            {
                var searchKey = GetEntriesKey(filePath, path);
                bool shouldDelete = true;
                if (m_Entries.TryGetValue(searchKey, out var entry))
                {
                    if (payloadFiles.TryGetValue(searchKey, out var payloadFile))
                    {
                        if (entry.Equals(new Entry(filePath, payloadFile.FileBlob)))
                        {
                            shouldDelete = false;
                        }
                    }
                }

                if (shouldDelete)
                {
                    try
                    {
                        var folderOfFile = Path.GetDirectoryName(filePath);
                        if (folderOfFile != null)
                        {
                            potentiallyEmptyFolders.Add(folderOfFile);
                        }
                        File.Delete(filePath);
                    }
                    catch(Exception)
                    {
                        if (payloadFiles.ContainsKey(searchKey))
                        {
                            // The payload we will want to copy need that file, so we have to remove the file...
                            logger.LogError($"Failed to delete {filePath}");
                            throw;
                        }
                        else
                        {
                            // The list of files we want to prepare does not contain that file, so we can probably
                            // tolerate a few zombies...
                            logger.LogWarning($"Failed to delete {filePath}");
                        }
                    }
                }
            }

            // Clean empty folders
            try
            {
                while (potentiallyEmptyFolders.Any())
                {
                    HashSet<string> parentFolders = new();

                    foreach (var potentiallyEmptyFolder in potentiallyEmptyFolders)
                    {
                        if (!Directory.EnumerateFileSystemEntries(potentiallyEmptyFolder).Any())
                        {
                            var parentFolder = Path.GetDirectoryName(potentiallyEmptyFolder);
                            if (parentFolder != null && parentFolder.ToLower() != cleanedPath.ToLower())
                            {
                                parentFolders.Add(parentFolder);
                            }
                            Directory.Delete(potentiallyEmptyFolder);
                        }
                    }

                    potentiallyEmptyFolders = parentFolders;
                }
            }
            catch(Exception e)
            {
                logger.LogWarning("Error cleaning empty folders, will continue as negative consequences are probably " +
                    $"not too bad: {e}");
            }
        }

        /// <summary>
        /// Fingerprints of a file.
        /// </summary>
        struct Entry: IEquatable<Entry>
        {
            /// <summary>
            /// Default empty constructor
            /// </summary>
            public Entry()
            {
                LastWriteTime = DateTime.MinValue;
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="filePath">Path to the file.</param>
            /// <param name="fileBlobId">Identifier representing the content of the file.</param>
            public Entry(string filePath, Guid fileBlobId)
            {
                LastWriteTime = File.GetLastWriteTimeUtc(filePath);
                BlobId = fileBlobId;
            }

            /// <summary>
            /// When was the last time the file written to.
            /// </summary>
            public DateTime LastWriteTime { get; set; }

            /// <summary>
            /// FileBlobId representing the content of the file.
            /// </summary>
            public Guid BlobId { get; set; } = Guid.Empty;

            public bool Equals(Entry other)
            {
                return LastWriteTime == other.LastWriteTime && BlobId == other.BlobId;
            }
        }

        /// <summary>
        /// Small wrapper to make serialization / deserialization more future proof (so that the serialized json looks
        /// like {"Entries":[...]} and not a top level array to which we can't add any properties in the future).
        /// </summary>
        class SerializeWrapper
        {
            public Dictionary<string, Entry> Entries { get; set; } = new Dictionary<string, Entry>();
        }

        /// <summary>
        /// Returns all the files in the given folder.
        /// </summary>
        /// <param name="path">Path to the folder</param>
        /// <returns>Full path to every file in the folder.</returns>
        static string[] GetAllFilesIn(string path)
        {
            var enumOptions = new EnumerationOptions();
            enumOptions.RecurseSubdirectories = true;
            return Directory.GetFiles(path, "*", enumOptions);
        }

        /// <summary>
        /// Returns a string to use a key in m_Entries.
        /// </summary>
        /// <param name="filePath">Path of the file name</param>
        /// <param name="basePath">Path to the folder that contains all the files</param>
        /// <returns>The string</returns>
        static string GetEntriesKey(string filePath, string basePath)
        {
            return Path.GetRelativePath(basePath, filePath);
        }

        /// <summary>
        /// Index the payload files in a way similar to the one we use to index our entries.
        /// </summary>
        /// <param name="path">Path to the folder relative to which all the files in <paramref name="payload"/> are.
        /// </param>
        /// <param name="payload">The <see cref="Payload"/> to index.</param>
        /// <returns>Indexed <see cref="PayloadFile"/>s.</returns>
        static Dictionary<string, PayloadFile> IndexPayload(string path, Payload payload)
        {
            return new Dictionary<string, PayloadFile>(
                payload.Files.Select(f => new KeyValuePair<string, PayloadFile>(GetEntriesKey(Path.Combine(path, f.Path), path), f)));
        }

        /// <summary>
        /// The entries defining the folder state.
        /// </summary>
        Dictionary<string, Entry> m_Entries = new();
    }
}
