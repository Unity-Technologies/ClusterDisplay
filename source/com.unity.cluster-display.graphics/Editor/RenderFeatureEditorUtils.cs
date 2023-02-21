#if CLUSTER_DISPLAY_URP
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    /// <summary>
    /// Utility to add opt-in behavior for URP render features to custom editors.
    /// </summary>
    static class RenderFeatureEditorUtils<TComponent, TRenderFeature> where TRenderFeature : ScriptableRendererFeature
    {
        static readonly string k_FormatMaskNotAddedMessage = RendererFeatureNotAddedMessage(typeof(TComponent).Name, typeof(TRenderFeature).Name);
        static readonly GUIContent k_AddFormatMaskButtonLabel = AddRenderFeatureButtonLabel(typeof(TRenderFeature).Name);

        static string RendererFeatureNotAddedMessage(string componentName, string featureName)
        {
            return $"In order to render the {componentName}, your {nameof(UniversalRenderPipelineAsset)} must be modified. Click the button below to " +
                $"add the {featureName} renderer feature to {nameof(UniversalRenderPipelineAsset)}.";
        }

        static GUIContent AddRenderFeatureButtonLabel(string featureName)
        {
            return new GUIContent($"Add {featureName} renderer feature",
                $"Clicking this button will add the {featureName} renderer feature to your {nameof(UniversalRenderPipelineAsset)}.");
        }

        public static void OnInspectorGUI()
        {
            if (!UrpUtility.HasRenderFeature<TRenderFeature>(out _))
            {
                EditorGUILayout.HelpBox(k_FormatMaskNotAddedMessage, UnityEditor.MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(k_AddFormatMaskButtonLabel))
                    {
                        UrpUtility.AddRenderFeature<TRenderFeature>();
                        Debug.Log($"Added {typeof(TRenderFeature).Name} render feature to {nameof(UniversalRenderPipelineAsset)}.");
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }
    }
}
#endif
