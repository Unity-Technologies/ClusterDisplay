using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Launch configuration of a MissionControl's mission.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class LaunchConfiguration: IEquatable<LaunchConfiguration>
    {
        /// <summary>
        /// Identifier of the <see cref="Asset"/> used by this mission.
        /// </summary>
        public Guid AssetId { get; set; }

        /// <summary>
        /// Configuration of every <see cref="LaunchComplex"/> participating to the mission.
        /// </summary>
        public List<LaunchComplexConfiguration> LaunchComplexes { get; set; } = new();

        public bool Equals(LaunchConfiguration other)
        {
            return other != null &&
                AssetId == other.AssetId &&
                LaunchComplexes.SequenceEqual(other.LaunchComplexes);
        }
    }
}
