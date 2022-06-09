using System;
using UnityEngine;
using UnityEditor;
using Unity.ClusterDisplay.Scripting;

#if CLUSTER_DISPLAY_HAS_MISSION_CONTROL
using Unity.ClusterDisplay.MissionControl.Editor;
#endif

namespace Unity.ClusterDisplay.Editor
{
    [CustomEditor(typeof(EditorLinkConfig))]
    public class EditorLinkConfigEditor : UnityEditor.Editor
    {
        static class Contents
        {

            public static readonly GUIContent LinkReceiverAddress =
                EditorGUIUtility.TrTextContent("Link Receiver", "Address of node to receive editor updates (typically the emitter)");

            public static readonly GUIContent ShowMissionControl =
                EditorGUIUtility.TrTextContent("Open Mission Control", "Open the Mission Control Window to view available nodes");

            public static readonly GUIContent SelectActiveEmitter =
                EditorGUIUtility.TrTextContent("Select Active Emitter", "Get the address of the first active emitter node");
        }

        SerializedProperty m_AddressProp;
        SerializedProperty m_PortProp;

#if CLUSTER_DISPLAY_HAS_MISSION_CONTROL
        MissionControlWindow m_MissionControlWindow;
#endif

        void OnEnable()
        {
            m_AddressProp = serializedObject.FindProperty("m_Address");
            m_PortProp = serializedObject.FindProperty("m_Port");
        }

        public override void OnInspectorGUI()
        {
#if CLUSTER_DISPLAY_HAS_MISSION_CONTROL
            if (GUILayout.Button(Contents.ShowMissionControl))
            {
                m_MissionControlWindow = MissionControlWindow.ShowWindow();
            }
#endif
            serializedObject.Update();
            using var changeCheck = new EditorGUI.ChangeCheckScope();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(m_AddressProp, Contents.LinkReceiverAddress);
#if CLUSTER_DISPLAY_HAS_MISSION_CONTROL
                if (m_MissionControlWindow != null && GUILayout.Button(Contents.SelectActiveEmitter))
                {
                    m_AddressProp.stringValue = m_MissionControlWindow.FindFirstActiveEmitter();
                }
#endif
            }
            EditorGUILayout.PropertyField(m_PortProp);

            if (changeCheck.changed)
                serializedObject.ApplyModifiedProperties();
        }

        public static EditorLinkConfig CreateLinkConfig()
        {
            var linkConfig = CreateInstance<EditorLinkConfig>();
            AssetDatabase.CreateAsset(linkConfig,
                AssetDatabase.GenerateUniqueAssetPath($"{EditorUtils.GetAssetsFolder()}/EditorLink.asset"));

            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = linkConfig;

            return linkConfig;
        }
    }

}
