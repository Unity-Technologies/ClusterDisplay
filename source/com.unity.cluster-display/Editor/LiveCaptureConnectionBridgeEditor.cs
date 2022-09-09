#if LIVE_CAPTURE_2_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.LiveCapture;
using Unity.ClusterDisplay.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Editor
{
    [CustomEditor(typeof(LiveCaptureConnectionBridge))]
    public class LiveCaptureConnectionInspector : UnityEditor.Editor
    {
        static class Contents
        {
            public static readonly GUIContent ConnectionTypeLabel = EditorGUIUtility.TrTextContent("Connection Type");
        }

        static readonly TypeCache.TypeCollection k_ConnectionTypes;
        static readonly GUIContent[] k_ConnectionTypeOptions;

        static LiveCaptureConnectionInspector()
        {
            k_ConnectionTypes = TypeCache.GetTypesDerivedFrom<Connection>();
            k_ConnectionTypeOptions = k_ConnectionTypes
                .Select(t => new GUIContent(t.Name))
                .ToArray();
        }

        SerializedProperty m_ConnectionProp;
        LiveCaptureConnectionBridge m_ConnectionBridge;

        void OnEnable()
        {
            m_ConnectionBridge = target as LiveCaptureConnectionBridge;
            m_ConnectionProp = serializedObject.FindProperty("m_ConnectionTypeName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using var changeCheckScope = new EditorGUI.ChangeCheckScope();
            var index = m_ConnectionBridge.m_ConnectionType == null
                ? -1
                : k_ConnectionTypes.IndexOf(m_ConnectionBridge.m_ConnectionType);
            index = EditorGUILayout.Popup(Contents.ConnectionTypeLabel, index, k_ConnectionTypeOptions);
            if (changeCheckScope.changed)
            {
                m_ConnectionProp.stringValue = k_ConnectionTypes[index].AssemblyQualifiedName;
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
#endif
