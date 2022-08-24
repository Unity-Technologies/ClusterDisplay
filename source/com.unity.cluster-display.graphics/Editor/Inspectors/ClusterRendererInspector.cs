using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        const string k_NoPolicyMessage = "No projection policy assigned.";

        SerializedProperty m_PolicyProp;
        SerializedProperty m_OverscanProp;
        SerializedProperty m_DelayPresentByOneFrameProp;

        NestedInspector m_PolicyEditor;

        ClusterRenderer m_ClusterRenderer;

        static readonly TypeCache.TypeCollection k_ProjectionPolicyTypes;
        static readonly GUIContent[] k_ProjectionPolicyGUIOptions;

        static ClusterRendererInspector()
        {
            k_ProjectionPolicyTypes = TypeCache.GetTypesDerivedFrom<ProjectionPolicy>();
            k_ProjectionPolicyGUIOptions = k_ProjectionPolicyTypes
                .Select(t => new GUIContent(t.GetCustomAttribute<PopupItemAttribute>()?.ItemName ?? t.Name))
                .ToArray();
        }

        void OnEnable()
        {
            m_ClusterRenderer = target as ClusterRenderer;
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
            var currentPolicy = m_PolicyProp.objectReferenceValue as ProjectionPolicy;
            using (var changeCheck = new EditorGUI.ChangeCheckScope())
            {
                var policyIndex = currentPolicy != null ? k_ProjectionPolicyTypes.IndexOf(currentPolicy.GetType()) : -1;

                policyIndex = EditorGUILayout.Popup(Labels.GetGUIContent(Labels.Field.ProjectionPolicy),
                    policyIndex,
                    k_ProjectionPolicyGUIOptions);

                if (changeCheck.changed)
                {
                    if (currentPolicy != null)
                    {
                        Undo.DestroyObjectImmediate(currentPolicy);
                    }

                    currentPolicy =
                        (ProjectionPolicy)Undo.AddComponent(m_ClusterRenderer.gameObject,
                            k_ProjectionPolicyTypes[policyIndex]);

                    m_PolicyProp.objectReferenceValue = currentPolicy;
                }
            }

            // Check that we're rendering the correct inspector for the active policy. The policy
            // may have changed outside of OnInspectorGUI (e.g. undo system).
            if (m_PolicyEditor == null || m_PolicyEditor.target != currentPolicy)
            {
                DestroyImmediate(m_PolicyEditor);
                if (currentPolicy is not null)
                {
                    currentPolicy.hideFlags = HideFlags.HideInInspector;
                    m_PolicyEditor = CreateEditor(currentPolicy) as NestedInspector;
                }
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
            if (m_PolicyEditor != null && m_PolicyEditor.target != null)
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
