using Newtonsoft.Json.Linq;
using Unity.ClusterDisplay.MissionControl;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// A utility whose purpose is to configure the Cluster Renderer based on mission control parameters.
    /// Introduced to avoid a having direct references to mission control parameters in the Cluster Renderer,
    /// since it would hamper testability.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ClusterRenderer))]
    public class ClusterRendererMissionControlUtils : MonoBehaviour
    {
        // This needs to execute after the main manager has initialized and before the renderer does.
        // We can manage with default execution order.
        // Ultimately architecture simplification will make this more straightforward / less fragile.
        void OnEnable()
        {
            var clusterRenderer = GetComponent<ClusterRenderer>();
            Assert.IsNotNull(clusterRenderer);

            if (ServiceLocator.TryGet(out IClusterSyncState clusterSync) &&
                clusterSync.NodeRole is NodeRole.Emitter &&
                clusterSync.EmitterIsHeadless)
            {
                clusterRenderer.enabled = false;
                return;
            }

            clusterRenderer.DelayPresentByOneFrame =
                clusterSync.NodeRole is NodeRole.Emitter && clusterSync.RepeatersDelayedOneFrame;

            if (Application.isPlaying && clusterRenderer.ProjectionPolicy is { } projectionPolicy)
            {
                ApplyMissionControlLaunchParameters(projectionPolicy);

                if (projectionPolicy.SupportsTestPattern &&
                    MissionControlLaunchConfiguration.Instance?.RawLaunchData?.Value<bool>(ProjectionPolicy
                            .TestPatternParameterId) is
                        { } showTestPattern)
                {
                    clusterRenderer.Settings.RenderTestPattern = showTestPattern;
                }
            }
        }

        static void ApplyMissionControlLaunchParameters(ProjectionPolicy projection)
        {
            switch (projection)
            {
                case TiledProjection tiledProjection:
                    var settings = tiledProjection.Settings;
                    ApplySettings(ref settings);
                    tiledProjection.Settings = settings;
                    break;
            }
        }

        static void ApplySettings(ref TiledProjectionSettings baseSettings)
        {
            var launchData = MissionControlLaunchConfiguration.Instance?.RawLaunchData;

            if (TryGetVector2Int(launchData, TiledProjection.BezelParameterId, out var bezelValue))
            {
                baseSettings.Bezel = bezelValue;
            }

            if (TryGetVector2Int(launchData, TiledProjection.GridSizeParameterId, out var gridSizeValue))
            {
                baseSettings.GridSize = gridSizeValue;
            }

            if (TryGetVector2Int(launchData, TiledProjection.PhysicalScreenSizeParameterId, out var physicalSizeValue))
            {
                baseSettings.PhysicalScreenSize = physicalSizeValue;
            }

            var positionWindows = launchData?.Value<bool?>(TiledProjection.PositionWindowsParameterId);
            if (positionWindows.HasValue)
            {
                baseSettings.PositionNonFullscreenWindow = positionWindows.Value;
            }
        }

        static bool TryGetVector2Int(JObject rawLaunchData, string parameterName, out Vector2Int value)
        {
            var xValue = rawLaunchData.Value<int?>($"{parameterName}.X");
            var yValue = rawLaunchData.Value<int?>($"{parameterName}.Y");
            if (xValue is > 0 && yValue is > 0)
            {
                value = new Vector2Int(xValue.Value, yValue.Value);
                return true;
            }
            else
            {
                value = Vector2Int.zero;
                return false;
            }
        }
    }
}
