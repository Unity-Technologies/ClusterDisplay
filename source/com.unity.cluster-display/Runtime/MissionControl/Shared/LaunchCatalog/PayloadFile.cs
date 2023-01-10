using System;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Information about a file in a payload.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
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
        public string Md5 { get; set; }

        public bool Equals(PayloadFile other)
        {
            return other != null &&
                Path == other.Path &&
                Md5 == other.Md5;
        }

        string m_Path = "";
    }
}
