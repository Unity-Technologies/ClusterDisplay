using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Information about a LaunchComplex (generally a computer).
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class LaunchComplex: IIncrementalCollectionObject, IEquatable<LaunchComplex>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public LaunchComplex(Guid id)
        {
            Id = id;
        }

        /// <summary>
        /// Identifier of the object
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// LaunchPads forming this LaunchComplex.
        /// </summary>
        public List<LaunchPad> LaunchPads { get; set; } = new();

        /// <inheritdoc/>
        public void DeepCopyFrom(IIncrementalCollectionObject fromObject)
        {
            var from = (LaunchComplex)fromObject;
            LaunchPads = from.LaunchPads.Select(lp => lp.DeepClone()).ToList();
        }

        public bool Equals(LaunchComplex other)
        {
            return other != null &&
                Id == other.Id &&
                LaunchPads.SequenceEqual(other.LaunchPads);
        }
    }
}
