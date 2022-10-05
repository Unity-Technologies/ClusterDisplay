using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Information about files to be used by a <see cref="Launchable"/>.
    /// </summary>
    public class Payload: IEquatable<Payload>
    {
        /// <summary>
        /// Name of this Payload.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// List of files that form this Payload.
        /// </summary>
        public IEnumerable<PayloadFile> Files { get; set; } = Enumerable.Empty<PayloadFile>();

        public bool Equals(Payload? other)
        {
            if (other == null || other.GetType() != typeof(Payload))
            {
                return false;
            }
            return Name == other.Name && Files.SequenceEqual(other.Files);
        }
    }
}
