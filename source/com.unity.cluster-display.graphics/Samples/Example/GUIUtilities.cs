using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Example
{
    static class GUIUtilities
    {
        static float GUISlider(string label, float value, float min, float max)
        {
            GUILayout.Label($"{label}: {value.ToString()}");
            return GUILayout.HorizontalSlider(value, min, max);
        }

        static int GUIIntSlider(string label, int value, int min, int max)
        {
            GUILayout.Label($"{label}: {value.ToString()}");
            return Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
        }

        static int GUIIntField(string label, int value)
        {
            var str = value.ToString();
            GUILayout.Label($"{label}: {str}");
            str = GUILayout.TextField(str);
            int.TryParse(str, out value);
            return value;
        }
        
        static float GUIFloatField(string label, float value)
        {
            var str = value.ToString("0.00");
            GUILayout.Label($"{label}: {str}");
            str = GUILayout.TextField(str);
            float.TryParse(str, out value);
            return value;
        }

        static Vector2Int GUIVector2IntField(string label, Vector2Int value)
        {
            GUILayout.Label(label);
            value.x = GUIIntField("x", value.x);
            value.y = GUIIntField("y", value.y);
            return value;
        }
        
        static Vector2 GUIVector2Field(string label, Vector2 value)
        {
            GUILayout.Label(label);
            value.x = GUIFloatField("x", value.x);
            value.y = GUIFloatField("y", value.y);
            return value;
        }

        public static void DrawSettings(ClusterRendererSettings settings)
        {
            settings.GridSize = GUIVector2IntField("Grid", settings.GridSize);
            settings.PhysicalScreenSize = GUIVector2Field("Physical Screen Size", settings.PhysicalScreenSize);
            settings.Bezel = GUIVector2Field("Bezel", settings.Bezel);
            settings.OverscanInPixels = GUIIntSlider("Overscan In Pixels", settings.OverscanInPixels, 0, 256);
            GUILayout.Label("Press <b>[O]</b> then use <b>left/right</b> arrows to decrease/increase");
        }

        public static void DrawDebugSettings(ClusterRendererDebugSettings settings)
        {
            settings.TileIndexOverride = GUIIntField("Tile Index Override", settings.TileIndexOverride);
            settings.EnableKeyword = GUILayout.Toggle(settings.EnableKeyword, "Enable Keyword");
            settings.EnableStitcher = GUILayout.Toggle(settings.EnableStitcher, "Enable Stitcher");
            settings.UseDebugViewportSubsection = GUILayout.Toggle(settings.UseDebugViewportSubsection, "Debug Viewport Section");

            if (settings.UseDebugViewportSubsection)
            {
                GUILayout.Label("Viewport Section");
                var rect = settings.ViewportSubsection;
                float xMin = rect.xMin;
                float xMax = rect.xMax;
                float yMin = rect.yMin;
                float yMax = rect.yMax;

                xMin = GUISlider("xMin", xMin, 0, 1);
                xMax = GUISlider("xMax", xMax, 0, 1);
                yMin = GUISlider("yMin", yMin, 0, 1);
                yMax = GUISlider("yMax", yMax, 0, 1);
                settings.ViewportSubsection = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }

            GUILayout.Label("Scale Bias Offset");
            var offset = settings.ScaleBiasTexOffset;
            offset.x = GUISlider("x", offset.x, -1, 1);
            offset.y = GUISlider("y", offset.y, -1, 1);
            settings.ScaleBiasTexOffset = offset;
        }

        // introduce keyboard controls to make up for lack of IMGUI support with Cluster Display
        public static void KeyboardControls(ClusterRendererSettings settings)
        {
            if (Input.GetKey(KeyCode.O))
            {
                var overscan = settings.OverscanInPixels;
                if (Input.GetKey(KeyCode.RightArrow))
                    ++overscan;
                else if (Input.GetKey(KeyCode.LeftArrow))
                    --overscan;
                settings.OverscanInPixels = Mathf.Clamp(overscan, 0, 256);
            }
        }
    }
}
