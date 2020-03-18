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
            Overscan,
            Debug,
            TileIndexOverride,
            Keyword,
            Stitcher,
            DebugViewportSubsection,
            ScaleBiasOffset,
        }
        
        static string GetName(Field field)
        {
            switch (field)
            {
                case Field.GridSize:                return "Grid Size";
                case Field.PhysicalScreenSize:      return "Physical Screen Size";
                case Field.Bezel:                   return "Bezel";
                case Field.Overscan:                return "Overscan in Pixels";
                case Field.Debug:                   return "Debug";
                case Field.TileIndexOverride:       return "Tile Index Override";
                case Field.Keyword:                 return "Keyword";
                case Field.Stitcher:                return "Stitcher";
                case Field.DebugViewportSubsection: return "Debug Viewport Subsection";
                case Field.ScaleBiasOffset:         return "Scale Bias Offset";
            }

            return string.Empty;
        }
        
        static string GetTooltip(Field field)
        {
            switch (field)
            {
                case Field.GridSize:                return "Number of displays per row and column.";
                case Field.PhysicalScreenSize:      return "Physical size of a display (not to be confused with screen size in pixels).";
                case Field.Bezel:                   return "Physical size of display bezels.";
                case Field.Overscan:                return "Amount of overscan in pixels.";
                case Field.Debug:                   return "Activate/Deactivate debug mode.";
                case Field.TileIndexOverride:       return "Tile index to be used in debug mode, overrides network tile index.";
                case Field.Keyword:                 return "Activate/Deactivate cluster display shading features.";
                case Field.Stitcher:                return "Activate/Deactivate stitcher.";
                case Field.DebugViewportSubsection: return "Activate/Deactivate direct viewport control, bypassing tile index completely.";
                case Field.ScaleBiasOffset:         return "Compositing offset allowing for overscanned pixels visualization.";
            }

            return string.Empty;
        }

        public static GUIContent GetGUIContent(Field field)
        {
            return new GUIContent(GetName(field), GetTooltip(field));
        }
    }
}
