using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Configuration of a <see cref="LaunchComplex"/> for a mission.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class LaunchComplexConfiguration: IEquatable<LaunchComplexConfiguration>
    {
        /// <summary>
        /// <see cref="LaunchComplex"/>'s identifier.
        /// </summary>
        public Guid Identifier { get; set; } = Guid.Empty;

        /// <summary>
        /// Configuration of every <see cref="LaunchPad"/> of this <see cref="LaunchComplex"/> participating to the
        /// mission.
        /// </summary>
        public List<LaunchPadConfiguration> LaunchPads { get; set; } = new();

        public bool Equals(LaunchComplexConfiguration other)
        {
            return other != null &&
                Identifier == other.Identifier &&
                LaunchPads.SequenceEqual(other.LaunchPads);
        }
    }
}
