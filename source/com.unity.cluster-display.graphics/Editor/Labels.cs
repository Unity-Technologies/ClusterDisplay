using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    // TODO use static GUIContents
    // A centralized place to store tooltip messages and other messages.
    // As they may be used for custom inspectors and runtime GUI, etc...
    static class Labels
    {
        public enum Field
        {
            GridSize,
            PhysicalScreenSize,
            Bezel,
            PresentClearColor,
            Overscan,
            Debug,
            TileIndexOverride,
            PositionNonFullscreenWindows,
            NodeIndexOverride,
            ProjectionSurfaces,
            Keyword,
            ScreenCoordOverride,
            LayoutMode,
            DebugViewportSubsection,
            ScaleBiasOffset,
            ProjectionPolicy,
            DefaultProjectionSurface,
            DelayPresentByOneFrame,
            ForceClearHistory
        }

        static string GetName(Field field)
        {
            switch (field)
            {
                case Field.GridSize: return "Grid Size";
                case Field.PhysicalScreenSize: return "Physical Screen Size";
                case Field.Bezel: return "Bezel";
                case Field.PresentClearColor: return "Present Clear Color";
                case Field.Overscan: return "Overscan";
                case Field.Debug: return "Debug Mode";
                case Field.TileIndexOverride: return "Tile Index Override";
                case Field.PositionNonFullscreenWindows: return "Position Windows";
                case Field.NodeIndexOverride: return "Node Index Override";
                case Field.ProjectionSurfaces: return "Projection Surfaces";
                case Field.Keyword: return "Keyword";
                case Field.ScreenCoordOverride: return "Screen Coord Override";
                case Field.LayoutMode: return "Layout Mode";
                case Field.DebugViewportSubsection: return "Debug Viewport Subsection";
                case Field.ScaleBiasOffset: return "Scale Bias Offset";
                case Field.ProjectionPolicy: return "Projection Policy";
                case Field.DefaultProjectionSurface: return "New default (planar) surface";
                case Field.DelayPresentByOneFrame: return "Delay Present By One Frame";
                case Field.ForceClearHistory: return "Force Clear History";
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
                case Field.PresentClearColor: return "Color of bezels in stitcher layout mode.";
                case Field.Overscan: return "Amount of overscan in pixels.";
                case Field.Debug: return "Activate/Deactivate debug mode.";
                case Field.TileIndexOverride: return "Tile index to be used when there is no cluster network.";
                case Field.PositionNonFullscreenWindows: return "Position non fullscreen windows to match layout of physical screens represented by windows.";
                case Field.NodeIndexOverride: return "Node index to be used when there is no cluster network.";
                case Field.ProjectionSurfaces: return "Collection of surfaces (screens) representing the display cluster.";
                case Field.Keyword: return "Activate/Deactivate cluster display shading features.";
                case Field.ScreenCoordOverride: return "Activate/Deactivate cluster display shading features.";
                case Field.LayoutMode: return "Select various layout modes for visualization.";
                case Field.DebugViewportSubsection: return "Activate/Deactivate direct viewport control, bypassing tile index completely.";
                case Field.ScaleBiasOffset: return "Compositing offset allowing for overscanned pixels visualization.";
                case Field.ProjectionPolicy: return "The method with which the content is projected for display.";
                case Field.DelayPresentByOneFrame: return "If true, delays presentation by one frame.";
                case Field.ForceClearHistory: return "If true, clears HDRP accumulation buffers (preventing potential issues related to motion vectors caused by Standard Stitcher Layout Mode).";
            }

            return string.Empty;
        }

        public static GUIContent GetGUIContent(Field field)
        {
            return new GUIContent(GetName(field), GetTooltip(field));
        }

        public enum MessageID
        {
            StandardStitcherWarning
        }

        public static string GetMessage(MessageID message)
        {
            switch (message)
            {
                case MessageID.StandardStitcherWarning: return "Standard Stitcher mode does not support camera " +
                    "persistent history (used by effects like motion blur).  Try activating the Force Clear History " +
                    "option below to lessen the problem (but disable parts of those effects).";
            }

            return string.Empty;
        }
    }
}
