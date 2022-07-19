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
        const string k_NoPolicyMessage = "No projection policy assigned. You can create a new Projection Policy using the \"Create/Cluster Display\" menu.";

        SerializedProperty m_PolicyProp;
        SerializedProperty m_OverscanProp;
        SerializedProperty m_DelayPresentByOneFrameProp;

        NestedInspector m_PolicyEditor;

        // We need to detect when the projection policy has changed. Caching the previous
        // value seems to be the most reliable way to detect changes.
        ProjectionPolicy m_CachedPolicyObject;

        void OnEnable()
        {
            m_PolicyProp = serializedObject.FindProperty("m_ProjectionPolicy");
            m_OverscanProp = serializedObject.FindProperty("m_Settings.m_OverscanInPixels");
            m_DelayPresentByOneFrameProp = serializedObject.FindProperty("m_DelayPresentByOneFrame");
        }

        public override void OnInspectorGUI()
        {
#if CLUSTER_DISPLAY_URP
            RenderFeatureEditorUtils<ClusterRenderer, InjectionPointRenderFeature>.OnInspectorGUI();
#endif
            CheckForClusterCameraComponents();

            serializedObject.Update();
            EditorGUILayout.PropertyField(m_PolicyProp, Labels.GetGUIContent(Labels.Field.ProjectionPolicy));

            var currentPolicy = m_PolicyProp.objectReferenceValue as ProjectionPolicy;

            // Make sure we are drawing the correct editor for the selected policy.
            // This hacky method detects if the policy has changed, either triggered by the user through
            // the Inspector, or some other logic (e.g. the undo system)
            if (m_CachedPolicyObject != currentPolicy)
            {
                if (m_PolicyEditor != null)
                {
                    DestroyImmediate(m_PolicyEditor);
                }

                if (m_CachedPolicyObject != null)
                {
                    m_CachedPolicyObject.OnDisable();
                }

                m_CachedPolicyObject = currentPolicy;
            }

            if (currentPolicy != null && m_PolicyEditor == null)
            {
                m_PolicyEditor = CreateEditor(currentPolicy) as NestedInspector;
            }

            EditorGUILayout.PropertyField(m_OverscanProp, Labels.GetGUIContent(Labels.Field.Overscan));
            EditorGUILayout.PropertyField(m_DelayPresentByOneFrameProp, Labels.GetGUIContent(Labels.Field.DelayPresentByOneFrame));

            if (currentPolicy != null)
            {
                Debug.Assert(m_PolicyEditor != null);
                m_PolicyEditor.OnInspectorGUI();
            }
            else
            {
                EditorGUILayout.HelpBox(k_NoPolicyMessage, MessageType.Warning);
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

        internal static void AddMissingClusterCameraComponents()
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
