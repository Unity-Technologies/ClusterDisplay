using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Information about a LaunchPad.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class LaunchPad: IEquatable<LaunchPad>
    {
        /// <summary>
        /// <see cref="LaunchPad"/>'s identifier.
        /// </summary>
        public Guid Identifier { get; set; } = Guid.Empty;

        /// <summary>
        /// User displayed name identifying this LaunchPad.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Http endpoint of the LaunchPad.
        /// </summary>
        public Uri Endpoint {
            get => m_Endpoint;
            set => m_Endpoint = new Uri(value.ToString());
        }
        Uri m_Endpoint = new Uri("http://0.0.0.0/");

        /// <summary>
        /// Types of Launchables that this LaunchPad can deal with.
        /// </summary>
        public List<string> SuitableFor { get; set; } = new();

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        /// <returns></returns>
        public LaunchPad DeepClone()
        {
            LaunchPad ret = new();
            ret.Identifier = Identifier;
            ret.Name = Name;
            ret.Endpoint = Endpoint;
            ret.SuitableFor = SuitableFor.ToList();
            return ret;
        }

        public bool Equals(LaunchPad other)
        {
            return other != null &&
                Identifier == other.Identifier &&
                Name == other.Name &&
                Endpoint == other.Endpoint &&
                SuitableFor.SequenceEqual(other.SuitableFor);
        }
    }
}
