using System;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay.Graphics.Inspectors
{
    [CustomEditor(typeof(ClusterRenderer))]
    class ClusterRendererInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var adapter = target as ClusterRenderer;

                var settings = adapter.settings;
                settings.Resources = (ClusterDisplayResources)EditorGUILayout.ObjectField(settings.Resources, typeof(ClusterDisplayResources), false);
                settings.GridSize = EditorGUILayout.Vector2IntField(Labels.GetGUIContent(Labels.Field.GridSize), settings.GridSize);
                settings.PhysicalScreenSize = EditorGUILayout.Vector2Field(Labels.GetGUIContent(Labels.Field.PhysicalScreenSize), settings.PhysicalScreenSize);
                settings.Bezel = EditorGUILayout.Vector2Field(Labels.GetGUIContent(Labels.Field.Bezel), settings.Bezel);
                settings.OverScanInPixels = EditorGUILayout.IntSlider(Labels.GetGUIContent(Labels.Field.Overscan), settings.OverScanInPixels, 0, 256);

                adapter.context.debug = EditorGUILayout.Toggle(Labels.GetGUIContent(Labels.Field.Debug), adapter.context.debug);

                if (adapter.context.debug)
                    EditDebugSettings(adapter.debugSettings);

                if (check.changed)
                    EditorUtility.SetDirty(adapter);
            }
        }

        static void EditDebugSettings(ClusterRendererDebugSettings settings)
        {
            //settings.TileIndexOverride = EditorGUILayout.IntField("Tile Index Override", settings.TileIndexOverride);
            settings.tileIndexOverride = EditorGUILayout.IntField(Labels.GetGUIContent(Labels.Field.TileIndexOverride), settings.tileIndexOverride);
            settings.enableKeyword = EditorGUILayout.Toggle(Labels.GetGUIContent(Labels.Field.Keyword), settings.enableKeyword);
            settings.currentLayoutMode = (ClusterRenderer.LayoutMode)EditorGUILayout.EnumPopup(Labels.GetGUIContent(Labels.Field.LayoutMode), settings.currentLayoutMode);
            settings.useDebugViewportSubsection = EditorGUILayout.Toggle(Labels.GetGUIContent(Labels.Field.DebugViewportSubsection), settings.useDebugViewportSubsection);

            if (ClusterRenderer.LayoutModeIsStitcher(settings.currentLayoutMode))
                settings.bezelColor = EditorGUILayout.ColorField(Labels.GetGUIContent(Labels.Field.BezelColor), settings.bezelColor);

            // Let user manipulate viewport directly instead of inferring it from tile index.
            if (settings.useDebugViewportSubsection)
            {
                EditorGUILayout.LabelField("Viewport Section");

                var rect = settings.viewportSubsection;
                var xMin = rect.xMin;
                var xMax = rect.xMax;
                var yMin = rect.yMin;
                var yMax = rect.yMax;

                xMin = EditorGUILayout.Slider("xMin", xMin, 0, 1);
                xMax = EditorGUILayout.Slider("xMax", xMax, 0, 1);
                yMin = EditorGUILayout.Slider("yMin", yMin, 0, 1);
                yMax = EditorGUILayout.Slider("yMax", yMax, 0, 1);
                settings.viewportSubsection = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }

            EditorGUILayout.LabelField(Labels.GetGUIContent(Labels.Field.ScaleBiasOffset));
            var offset = settings.scaleBiasTextOffset;
            offset.x = EditorGUILayout.Slider("x", offset.x, -1, 1);
            offset.y = EditorGUILayout.Slider("y", offset.y, -1, 1);
            settings.scaleBiasTextOffset = offset;
        }
    }
}
