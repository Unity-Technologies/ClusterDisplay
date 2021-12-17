using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [CustomEditor(typeof(ClusterRenderer))]
    class ClusterRendererInspector : UnityEditor.Editor
    {
        const string k_NoCamerasMessage = "No cameras are marked to render in this cluster.";
        const string k_AddCameraScriptText = "Add ClusterCamera component to all cameras";

        SerializedProperty m_PolicyProp;
        SerializedProperty m_OverscanProp;

        NestedInspector m_PolicyEditor;

        // We need to detect when the projection policy has changed. Caching the previous
        // value seems to be the only reliable way to detect changes. Can we do better?
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
#endif
            CheckForClusterCameraComponents();

            serializedObject.Update();
            var currentPolicy = m_PolicyProp.objectReferenceValue;

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(m_PolicyProp, Labels.GetGUIContent(Labels.Field.ProjectionPolicy));
                // Make sure we are drawing the correct editor for the select policy.
                // This detects if the policy has changed. Not all changes come from the Inspector.
                // e.g. The scene undo stack could change it.
                if (check.changed || m_CachedPolicyObject != currentPolicy)
                {
                    if (m_PolicyEditor != null)
                    {
                        DestroyImmediate(m_PolicyEditor);
                    }
                    
                    if (currentPolicy != null || m_PolicyEditor == null)
                    {
                        m_PolicyEditor = CreateEditor(currentPolicy) as NestedInspector;
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
