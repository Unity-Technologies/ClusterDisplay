using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [CustomEditor(typeof(ClusterRenderer))]
    [InitializeOnLoad]
    class ClusterRendererInspector : UnityEditor.Editor
    {
        const string k_NoCamerasMessage = "No cameras are marked to render in this cluster.";
        const string k_AddCameraScriptText = "Add ClusterCamera component to all cameras";
        const string k_ConfirmPolicyChange = "You are changing the projection policy. Your current projection settings will by lost. Continue?";
        static Type[] s_ProjectionPolicies;
        static GUIContent[] s_PolicyOptions;

        SerializedProperty m_PolicyProp;
        SerializedProperty m_OverscanProp;

        NestedInspector m_PolicyEditor;

        static ClusterRendererInspector()
        {
            s_ProjectionPolicies = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(ProjectionPolicy)))
                .ToArray();

            s_PolicyOptions = s_ProjectionPolicies
                .Select(GetPopupItemName)
                .Select(str => new GUIContent(str))
                .ToArray();
        }

        void OnEnable()
        {
            m_PolicyProp = serializedObject.FindProperty("m_ProjectionPolicy");
            m_OverscanProp = serializedObject.FindProperty("m_Settings.m_OverscanInPixels");
        }

        static string GetPopupItemName(Type type)
        {
            return type.GetCustomAttributes(typeof(PopupItemAttribute), true).FirstOrDefault()
                is PopupItemAttribute item
                ? item.ItemName
                : type.ToString();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (target as ClusterRenderer is not { } clusterRenderer) return;

            using (var check = new EditorGUI.ChangeCheckScope())
            {
#if CLUSTER_DISPLAY_URP
                RenderFeatureEditorUtils<ClusterRenderer, InjectionPointRenderFeature>.OnInspectorGUI();
#endif
                CheckForClusterCameraComponents();
                SelectPolicyDropdown();

                EditorGUILayout.PropertyField(m_OverscanProp, Labels.GetGUIContent(Labels.Field.Overscan));

                if (check.changed)
                {
                    serializedObject.ApplyModifiedProperties();

                    // TODO needed?
                    EditorUtility.SetDirty(clusterRenderer);
                }
            }

            if (m_PolicyProp.objectReferenceValue != null)
            {
                if (m_PolicyEditor == null)
                {
                    m_PolicyEditor = CreateEditor(m_PolicyProp.objectReferenceValue) as NestedInspector;
                }

                if (m_PolicyEditor != null)
                {
                    m_PolicyEditor.OnInspectorGUI();
                }
            }
        }

        void SelectPolicyDropdown()
        {
            var policyRef = m_PolicyProp.objectReferenceValue;
            var selectedIndex = -1;
            if (policyRef != null)
            {
                var selectedPolicy = policyRef.GetType();
                selectedIndex = Array.IndexOf(s_ProjectionPolicies, policyRef.GetType());
            }

            var newIndex = EditorGUILayout.Popup(
                Labels.GetGUIContent(Labels.Field.ProjectionPolicy),
                selectedIndex,
                s_PolicyOptions);

            if (newIndex != selectedIndex)
            {
                if (selectedIndex >= 0)
                {
                    if (!EditorUtility.DisplayDialog("Cluster Rendering", k_ConfirmPolicyChange, "Change projection policy", "Cancel"))
                    {
                        return;
                    }
                }

                SetProjectionPolicy(s_ProjectionPolicies[newIndex]);
            }
        }

        void SetProjectionPolicy(Type type)
        {
            if (target as ClusterRenderer is not { } renderer) return;

            m_PolicyEditor = null;
            Undo.RegisterCompleteObjectUndo(target, "Set Projection Policy");
            var setterMethod = typeof(ClusterRenderer).GetMethod(nameof(ClusterRenderer.SetNewProjectionPolicy));
            var genericSetter = setterMethod?.MakeGenericMethod(type);
            genericSetter?.Invoke(renderer, null);
        }

        void OnSceneGUI()
        {
            if (m_PolicyEditor != null)
            {
                m_PolicyEditor.OnSceneGUI();
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
