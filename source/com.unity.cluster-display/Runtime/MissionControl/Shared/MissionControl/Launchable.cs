using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Represent MissionControl information about something that can be launched on a launchpad.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class Launchable: IEquatable<Launchable>
    {
        /// <summary>
        /// Some descriptive name identifying the <see cref="Launchable"/> to the user.
        /// </summary>
        /// <remarks>Must be unique within the catalog.</remarks>
        public string Name { get; set; } = "";

        /// <summary>
        /// Identifier of this type of <see cref="Launchable"/> (to find compatible nodes).
        /// </summary>
        /// <remarks>The type "capcom" is used to identify a launchable that is to be launched on the local mission
        /// control computer to act to act as a liaison with launched payloads handling work that is dependent on the
        /// payload.<br/><br/>
        /// Although conceptually type is an enum, a new MissionControl plug-in should be able to add a new
        /// type of launchable support, so we keep this type a string to avoid the need to recompile MissionControl
        /// when a new plug-in is available.</remarks>
        public string Type { get; set; } = "";

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        public Launchable DeepClone()
        {
            Launchable ret = new();
            ret.Name = Name;
            ret.Type = Type;
            return ret;
        }

        public bool Equals(Launchable other)
        {
            return other != null &&
                Name == other.Name &&
                Type == other.Type;
        }
    }
}
