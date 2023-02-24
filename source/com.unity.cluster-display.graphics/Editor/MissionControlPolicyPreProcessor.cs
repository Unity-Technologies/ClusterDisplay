using System.Collections.Generic;
using Unity.ClusterDisplay.MissionControl;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Build PreProcessor that detect if we need to launch parameters from policies.
    /// </summary>
    public class MissionControlPolicyPreProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!MissionControlSettings.Current.Instrument)
            {
                return;
            }

            // Ensure projection policies launch parameters are up to date.
            var allScenesInBuild = EditorBuildSettings.scenes;
            foreach (var buildScene in allScenesInBuild)
            {
                if (!buildScene.enabled)
                {
                    continue;
                }

                var scene = SceneManager.GetSceneByPath(buildScene.path);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                }

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                var missionControlSettings = MissionControlSettings.Current;
                if (ClusterRenderer.TryGetInstance(out var clusterRenderer, logError: false, includeInactive: false))
                {
                    if (clusterRenderer.ProjectionPolicy is TiledProjection)
                    {
                        AddTiledProjectionPolicyParameters();
                    }
                    if (clusterRenderer.ProjectionPolicy.SupportsTestPattern)
                    {
                        MissionControlParameters.Instance.GlobalParameters.Add(new()
                        {
                            Name = "Show Test Pattern",
                            Id = ProjectionPolicy.TestPatternParameterId,
                            Description = "Show the test pattern instead of rendering the game.",
                            Type = LaunchParameterType.Boolean,
                            DefaultValue = false
                        });
                    }
                    if (MissionControlParameters.Instance.Any)
                    {
                        // Ensure we have a ClusterRenderMissionControlUtils on the clusterRender.
                        if (clusterRenderer.GetComponent<ClusterRendererMissionControlUtils>() == null)
                        {
                            clusterRenderer.gameObject.AddComponent<ClusterRendererMissionControlUtils>();
                            EditorSceneManager.SaveScene(scene);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Returns a list of <see cref="LaunchParameter"/>s to allow customization of a <see cref="TiledProjection"/>.
        /// </summary>
        static void AddTiledProjectionPolicyParameters()
        {
            AddVector2IntParameter(MissionControlParameters.Instance.GlobalParameters,
                "Tile grid size", TiledProjection.GridSizeParameterId,"Number of horizontal displays.",
                "Number of vertical displays.");
            AddVector2IntParameter(MissionControlParameters.Instance.GlobalParameters, "Physical screen size",
                TiledProjection.PhysicalScreenSizeParameterId,
                "Physical width of a display (not to be confused with screen size in pixels).",
                "Physical height of a display (not to be confused with screen size in pixels).");
            AddVector2IntParameter(MissionControlParameters.Instance.GlobalParameters, "Bezel",
                TiledProjection.BezelParameterId, "Physical width of display bezels.",
                "Physical height of display bezels.");
        }

        /// <summary>
        /// Add a Vector2Int <see cref="LaunchParameter"/> to the list.
        /// </summary>
        /// <param name="list">List to which to add the parameters.</param>
        /// <param name="name">Display name of the parameter.</param>
        /// <param name="id">Identifier of the parameter.</param>
        /// <param name="descriptionX">Description of the X parameter.</param>
        /// <param name="descriptionY">Description of the Y parameter.</param>
        static void AddVector2IntParameter(List<LaunchParameter> list, string name, string id, string descriptionX,
            string descriptionY)
        {
            list.Add(new()
            {
                Name = "X", Group = name, Id = $"{id}.X", Description = descriptionX, Type = LaunchParameterType.Integer,
                Constraint = new RangeConstraint() {Min = 0}, DefaultValue = 0
            });
            list.Add(new()
            {
                Name = "Y", Group = name, Id = $"{id}.Y", Description = descriptionY, Type = LaunchParameterType.Integer,
                Constraint = new RangeConstraint() {Min = 0}, DefaultValue = 0
            });
        }
    }
}
