using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Information about a file in a payload.
    /// </summary>
    public class PayloadFile: IEquatable<PayloadFile>
    {
        /// <summary>
        /// Path of the file relative to the file containing this information.
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// Md5 checksum of the file.
        /// </summary>
        public string Md5 { get; set; } = "";

        public bool Equals(PayloadFile? other)
        {
            return other != null &&
                Path == other.Path &&
                Md5 == other.Md5;
        }
    }
}
