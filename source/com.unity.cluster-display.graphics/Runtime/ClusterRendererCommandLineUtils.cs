using System.Runtime.CompilerServices;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    static class ClusterRendererCommandLineUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void TrySet<T>(ref T dest, T? maybeValue) where T : struct
        {
            dest = maybeValue ?? dest;
        }

        internal static void ApplyCmdLineSettings(this ProjectionPolicy projection)
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

        static void ParseSettings(ref this TiledProjectionSettings baseSettings)
        {
            TrySet(ref baseSettings.Bezel, CommandLineParser.bezel);
            TrySet(ref baseSettings.GridSize, CommandLineParser.gridSize);
            TrySet(ref baseSettings.PhysicalScreenSize, CommandLineParser.physicalScreenSize);
        }
    }
}
