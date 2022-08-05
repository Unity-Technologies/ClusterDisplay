using System;
using System.Runtime.CompilerServices;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// A utility whose purpose is to configure the Cluster Renderer based on command line parameters.
    /// Introduced to avoid a having direct references to command line parameters in the Cluster Renderer,
    /// since it would hamper testability.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ClusterRenderer))]
    // TODO Rename component?
    class ClusterRendererCommandLineUtils : MonoBehaviour
    {
        // This needs to execute after the main manager has initialized and before the renderer does.
        // We can manage with default execution order.
        // Ultimately architecture simplification will make this more straightforward / less fragile.
        void OnEnable()
        {
            var renderer = GetComponent<ClusterRenderer>();
            Assert.IsNotNull(renderer);

            if (ServiceLocator.TryGet(out IClusterSyncState clusterSync) &&
                clusterSync.NodeRole is NodeRole.Emitter &&
                clusterSync.EmitterIsHeadless)
            {
                renderer.enabled = false;
                return;
            }

            renderer.DelayPresentByOneFrame = clusterSync.NodeRole is NodeRole.Emitter && clusterSync.RepeatersDelayedOneFrame;

            // Only apply cmd line settings to the policy when not in the editor, as it otherwise cause problems with
            // affecting ScriptableObjects that keep modifications done to them in play mode.
#if !UNITY_EDITOR
            if (Application.isPlaying && renderer.ProjectionPolicy is { } projectionPolicy)
            {
                ApplyCmdLineSettings(projectionPolicy);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void TrySet<T>(ref T dest, T? maybeValue) where T : struct
        {
            dest = maybeValue ?? dest;
        }

        static void ApplyCmdLineSettings(ProjectionPolicy projection)
        {
            switch (projection)
            {
                case TiledProjection tiledProjection:
                    var settings = tiledProjection.Settings;
                    ParseSettings(ref settings);
                    tiledProjection.Settings = settings;
                    break;
            }
        }

        static void ParseSettings(ref TiledProjectionSettings baseSettings)
        {
            if (CommandLineParser.bezel.Defined)
                TrySet(ref baseSettings.Bezel, CommandLineParser.bezel.Value);

            if (CommandLineParser.gridSize.Defined)
                TrySet(ref baseSettings.GridSize, CommandLineParser.gridSize.Value);

            if (CommandLineParser.physicalScreenSize.Defined)
                TrySet(ref baseSettings.PhysicalScreenSize, CommandLineParser.physicalScreenSize.Value);
        }
    }
}
