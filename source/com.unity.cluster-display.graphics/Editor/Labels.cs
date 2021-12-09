using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    // A centralized place to store tooltip messages.
    // As they may be used for custom inspectors and runtime GUI, etc...
    static class Labels
    {
        public enum Field
        {
            GridSize,
            PhysicalScreenSize,
            Bezel,
            BezelColor,
            Overscan,
            Debug,
            TileIndexOverride,
            NodeIndexOverride,
            ProjectionSurfaces,
            Keyword,
            LayoutMode,
            DebugViewportSubsection,
            ScaleBiasOffset,
            ProjectionPolicy,
            DefaultProjectionSurface,
        }

        static string GetName(Field field)
        {
            switch (field)
            {
                case Field.GridSize: return "Grid Size";
                case Field.PhysicalScreenSize: return "Physical Screen Size";
                case Field.Bezel: return "Bezel";
                case Field.BezelColor: return "Bezel Color";
                case Field.Overscan: return "Overscan";
                case Field.Debug: return "Debug Mode";
                case Field.TileIndexOverride: return "Tile Index Override";
                case Field.NodeIndexOverride: return "Node Index Override";
                case Field.ProjectionSurfaces: return "Projection Surfaces";
                case Field.Keyword: return "Keyword";
                case Field.LayoutMode: return "Layout Mode";
                case Field.DebugViewportSubsection: return "Debug Viewport Subsection";
                case Field.ScaleBiasOffset: return "Scale Bias Offset";
                case Field.ProjectionPolicy: return "Projection Policy";
                case Field.DefaultProjectionSurface: return "New default (planar) surface";
            }

            return string.Empty;
        }

        static string GetTooltip(Field field)
        {
            switch (field)
            {
                case Field.GridSize: return "Number of displays per row and column.";
                case Field.PhysicalScreenSize: return "Physical size of a display (not to be confused with screen size in pixels).";
                case Field.Bezel: return "Physical size of display bezels.";
                case Field.BezelColor: return "Color of bezels in stitcher layout mode.";
                case Field.Overscan: return "Amount of overscan in pixels.";
                case Field.Debug: return "Activate/Deactivate debug mode.";
                case Field.TileIndexOverride: return "Tile index to be used when there is no cluster network.";
                case Field.NodeIndexOverride: return "Node index to be used when there is no cluster network.";
                case Field.ProjectionSurfaces: return "Collection of surfaces (screens) representing the display cluster.";
                case Field.Keyword: return "Activate/Deactivate cluster display shading features.";
                case Field.LayoutMode: return "Select various layout modes for visualization.";
                case Field.DebugViewportSubsection: return "Activate/Deactivate direct viewport control, bypassing tile index completely.";
                case Field.ScaleBiasOffset: return "Compositing offset allowing for overscanned pixels visualization.";
                case Field.ProjectionPolicy: return "The method with which the content is projected for display.";
            }

            return string.Empty;
        }

        public static GUIContent GetGUIContent(Field field)
        {
            return new GUIContent(GetName(field), GetTooltip(field));
        }
    }
}
