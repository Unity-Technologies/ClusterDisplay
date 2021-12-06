using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        public static void DrawSettings(TiledProjectionSettings settings)
        {
            settings.GridSize = GUIVector2IntField("Grid", settings.GridSize);
            settings.PhysicalScreenSize = GUIVector2Field("Physical Screen Size", settings.PhysicalScreenSize);
            settings.Bezel = GUIVector2Field("Bezel", settings.Bezel);
            GUILayout.Label("Press <b>[O]</b> then use <b>left/right</b> arrows to decrease/increase");
        }

        public static void DrawDebugSettings(TiledProjectionDebugSettings tileDebugSettings)
        {
            tileDebugSettings.TileIndexOverride = GUIIntField("Tile Index Override", tileDebugSettings.TileIndexOverride);
            tileDebugSettings.EnableKeyword = GUILayout.Toggle(tileDebugSettings.EnableKeyword, "Enable Keyword");

            var currentLayoutMode = Enum.GetName(typeof(LayoutMode), tileDebugSettings.LayoutMode);
            var layoutModes = Enum.GetNames(typeof(LayoutMode));
            GUILayout.Label("Layout Modes");
            for (var i = 0; i < layoutModes.Length; i++)
            {
                if (layoutModes[i] == currentLayoutMode)
                {
                    if (GUILayout.Button($"{currentLayoutMode} (Active)")) { }

                    continue;
                }

                if (GUILayout.Button(layoutModes[i]))
                {
                    tileDebugSettings.LayoutMode = (LayoutMode)Enum.Parse(typeof(LayoutMode), layoutModes[i]);
                }
            }

            tileDebugSettings.UseDebugViewportSubsection = GUILayout.Toggle(tileDebugSettings.UseDebugViewportSubsection, "Debug Viewport Section");

            if (tileDebugSettings.UseDebugViewportSubsection)
            {
                GUILayout.Label("Viewport Section");
                var rect = tileDebugSettings.ViewportSubsection;
                var xMin = rect.xMin;
                var xMax = rect.xMax;
                var yMin = rect.yMin;
                var yMax = rect.yMax;

                xMin = GUISlider("xMin", xMin, 0, 1);
                xMax = GUISlider("xMax", xMax, 0, 1);
                yMin = GUISlider("yMin", yMin, 0, 1);
                yMax = GUISlider("yMax", yMax, 0, 1);
                tileDebugSettings.ViewportSubsection = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }

            GUILayout.Label("Scale Bias Offset");
            var offset = tileDebugSettings.ScaleBiasTextOffset;
            offset.x = GUISlider("x", offset.x, -1, 1);
            offset.y = GUISlider("y", offset.y, -1, 1);
            tileDebugSettings.ScaleBiasTextOffset = offset;
        }

        // introduce keyboard controls to make up for lack of IMGUI support with Cluster Display
        public static void KeyboardControls(ClusterRendererSettings settings)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current.oKey.isPressed)
            {
                var overscan = settings.overScanInPixels;
                if (Keyboard.current.rightArrowKey.isPressed)
                    ++overscan;
                else if (Keyboard.current.leftArrowKey.isPressed)
                    --overscan;
                settings.overScanInPixels = Mathf.Clamp(overscan, 0, 256);
            }

#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.O))
            {
                var overscan = settings.OverScanInPixels;
                if (Input.GetKey(KeyCode.RightArrow))
                {
                    ++overscan;
                }
                else if (Input.GetKey(KeyCode.LeftArrow))
                {
                    --overscan;
                }

                settings.OverScanInPixels = Mathf.Clamp(overscan, 0, 256);
            }
#endif
        }
    }
}
