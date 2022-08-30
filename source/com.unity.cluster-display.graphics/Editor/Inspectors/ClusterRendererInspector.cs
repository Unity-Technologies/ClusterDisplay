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
        const string k_UseInspector = "New projection policy assigned to the Cluster Renderer component. Use the " +
                                      "Cluster Rendering inspector to modify it.";

        const string k_MultipleDisallowed = "Multiple Projection Policies are not permitted.";

        SerializedProperty m_PolicyProp;
        SerializedProperty m_OverscanProp;
        SerializedProperty m_DelayPresentByOneFrameProp;

        UnityEditor.Editor m_PolicyEditor;

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

        /// <summary>
        /// Helper function to let us change the projection policy on the undo stack.
        /// </summary>
        /// <param name="clusterRenderer"></param>
        /// <param name="policyType"></param>
        internal static void SetProjectionPolicy(ClusterRenderer clusterRenderer, Type policyType)
        {
            if (clusterRenderer.ProjectionPolicy != null)
            {
                Undo.DestroyObjectImmediate(clusterRenderer.ProjectionPolicy);
            }

            clusterRenderer.ProjectionPolicy =
                (ProjectionPolicy)Undo.AddComponent(clusterRenderer.gameObject, policyType);

            clusterRenderer.ProjectionPolicy.hideFlags = HideFlags.HideInInspector;
        }

        public override void OnInspectorGUI()
        {
#if CLUSTER_DISPLAY_URP
            RenderFeatureEditorUtils<ClusterRenderer, InjectionPointRenderFeature>.OnInspectorGUI();
#endif
            DisallowMultiplePolicies();
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
                    SetProjectionPolicy(m_ClusterRenderer, k_ProjectionPolicyTypes[policyIndex]);
                    serializedObject.Update();
                }
            }

            // Check that we're rendering the correct inspector for the active policy. The policy
            // may have changed outside of OnInspectorGUI (e.g. undo system).
            if (m_PolicyEditor == null || m_PolicyEditor.target != currentPolicy)
            {
                DestroyImmediate(m_PolicyEditor);
                if (currentPolicy is not null)
                {
                    m_PolicyEditor = CreateEditor(currentPolicy);
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

        void DisallowMultiplePolicies()
        {
            foreach (var projectionPolicy in m_ClusterRenderer.GetComponents<ProjectionPolicy>())
            {
                if (projectionPolicy == m_ClusterRenderer.ProjectionPolicy)
                {
                    projectionPolicy.hideFlags = HideFlags.HideInInspector;
                    continue;
                }

                if (m_ClusterRenderer.ProjectionPolicy == null)
                {
                    m_ClusterRenderer.ProjectionPolicy = projectionPolicy;
                    Debug.LogWarning(k_UseInspector);
                    continue;
                }

                DestroyImmediate(projectionPolicy);
                Debug.LogError(k_MultipleDisallowed);
            }
        }

        void OnSceneGUI()
        {
            if (m_PolicyEditor != null && m_PolicyEditor.target != null &&
                m_PolicyEditor is NestedInspector policyEditor)
            {
                policyEditor.OnSceneGUI();
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
