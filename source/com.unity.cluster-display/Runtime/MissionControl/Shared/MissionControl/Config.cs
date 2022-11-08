using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// MissionControl's configuration.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class Config: IEquatable<Config>
    {
        /// <summary>
        /// Base address that will be used by local services to reach back mission control (like capcom launchables).
        /// </summary>
        // ReSharper disable once UnusedAutoPropertyAccessor.Global -> Set needs to be public for Json serialization
        public String LocalEntry { get; set; }

        public bool Equals(Config other)
        {
            return other != null &&
                LocalEntry.Equals(other.LocalEntry);
        }
    }
}
