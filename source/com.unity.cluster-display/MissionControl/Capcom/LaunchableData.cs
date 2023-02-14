using System;
using Newtonsoft.Json;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// Data stored in <see cref="Launchable.Data"/> and received through the <c>LAUNCHABLE_DATA</c> environment
    /// variable.
    /// </summary>
    public class LaunchableData
    {
        /// <summary>
        /// How much time does a launchable process has to adapt to a cluster reconfiguration following a failover
        /// operation.
        /// </summary>
        [JsonConverter(typeof(TimeSpanToSecondsJsonConverter))]
        [JsonProperty("failOverProcessTimeoutSec")]
        public TimeSpan FailOverProcessTimeout { get; set; } = TimeSpan.FromSeconds(15);
    }
}
