using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// A utility whose purpose is to configure the Cluster Renderer based on command line parameters.
    /// Introduced to avoid a having direct references to command line parameters in the Cluster Renderer,
    /// since it would hamper testability.
    /// </summary>
    [ExecuteAlways]
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

            if (ClusterDisplayState.IsEmitter && ClusterDisplayState.EmitterIsHeadless)
            {
                renderer.enabled = false;
                return;
            }

            renderer.DelayPresentByOneFrame = CommandLineParser.emitterSpecified && CommandLineParser.delayRepeaters;

            if (Application.isPlaying && renderer.ProjectionPolicy is ProjectionPolicy projectionPolicy)
            {
                ApplyCmdLineSettings(projectionPolicy);
            }
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
            TrySet(ref baseSettings.Bezel, CommandLineParser.bezel);
            TrySet(ref baseSettings.GridSize, CommandLineParser.gridSize);
            TrySet(ref baseSettings.PhysicalScreenSize, CommandLineParser.physicalScreenSize);
        }
    }
}
