using System.Collections.Generic;
using System.Linq;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Singleton object that survives domain reload and contains additional mission control launch parameters added
    /// by "optional components" that are not part of the "core" of Mission Control.
    /// </summary>
    /// <remarks>We have to use such a <see cref="ScriptableObject"/> to store that information because simply using
    /// some none serialized variable would cause that data to be flushed when a scene need to be saved (and saving a
    /// scene trigger a domain reload).</remarks>
    public class MissionControlParameters: ScriptableObject
    {
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static MissionControlParameters Instance
        {
            get
            {
                // Remark: Cannot use a static constructor otherwise I get the following exception:
                // UnityEngine.UnityException: CreateScriptableObjectInstanceFromType is not allowed to be called from a
                // ScriptableObject constructor (or instance field initializer), call it in OnEnable instead.
                if (s_Instance == null)
                {
                    s_Instance = CreateInstance<MissionControlParameters>();
                    s_Instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return s_Instance;
            }
        }

        /// <summary>
        /// Global <see cref="LaunchParameter"/>s.
        /// </summary>
        public List<LaunchParameter> GlobalParameters { get; } = new();

        /// <summary>
        /// LaunchComplex level <see cref="LaunchParameter"/>s.
        /// </summary>
        // ReSharper disable once CollectionNeverUpdated.Global -> Not yet set but might be in the future
        public List<LaunchParameter> LaunchComplexParameters { get; } = new();

        /// <summary>
        /// LaunchPad level <see cref="LaunchParameter"/>s.
        /// </summary>
        // ReSharper disable once CollectionNeverUpdated.Global -> Not yet set but might be in the future
        public List<LaunchParameter> LaunchPadParameters { get; } = new();

        /// <summary>
        /// Does the contain contain anything.
        /// </summary>
        public bool Any => GlobalParameters.Any() || LaunchComplexParameters.Any() || LaunchPadParameters.Any();

        /// <summary>
        /// Method called at the start of the build process to clear any parameters added during the previous build.
        /// </summary>
        public void Clear()
        {
            GlobalParameters.Clear();
            LaunchComplexParameters.Clear();
            LaunchPadParameters.Clear();
        }

        /// <summary>
        /// Storage of the singleton.
        /// </summary>
        static MissionControlParameters s_Instance;
    }
}
