using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.ClusterDisplay.Editor
{
    [CustomPropertyDrawer(typeof(ClusterParams))]
    public class ClusterParamsPropertyDrawer : PropertyDrawer
    {
        const string k_StyleSheetCommon = "Packages/com.unity.cluster-display/Editor/UI/SettingsWindowCommon.uss";
        static readonly StyleSheet k_StyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StyleSheetCommon);

        static class Contents
        {
            public const string HandshakeTimeoutLabel = "Handshake Timeout (s)";
            public const string CommTimeoutLabel = "Communication Timeout (s)";
            public const string InputWarning = "Input synchronization requires the Delay Repeaters option.";
            public const string InputSync = "Input synchronization";
        }


        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.styleSheets.Add(k_StyleSheet);
            container.Add(new PropertyField(property.FindPropertyRelative("MulticastAddress")));
            container.Add(new PropertyField(property.FindPropertyRelative("Port")));

            container.Add(new PropertyField(property.FindPropertyRelative("m_HandshakeTimeoutSec"), Contents.HandshakeTimeoutLabel));
            container.Add(new PropertyField(property.FindPropertyRelative("m_CommTimeoutSec"), Contents.CommTimeoutLabel));

            container.Add(new PropertyField(property.FindPropertyRelative("TargetFps")));
            container.Add(new PropertyField(property.FindPropertyRelative("Fence")));
            container.Add(new PropertyField(property.FindPropertyRelative("AdapterName")));

            var headlessProperty = property.FindPropertyRelative("HeadlessEmitter");
            container.Add(new PropertyField(headlessProperty));

            var replaceHeadlessField = new PropertyField(property.FindPropertyRelative("ReplaceHeadlessEmitter"));
            replaceHeadlessField.AddToClassList("cluster-settings-indented");

            container.Add(replaceHeadlessField);

            var delayRepeatersProperty = property.FindPropertyRelative("DelayRepeaters");
            container.Add(new PropertyField(delayRepeatersProperty));

            var inputSyncProperty = property.FindPropertyRelative("InputSync");
            container.Add(new PropertyField(inputSyncProperty, Contents.InputSync));
            var inputWarning = new HelpBox(Contents.InputWarning, HelpBoxMessageType.Warning);
            container.Add(inputWarning);

            var editorDebugFoldout = new Foldout { text = "Play in Editor Settings" };
            container.Add(editorDebugFoldout);

            editorDebugFoldout.Add(new PropertyField(property.FindPropertyRelative("NodeID")));
            editorDebugFoldout.Add(new PropertyField(property.FindPropertyRelative("RepeaterCount")));

            var isEmitterProperty = property.FindPropertyRelative("EmitterSpecified");

            // Hidden control to create a binding to EmitterSpecified, but we're not going to display it (we don't
            // want to use a checkbox).
            var emitterSpecifiedWatcher = new PropertyField(isEmitterProperty);
            emitterSpecifiedWatcher.SetHidden(true);
            editorDebugFoldout.Add(emitterSpecifiedWatcher);

            // Use buttons to control the Emitter/Repeater setting
            var emitterRepeaterContainer = new VisualElement();

            emitterRepeaterContainer.Add(new Label("Play as"));
            emitterRepeaterContainer.AddToClassList("toggle-group");
            var emitterButton = new Button { text = "Emitter" };
            var repeaterButton = new Button { text = "Repeater" };
            emitterRepeaterContainer.Add(emitterButton);
            emitterRepeaterContainer.Add(repeaterButton);
            editorDebugFoldout.Add(emitterRepeaterContainer);

            emitterButton.RegisterCallback((ClickEvent _) =>
            {
                isEmitterProperty.boolValue = true;
                property.serializedObject.ApplyModifiedProperties();
            });

            repeaterButton.RegisterCallback((ClickEvent _) =>
            {
                isEmitterProperty.boolValue = false;
                property.serializedObject.ApplyModifiedProperties();
            });

            void OnSettingsChanged()
            {
                var hideWarning = inputSyncProperty.intValue == (int)InputSync.None || delayRepeatersProperty.boolValue;
                inputWarning.SetHidden(hideWarning);
                replaceHeadlessField.SetHidden(!headlessProperty.boolValue);

                if (isEmitterProperty.boolValue)
                {
                    emitterButton.AddToClassList("checked");
                    repeaterButton.RemoveFromClassList("checked");
                }
                else
                {
                    emitterButton.RemoveFromClassList("checked");
                    repeaterButton.AddToClassList("checked");
                }
            }

            container.RegisterCallback<SerializedPropertyChangeEvent>(_ => OnSettingsChanged());

            return container;
        }
    }

    static class UIHelpers
    {
        public static void SetHidden(this VisualElement element, bool hidden)
        {
            if (hidden)
            {
                element.AddToClassList("hidden");
            }
            else
            {
                element.RemoveFromClassList("hidden");
            }
        }
    }
}
