﻿using System.Collections.Generic;
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
            if (!ClusterDisplaySettings.CurrentSettings.MissionControlSettings.Instrument)
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

                var missionControlSettings = ClusterDisplaySettings.CurrentSettings.MissionControlSettings;
                if (ClusterRenderer.TryGetInstance(out var clusterRenderer, logError: false, includeInactive: false))
                {
                    missionControlSettings.PolicyParameters = clusterRenderer.ProjectionPolicy switch
                    {
                        TiledProjection => AddTiledProjectionPolicyParameters(),
                        _ => new()
                    };
                    if (missionControlSettings.PolicyParameters.Any)
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
        static MissionControlSettings.ParametersContainer AddTiledProjectionPolicyParameters()
        {
            MissionControlSettings.ParametersContainer ret = new();
            AddVector2IntParameter(ret.GlobalParameters, "Tile grid size", TiledProjection.GridSizeParameterId,
                "Number of displays per row and column.");
            AddVector2IntParameter(ret.GlobalParameters, "Physical screen size", TiledProjection.PhysicalScreenSizeParameterId,
                "Physical size of a display (not to be confused with screen size in pixels).");
            AddVector2IntParameter(ret.GlobalParameters, "Bezel", TiledProjection.BezelParameterId,
                "Physical size of display bezels.");
            return ret;
        }

        /// <summary>
        /// Add a Vector2Int <see cref="LaunchParameter"/> to the list.
        /// </summary>
        /// <param name="list">List to which to add the parameters.</param>
        /// <param name="name">Display name of the parameter.</param>
        /// <param name="id">Identifier of the parameter.</param>
        /// <param name="description">Description of the parameter.</param>
        static void AddVector2IntParameter(List<LaunchParameter> list, string name, string id, string description)
        {
            list.Add(new()
            {
                Name = "X", Group = name, Id = $"{id}.X", Description = description, Type = LaunchParameterType.Integer,
                Constraint = new RangeConstraint() {Min = 0}, DefaultValue = 0
            });
            list.Add(new()
            {
                Name = "Y", Group = name, Id = $"{id}.Y", Description = description, Type = LaunchParameterType.Integer,
                Constraint = new RangeConstraint() {Min = 0}, DefaultValue = 0
            });
        }
    }
}
