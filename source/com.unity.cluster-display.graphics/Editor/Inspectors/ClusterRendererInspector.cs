using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [CustomEditor(typeof(ClusterRenderer))]
    class ClusterRendererInspector : UnityEditor.Editor
    {
        static string k_NoCamerasMessage = L10n.Tr("No cameras are marked to render in this cluster.");
        static string k_AddCameraScriptText = L10n.Tr("Add ClusterCamera component to all cameras");
        static string k_NoPolicyMessage = L10n.Tr("No projection policy assigned.");
        static string k_UseInspector = L10n.Tr("New projection policy assigned to the Cluster Renderer component. Use the " +
                                      "Cluster Rendering inspector to modify it.");
        static class Contents
        {
            public static readonly GUIContent DrawTestPattern = EditorGUIUtility.TrTextContent("Draw Test Pattern",
            "Draw test pattern onto surfaces");
        }

        const string k_MultipleDisallowed = "Multiple Projection Policies are not permitted.";

        SerializedProperty m_PolicyProp;
        SerializedProperty m_OverscanProp;
        SerializedProperty m_TestPatternProp;
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
            m_TestPatternProp = serializedObject.FindProperty("m_Settings.m_RenderTestPattern");
            m_DelayPresentByOneFrameProp = serializedObject.FindProperty("m_DelayPresentByOneFrame");
        }

        /// <summary>
        /// Helper function to let us change the projection policy on the undo stack.
        /// </summary>
        /// <param name="clusterRenderer"></param>
        /// <param name="policyType"></param>
        static void SetProjectionPolicy(ClusterRenderer clusterRenderer, Type policyType)
        {
            if (clusterRenderer.ProjectionPolicy != null)
            {
                Undo.DestroyObjectImmediate(clusterRenderer.ProjectionPolicy);
            }

            clusterRenderer.ProjectionPolicy =
                (ProjectionPolicy)Undo.AddComponent(clusterRenderer.gameObject, policyType);

            clusterRenderer.ProjectionPolicy.hideFlags = HideFlags.HideInInspector;
        }

        internal static void SetProjectionPolicy<T>(ClusterRenderer clusterRenderer) where T : ProjectionPolicy =>
            SetProjectionPolicy(clusterRenderer, typeof(T));

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
            if (currentPolicy != null && currentPolicy.SupportsTestPattern)
            {
                EditorGUILayout.PropertyField(m_TestPatternProp, Contents.DrawTestPattern);
            }

            EditorGUILayout.PropertyField(m_DelayPresentByOneFrameProp, Labels.GetGUIContent(Labels.Field.DelayPresentByOneFrame));

            if (currentPolicy != null)
            {
                Debug.Assert(m_PolicyEditor != null);
                m_PolicyEditor.OnInspectorGUI();
            }
            else
            {
                EditorGUILayout.HelpBox(k_NoPolicyMessage, UnityEditor.MessageType.Warning);
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
                EditorGUILayout.HelpBox(k_NoCamerasMessage, UnityEditor.MessageType.Warning);
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
