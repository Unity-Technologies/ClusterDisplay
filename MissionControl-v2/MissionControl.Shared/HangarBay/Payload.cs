using System.Drawing;

namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// Information about a Payload
    /// </summary>
    public class Payload
    {
        /// <summary>
        /// List of files composing the Payload
        /// </summary>
        public IEnumerable<PayloadFile> Files { get; set; } = Enumerable.Empty<PayloadFile>();

        /// <summary>
        /// Merge the content (files) of the given <see cref="Payload"/>s into a new one.  Files present in multiple 
        /// payloads will kept once.
        /// </summary>
        /// <param name="payloads">From where to take the files to merge in this <see cref="Payload"/>.</param>
        /// <exception cref="ArgumentException">If two blobs are to be put into the same file.</exception>
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

            var ret = new Payload();
            ret.Files = files.Values;
            return ret;
        }

        public override bool Equals(Object? obj)
        {
            if (obj == null || obj.GetType() != typeof(Payload))
            {
                return false;
            }
            var other = (Payload)obj;

            return Files == other.Files;
        }

        public override int GetHashCode()
        {
            return Files.GetHashCode();
        }
    }
}
