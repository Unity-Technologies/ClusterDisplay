using System.IO;
using System.Runtime.InteropServices;
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
        /// <see cref="FolderContentFingerprints"/>
        /// from it.
        /// </summary>
        /// <param name="path">Path to the folder from which to build the <see cref="FolderContentFingerprints"/>.
        /// </param>
        /// <returns>Newly created <see cref="FolderContentFingerprints"/>.</returns>
        public static FolderContentFingerprints BuildFrom(string path)
        {
            FolderContentFingerprints ret = new ();

            var files = GetAllFilesIn(path);

            foreach (var filePath in files)
            {
                ret.m_Entries.Add(GetEntriesKey(filePath, path), new Entry(filePath));
            }

            return ret;
        }

        /// <summary>
        /// Load a <see cref="FolderContentFingerprints"/> that was previously saved with the <see cref="Save"/> method.
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
        /// Delete any new file in the folder or any file modified since the fingerprints in this object has been taken.
        /// </summary>
        /// <param name="path">Path to the folder to clean</param>
        public void CleanModified(string path, ILogger logger)
        {
            var cleanedPath = Path.GetFullPath(path);
            var files = GetAllFilesIn(cleanedPath);

            // Delete new or modified files
            HashSet<string> potentiallyEmptyFolders = new();
            foreach (var filePath in files)
            {
                var searchKey = GetEntriesKey(filePath, path);
                bool shouldDelete = true;
                if (m_Entries.TryGetValue(searchKey, out Entry entry))
                {
                    if (entry.EquivalentTo(new Entry(filePath)))
                    {
                        shouldDelete = false;
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
                        logger.LogError($"Failed to delete {filePath}");
                        throw;
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
        /// <remarks>For now it only contains one property, but created this struct to make future modifications easier
        /// if ever we need additional information to better detect file changes.</remarks>
        struct Entry
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
            /// <param name="filePath">Path to the file</param>
            public Entry(string filePath)
            {
                LastWriteTime = File.GetLastWriteTimeUtc(filePath);
            }

            /// <summary>
            /// Returns if this <see cref="Entry"/> is equivalent to the other provided <see cref="Entry"/>.
            /// </summary>
            /// <param name="other">The other <see cref="Entry"/>.</param>
            public bool EquivalentTo(Entry other)
            {
                return LastWriteTime == other.LastWriteTime;
            }

            /// <summary>
            /// When was the last time the file written to.
            /// </summary>
            public DateTime LastWriteTime { get; set; }
        }

        /// <summary>
        /// Small wrapper to make serialization / deserialization more future proof (in case we want to serialize
        /// something else in the future).
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
            var ret = Path.GetRelativePath(basePath, filePath);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ret = ret.ToLower();
            }
            return ret;
        }

        /// <summary>
        /// The entries defining the folder state.
        /// </summary>
        Dictionary<string, Entry> m_Entries = new();
    }
}
