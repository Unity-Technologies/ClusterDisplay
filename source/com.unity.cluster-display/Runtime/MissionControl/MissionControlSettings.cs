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

        protected override void InitializeInstance()
        {
            m_QuitTimeout = 15.0f;
        }
    }
}
