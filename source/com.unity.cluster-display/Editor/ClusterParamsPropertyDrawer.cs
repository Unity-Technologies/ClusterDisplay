using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.ClusterDisplay.Editor
{
    [CustomPropertyDrawer(typeof(ClusterParams))]
    public class ClusterParamsPropertyDrawer : PropertyDrawer
    {
        class Contents
        {
            public const string HandshakeTimeoutLabel = "Handshake Timeout (s)";
            public const string CommTimeoutLabel = "Communication Timeout (s)";
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.Add(new PropertyField(property.FindPropertyRelative("MulticastAddress")));
            container.Add(new PropertyField(property.FindPropertyRelative("Port")));

            container.Add(new PropertyField(property.FindPropertyRelative("m_HandshakeTimeoutSec"), Contents.HandshakeTimeoutLabel));
            container.Add(new PropertyField(property.FindPropertyRelative("m_CommTimeoutSec"), Contents.CommTimeoutLabel));

            container.Add(new PropertyField(property.FindPropertyRelative("TargetFps")));
            container.Add(new PropertyField(property.FindPropertyRelative("Fence")));
            container.Add(new PropertyField(property.FindPropertyRelative("AdapterName")));

            var headlessField = new PropertyField(property.FindPropertyRelative("HeadlessEmitter"));
            var replaceHeadlessField = new PropertyField(property.FindPropertyRelative("ReplaceHeadlessEmitter"));
            container.Add(new PropertyField(property.FindPropertyRelative("DelayRepeaters")));

            replaceHeadlessField.AddToClassList("cluster-settings-indented");

            headlessField.RegisterValueChangeCallback(evt =>
            {
                if (evt.changedProperty.boolValue)
                {
                    replaceHeadlessField.RemoveFromClassList("hidden");
                }
                else
                {
                    replaceHeadlessField.AddToClassList("hidden");
                }
            });

            container.Add(headlessField);
            container.Add(replaceHeadlessField);

            var editorDebugFoldout = new Foldout
            {
                text = "Play in Editor Settings"
            };
            container.Add(editorDebugFoldout);

            editorDebugFoldout.Add(new PropertyField(property.FindPropertyRelative("NodeID")));
            editorDebugFoldout.Add(new PropertyField(property.FindPropertyRelative("RepeaterCount")));

            var emitterRepeaterContainer = new VisualElement();
            emitterRepeaterContainer.Bind(property.serializedObject);

            emitterRepeaterContainer.Add(new Label("Play as"));
            emitterRepeaterContainer.AddToClassList("toggle-group");
            var emitterButton = new Button { text = "Emitter" };
            var repeaterButton = new Button { text = "Repeater" };
            emitterRepeaterContainer.Add(emitterButton);
            emitterRepeaterContainer.Add(repeaterButton);

            var isEmitterProperty = property.FindPropertyRelative("EmitterSpecified");
            var emitterSpecifiedWatcher = new PropertyField(isEmitterProperty);
            emitterSpecifiedWatcher.AddToClassList("hidden");
            editorDebugFoldout.Add(emitterSpecifiedWatcher);

            emitterSpecifiedWatcher.RegisterValueChangeCallback(evt =>
            {
                if (evt.changedProperty.boolValue)
                {
                    emitterButton.AddToClassList("checked");
                    repeaterButton.RemoveFromClassList("checked");
                }
                else
                {
                    emitterButton.RemoveFromClassList("checked");
                    repeaterButton.AddToClassList("checked");
                }
            });

            emitterButton.clicked += () =>
            {
                isEmitterProperty.boolValue = true;
                property.serializedObject.ApplyModifiedProperties();
            };

            repeaterButton.clicked += () =>
            {
                isEmitterProperty.boolValue = false;
                property.serializedObject.ApplyModifiedProperties();
            };

            editorDebugFoldout.Add(emitterRepeaterContainer);

            return container;
        }
    }
}
