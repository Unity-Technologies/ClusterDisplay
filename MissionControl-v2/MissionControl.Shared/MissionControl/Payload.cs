using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Information about a set of files to be use to prepare a LaunchPad.
    /// </summary>
    /// <remarks><see cref="Asset"/> for a family portrait of <see cref="Payload"/> and its relation to an
    /// <see cref="Asset"/>.</remarks>
    public class Payload : IEquatable<Payload>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public Payload()
        {
            Files = Enumerable.Empty<PayloadFile>();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="files">List of files composing the Payload</param>
        [JsonConstructor]
        public Payload(IEnumerable<PayloadFile> files)
        {
            Files = files;
        }

        /// <summary>
        /// List of files composing the Payload
        /// </summary>
        public IEnumerable<PayloadFile> Files { get; }

        /// <summary>
        /// Merge the content (list of files) of the given <see cref="Payload"/>s into a new one.  Files present in 
        /// multiple payloads will kept once.
        /// </summary>
        /// <param name="payloads"><see cref="Payload"/>s to merge together.</param>
        /// <exception cref="ArgumentException">If two different blobs are to be put into the same file.</exception>
        public static Payload Merge(IEnumerable<Payload> payloads)
        {
            Dictionary<string, PayloadFile> files = new Dictionary<string, PayloadFile>();

            string cleanRoot = Path.GetTempPath();

            foreach (var payload in payloads)
            {
                foreach (var file in payload.Files)
                {
                    // Clean and simplify the path
                    string cleanedPath = Path.GetFullPath(file.Path, cleanRoot);

                    // Get already existing files with the same cleaned path (and validate equivalent) or add a new one.
                    if (files.TryGetValue(cleanedPath, out var mergedFile))
                    {
                        if (!file.IsSameContent(mergedFile))
                        {
                            throw new ArgumentException($"{file.Path} conflicts with another file.");
                        }
                    }
                    else
                    {
                        files.Add(cleanedPath, file);
                    }
                }
            }

            return new Payload(files.Values);
        }

        public bool Equals(Payload? other)
        {
            if (other == null || other.GetType() != typeof(Payload))
            {
                return false;
            }

            return Files.SequenceEqual(other.Files);
        }
    }
}
