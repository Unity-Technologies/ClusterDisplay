using System;
using System.Collections.Generic;
using System.Linq;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl
{
    [Serializable]
    class MissionControlSettings : ProjectSettings<MissionControlSettings>
    {
        const string k_AssetName = "MissionControlSettings";

        [SerializeField]
        [Tooltip("Instrument the build to be used with Mission Control.")]
        // ReSharper disable once InconsistentNaming
        bool m_Instrument;

        [SerializeField]
        [Tooltip("Number of seconds for the application to gracefully exit before being forcefully terminated.")]
        // ReSharper disable once InconsistentNaming
        float m_QuitTimeout;

        public bool Instrument => m_Instrument;

        public float QuitTimeout => m_QuitTimeout;

        /// <summary>
        /// List of properties
        /// </summary>
        /// <remarks>Not serialized by design, this is a way for build preprocessors to store information that will be
        /// used by the main MissionControl build post processing.</remarks>
        public ParametersContainer PolicyParameters { get; set; }

        protected override void InitializeInstance()
        {
            m_QuitTimeout = 15.0f;
        }

        /// <summary>
        /// Small helper store list of <see cref="LaunchPadParameters"/> to be added to a LaunchCatalog.
        /// </summary>
        public class ParametersContainer
        {
            /// <summary>
            /// Global <see cref="LaunchParameter"/>s.
            /// </summary>
            public List<LaunchParameter> GlobalParameters { get; set; } = new();

            /// <summary>
            /// LaunchComplex level <see cref="LaunchParameter"/>s.
            /// </summary>
            // ReSharper disable once CollectionNeverUpdated.Global -> Not yet set but might be in the future
            public List<LaunchParameter> LaunchComplexParameters { get; set; } = new();

            /// <summary>
            /// LaunchPad level <see cref="LaunchParameter"/>s.
            /// </summary>
            // ReSharper disable once CollectionNeverUpdated.Global -> Not yet set but might be in the future
            public List<LaunchParameter> LaunchPadParameters { get; set; } = new();

            /// <summary>
            /// Does the contain contain anything.
            /// </summary>
            public bool Any => GlobalParameters.Any() || LaunchComplexParameters.Any() || LaunchPadParameters.Any();
        }
    }
}
