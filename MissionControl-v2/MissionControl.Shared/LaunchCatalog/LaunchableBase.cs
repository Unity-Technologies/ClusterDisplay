using System;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Base class for Launchables in the launch catalog or as used in mission control's REST interface.
    /// </summary>
    public class LaunchableBase
    {
        /// <summary>
        /// Some descriptive name identifying the <see cref="LaunchableBase"/> to the user.
        /// </summary>
        /// <remarks>Must be unique within the catalog.</remarks>
        public string Name { get; set; } = "";

        /// <summary>
        /// Identifier of this type of <see cref="LaunchableBase"/> (to find compatible nodes).
        /// </summary>
        /// <remarks>The type "capcom" is used to identify a launchable that is to be launched on the local mission
        /// control computer to act to act as a liaison with launched payloads handling work that is dependent on the
        /// payload.<br/><br/>
        /// Although conceptually type is an enum, a new MissionControl plug-in should be able to add a new
        /// type of launchable support, so we keep this type a string to avoid the need to recompile MissionControl
        /// when a new plug-in is available.</remarks>
        public string Type { get; set; } = "";

        /// <summary>
        /// Some data (opaque to all parts of MissionControl, only to be used by the launch and pre-launch executables)
        /// to be passed using the LAUNCHABLE_DATA environment variable both during launch and pre-launch.  This is the
        /// same hard-coded data for all nodes of the cluster, useful for configuring some options decided at the moment
        /// of producing the file containing this information.
        /// </summary>
        /// <remarks>Because of OS limitations the amount of data in this object should be kept reasonable (current
        /// limitation seem to be around 8192 characters).</remarks>
        public JsonNode? Data { get; set; }

        /// <summary>
        /// Parameters allowing to customize execution (passed in the LAUNCH_DATA environment variable for both
        /// pre-launch and launch).  Value will be the same for every launchpad executing this <see cref="LaunchableBase"/>.
        /// </summary>
        public IEnumerable<LaunchParameter> GlobalParameters { get; set; } = Enumerable.Empty<LaunchParameter>();

        /// <summary>
        /// Parameters allowing to customize execution (passed in the LAUNCH_DATA environment variable for both
        /// pre-launch and launch).  Value will be the same for all launchpads of a launch complex.
        /// </summary>
        public IEnumerable<LaunchParameter> LaunchComplexParameters { get; set; } = Enumerable.Empty<LaunchParameter>();

        /// <summary>
        /// Parameters allowing to customize execution (passed in the LAUNCH_DATA environment variable for both
        /// pre-launch and launch).  Value will can be different for each launchpad.
        /// </summary>
        public IEnumerable<LaunchParameter> LaunchPadParameters { get; set; } = Enumerable.Empty<LaunchParameter>();

        /// <summary>
        /// Path (relative to the location where all payloads files are stored) to an optional executable to execute
        /// before launch.  This executable is responsible to ensure that any external dependencies are installed and
        /// ready to use.
        /// </summary>
        /// <remarks>Can be an executable, a ps1 or an assemblyrun:// url.</remarks>
        public string PreLaunchPath { get; set; } = "";

        /// <summary>
        /// Path (relative to the location where all payloads files are stored) to the executable to launch to start the
        /// process of this <see cref="LaunchableBase"/>.
        /// </summary>
        /// <remarks>Can be an executable, a ps1 or an assemblyrun:// url.</remarks>
        public string LaunchPath { get; set; } = "";

        /// <summary>
        /// How many seconds does a launchable process has to realize it has to stop before being killed.
        /// </summary>
        public float LandingTimeSec { get; set; }

        /// <summary>
        /// <see cref="TimeSpan"/> from <see cref="LandingTimeSec"/>.
        /// </summary>
        [JsonIgnore]
        public TimeSpan LandingTime => TimeSpan.FromSeconds(LandingTimeSec);

        /// <summary>
        /// Create a shallow copy of from.
        /// </summary>
        /// <param name="from">To copy from.</param>
        public void ShallowCopyFrom(LaunchableBase from)
        {
            Name = from.Name;
            Type = from.Type;
            Data = from.Data;
            GlobalParameters = from.GlobalParameters;
            LaunchComplexParameters = from.LaunchComplexParameters;
            LaunchPadParameters = from.LaunchPadParameters;
            PreLaunchPath = from.PreLaunchPath;
            LaunchPath = from.LaunchPath;
            LandingTimeSec = from.LandingTimeSec;
        }

        /// <summary>
        /// <see cref="Type"/> for launchables used to identify a special launchable that is to be launched on the
        /// local mission control computer to act as a liaison with all launched payloads handling aspects of the work
        /// that has to consider multiple launched payloads.  Capcom is started as soon as the asset is selected.
        /// Because of those differences capcom launchables does not have any launch parameters (global, complex or
        /// launchpad).
        /// </summary>
        public const string CapcomLaunchableType = "capcom";

        /// <summary>
        /// Returns if all properties of this are equal to properties of <paramref name="other"/>.
        /// </summary>
        /// <param name="other">The other <see cref="LaunchableBase"/> to compare with.</param>
        protected bool ArePropertiesEqual(LaunchableBase other)
        {
            return Name == other.Name &&
                Type == other.Type &&
                SerializeJsonNode(Data) == SerializeJsonNode(other.Data) &&
                GlobalParameters.SequenceEqual(other.GlobalParameters) &&
                LaunchComplexParameters.SequenceEqual(other.LaunchComplexParameters) &&
                LaunchPadParameters.SequenceEqual(other.LaunchPadParameters) &&
                PreLaunchPath == other.PreLaunchPath &&
                LaunchPath == other.LaunchPath &&
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                LandingTimeSec == other.LandingTimeSec;
        }

        static string SerializeJsonNode(JsonNode? toSerialize)
        {
            return toSerialize != null ? toSerialize.ToJsonString() : "";
        }
    }
}
