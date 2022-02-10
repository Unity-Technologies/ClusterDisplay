using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
#if CLUSTER_DISPLAY_URP
using UnityEngine.Rendering.Universal;
#endif
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [CustomEditor(typeof(ClusterRenderer))]
    class ClusterRendererInspector : UnityEditor.Editor
    {
        const string k_NoCamerasMessage = "No cameras are marked to render in this cluster.";
        const string k_AddCameraScriptText = "Add ClusterCamera component to all cameras";
        const string k_NoPolicyMessage = "No projection policy assigned. You can create a new Projection Policy using the \"Create/Cluster Display\" menu.";
        const string k_UrpAssetDoesNotSupportScreenCoordOverride = "Universal Render Pipeline asset does not use Screen Coordinates Override. You can fix this by selecting the \"Screen Coordinates Override\" checkbox in the \"Post-processing\" section.";

        SerializedProperty m_PolicyProp;
        SerializedProperty m_OverscanProp;

        NestedInspector m_PolicyEditor;

        // We need to detect when the projection policy has changed. Caching the previous
        // value seems to be the most reliable way to detect changes.
        Object m_CachedPolicyObject;

        void OnEnable()
        {
            m_PolicyProp = serializedObject.FindProperty("m_ProjectionPolicy");
            m_OverscanProp = serializedObject.FindProperty("m_Settings.m_OverscanInPixels");
        }

        public override void OnInspectorGUI()
        {
#if CLUSTER_DISPLAY_URP
            RenderFeatureEditorUtils<ClusterRenderer, InjectionPointRenderFeature>.OnInspectorGUI();

            if (UniversalRenderPipeline.asset is not { useScreenCoordOverride: true })
            {
                EditorGUILayout.HelpBox(k_UrpAssetDoesNotSupportScreenCoordOverride, MessageType.Warning);
            }
#endif
            CheckForClusterCameraComponents();

            serializedObject.Update();
            var currentPolicy = m_PolicyProp.objectReferenceValue;

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(m_PolicyProp, Labels.GetGUIContent(Labels.Field.ProjectionPolicy));
                // Make sure we are drawing the correct editor for the selected policy.
                // This detects if the policy has changed. Not all changes come from the Inspector.
                // e.g. The scene undo stack could change it.
                if (check.changed || m_CachedPolicyObject != currentPolicy)
                {
                    if (m_PolicyEditor != null)
                    {
                        DestroyImmediate(m_PolicyEditor);
                    }
                    
                    if (currentPolicy != null)
                    {
                        if (m_PolicyEditor == null)
                        {
                            m_PolicyEditor = CreateEditor(currentPolicy) as NestedInspector;
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(k_NoPolicyMessage, MessageType.Warning);
                    }
                }
            }

            EditorGUILayout.PropertyField(m_OverscanProp, Labels.GetGUIContent(Labels.Field.Overscan));
            
            if (currentPolicy != null && m_PolicyEditor != null)
            {
                m_PolicyEditor.OnInspectorGUI();
                m_CachedPolicyObject = currentPolicy;
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        void OnSceneGUI()
        {
            if (m_PolicyEditor != null)
            {
                m_PolicyEditor.OnSceneGUI();
            }
        }

        void OnDestroy()
        {
            if (m_PolicyEditor != null)
            {
                DestroyImmediate(m_PolicyEditor);
            }
        }

        static void CheckForClusterCameraComponents()
        {
            if (!SceneUtils.FindAllObjectsInScene<ClusterCamera>().Any())
            {
                EditorGUILayout.HelpBox(k_NoCamerasMessage, MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(k_AddCameraScriptText))
                    {
                        AddMissingClusterCameraComponents();
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        static void AddMissingClusterCameraComponents()
        {
            foreach (var camera in SceneUtils.FindAllObjectsInScene<Camera>())
            {
                if (camera.GetComponent<ClusterRenderer>() == null && camera.GetComponent<ClusterCamera>() == null)
                {
                    camera.gameObject.AddComponent<ClusterCamera>();
                }
            }
        }
    }
}
