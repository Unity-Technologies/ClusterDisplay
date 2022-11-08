using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// An entry of the catalog describing something that can be launched.
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
        public string Type { get; set; } = "";

        /// <summary>
        /// Some data (opaque to all parts of MissionControl, only to be used by the launch and pre-launch executables)
        /// to be passed using the LAUNCHABLE_DATA environment variable both during launch and pre-launch.
        /// </summary>
        /// <remarks>This is the same hard-coded data for all nodes of the cluster, useful for configuring some options
        /// decided at the moment of producing the file containing this information.  Because of OS limitations the
        /// amount of data in this object should be kept reasonable (current limitation seem to be around 8192
        /// characters).</remarks>
        public JToken Data { get; set; }

        /// <summary>
        /// Parameters allowing to customize execution (passed in the LAUNCH_DATA environment variable for both
        /// pre-launch and launch).  Value will be the same for every launchpad executing this <see cref="Launchable"/>.
        /// </summary>
        public List<LaunchParameter> GlobalParameters { get; set; } = new();

        /// <summary>
        /// Parameters allowing to customize execution (passed in the LAUNCH_DATA environment variable for both
        /// pre-launch and launch).  Value will be the same for all launchpads of a launch complex.
        /// </summary>
        public List<LaunchParameter> LaunchComplexParameters { get; set; } = new();

        /// <summary>
        /// Parameters allowing to customize execution (passed in the LAUNCH_DATA environment variable for both
        /// pre-launch and launch).  Value will can be different for each launchpad.
        /// </summary>
        public List<LaunchParameter> LaunchPadParameters { get; set; } = new();

        /// <summary>
        /// Path (relative to the location where all payloads files are stored) to an optional executable to execute
        /// before launch.  This executable is responsible to ensure that any external dependencies are installed and
        /// ready to use.
        /// </summary>
        /// <remarks>Can be an executable, a ps1 or an assemblyrun:// url.</remarks>
        public string PreLaunchPath { get; set; } = "";

        /// <summary>
        /// Path (relative to the location where all payloads files are stored) to the executable to launch to start the
        /// process of this <see cref="Launchable"/>.
        /// </summary>
        /// <remarks>Can be an executable, a ps1 or an assemblyrun:// url.</remarks>
        public string LaunchPath { get; set; } = "";

        /// <summary>
        /// How much time does a launchable process has to realize it has to stop before being killed.
        /// </summary>
        [JsonConverter(typeof(TimeSpanToSecondsJsonConverter))]
        [JsonProperty("landingTimeSec")]
        public TimeSpan LandingTime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Name of Payloads forming the list of all the files needed by this <see cref="Launchable"/>.
        /// </summary>
        public List<string> Payloads { get; set; } = new();

        public bool Equals(Launchable other)
        {
            return other != null &&
                Name == other.Name &&
                Type == other.Type &&
                ReferenceEquals(Data, null) == ReferenceEquals(other.Data, null) &&
                (ReferenceEquals(Data, null) || Data.ToString() == other.Data!.ToString()) &&
                GlobalParameters.SequenceEqual(other.GlobalParameters) &&
                LaunchComplexParameters.SequenceEqual(other.LaunchComplexParameters) &&
                LaunchPadParameters.SequenceEqual(other.LaunchPadParameters) &&
                PreLaunchPath == other.PreLaunchPath &&
                LaunchPath == other.LaunchPath &&
                LandingTime == other.LandingTime &&
                Payloads.SequenceEqual(other.Payloads);
        }

        /// <summary>
        /// Value for the <see cref="Type"/> property for launchables acting as capcom.
        /// </summary>
        public const string CapcomType = "capcom";

        /// <summary>
        /// Value for the <see cref="Type"/> property for launchables acting as cluster node.
        /// </summary>
        public const string ClusterNodeType = "clusterNode";
    }
}
