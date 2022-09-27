using System;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Various helpers related to directory, file system, files, ...
    /// </summary>
    public static class FileHelpers
    {
        /// <summary>
        /// Returns if the provided path point to the same folder as at least one of the other folders in a list of
        /// folders.
        /// </summary>
        /// <param name="path">Path.</param>
        /// <param name="listOfPath">List of paths to test.</param>
        /// <remarks>This method is made necessary from the fact that we can some time have different paths that leads
        /// to the same folder, even when the paths have been "cleaned" (<see cref="Path.GetFullPath(string)"/>).  This
        /// can happen on case insensitive file system when the case is different or on file systems that support
        /// symbolic links.<br/><br/>
        /// For this method to work it needs to have a write access to <paramref name="path"/>.</remarks>
        /// <returns>Path of <paramref name="listOfPath"/> pointing to the same location as <paramref name="path"/>.
        /// </returns>
        public static string GetPathToTheSameFolder(string path, IEnumerable<string> listOfPath)
        {
            // The most cross platform and easiest way of doing this is creating a file in path.  We choose a filename
            // from a brand new GUID so in theory it shouldn't already exist.  We also don't catch any exception while
            // creating that temporary file so that caller can more easily identify problems with writing to the folder.
            string testFilename = Guid.NewGuid().ToString();
            string testPath = Path.Combine(path, testFilename);
            try
            {
                File.WriteAllText(testPath, testFilename);
                foreach (var testFolder in listOfPath)
                {
                    if (File.Exists(Path.Combine(testFolder, testFilename)))
                    {
                        return testFolder;
                    }
                }
                return "";
            }
            finally
            {
                try
                {
                    File.Delete(testPath);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }
}
