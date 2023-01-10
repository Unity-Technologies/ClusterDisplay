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
        /// <remarks>Folders should be separated using a forward slash (Linux convention, not Dos) so that we have the
        /// same result no matter the platform on which the LaunchCatalog.json is generated.</remarks>
        public string Path { get => m_Path; set => m_Path = value.Replace('\\', '/'); }

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

        /// <summary>
        /// Path of the file relative to the file containing this information.
        /// </summary>
        string m_Path = "";
    }
}
