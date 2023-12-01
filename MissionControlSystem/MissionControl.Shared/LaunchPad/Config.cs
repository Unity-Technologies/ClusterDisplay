using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// LaunchPad's configuration.  This data will be passed to both pre-launch and launch executables by serializing
    /// this object to JSON and passing it using the LAUNCHPAD_CONFIG environment variable.  It is also the content of
    /// the config.json file used to store the configuration of the LaunchPad.
    /// </summary>
    /// <remarks>This configuration is where new launchpad parameters that do not change between launchables might be
    /// added in the future (for example the GPU index to use?).</remarks>
    public struct Config: IEquatable<Config>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public Config() { }

        /// <summary>
        /// Uniquely identify this LaunchPad.
        /// </summary>
        /// <remarks>This identifier is created when first started and shall never change.</remarks>
        public Guid Identifier { get; set; } = Guid.Empty;

        /// <summary>
        /// End points to which to listen for REST commands (most likely coming from MissionControl).
        /// </summary>
        public IEnumerable<string> ControlEndPoints { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Name (or IP address) of the NIC connected to the cluster network.
        /// </summary>
        /// <remarks>Name will be transformed to an IP before serializing this object to fill LAUNCHPAD_CONFIG (to
        /// reduce the amount of work to be done by the launched payload).</remarks>
        public string ClusterNetworkNic { get; set; } = "";

        /// <summary>
        /// End point to which the hangar bay is listening for requests.
        /// </summary>
        public string HangarBayEndPoint { get; set; } = "http://127.0.0.1:8100";

        public bool Equals(Config other)
        {
            return Identifier == other.Identifier &&
                ControlEndPoints.SequenceEqual(other.ControlEndPoints) &&
                ClusterNetworkNic == other.ClusterNetworkNic && HangarBayEndPoint == other.HangarBayEndPoint;
        }
    }
}
