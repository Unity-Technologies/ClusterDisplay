using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Root of the LaunchCatalog.json that describe something that can be launched with MissionControl.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class Catalog: IEquatable<Catalog>
    {
        /// <summary>
        /// List of all the payloads shared by the different <see cref="Launchable"/>s.
        /// </summary>
        public List<Payload> Payloads { get; set; } = new();

        /// <summary>
        /// List of all the things that can be launched on different launchpads of mission control.
        /// </summary>
        public List<Launchable> Launchables { get; set; } = new();

        public bool Equals(Catalog other)
        {
            return other != null &&
                Payloads.SequenceEqual(other.Payloads) &&
                Launchables.SequenceEqual(other.Launchables);
        }
    }
}
