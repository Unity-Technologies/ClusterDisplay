using System;
using System.Collections;
using System.Collections.Generic;
using Unity.LiveCapture.CompanionApp;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VirtualCameraPoseReader))]
public class VirtualCameraPoseReaderEditor : Editor
{
    static class Contents
    {
        public static readonly GUIContent ActorLabel = EditorGUIUtility.TrTextContent("Actor", "The actor currently assigned to this device.");
    }

    SerializedProperty m_Actor;

    void OnEnable()
    {
        m_Actor = serializedObject.FindProperty("m_Actor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(m_Actor, Contents.ActorLabel);
        serializedObject.ApplyModifiedProperties();
    }
}
