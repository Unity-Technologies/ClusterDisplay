using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Information about files to be used by a <see cref="Launchable"/>.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class Payload: IEquatable<Payload>
    {
        /// <summary>
        /// Name of this Payload.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// List of files that form this Payload.
        /// </summary>
        public List<PayloadFile> Files { get; set; } = new();

        public bool Equals(Payload other)
        {
            return other != null &&
                Name == other.Name &&
                Files.SequenceEqual(other.Files);
        }
    }
}
