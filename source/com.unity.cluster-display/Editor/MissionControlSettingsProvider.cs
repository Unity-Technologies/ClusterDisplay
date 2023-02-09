using System;
using Unity.ClusterDisplay.MissionControl;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.ClusterDisplay.Editor
{
    static class MissionControlSettingsProvider
    {
        static readonly string k_SettingsPath = "Project/ClusterDisplaySettings/MissionControlSettings";
        const string k_StyleSheetCommon = "Packages/com.unity.cluster-display/Editor/UI/SettingsWindowCommon.uss";

        class Contents
        {
            public const string SettingsName = "Mission Control Settings";
        }

        [SettingsProvider]
        static SettingsProvider CreateSettingsProvider() =>
            new (k_SettingsPath, SettingsScope.Project)
            {
                label = "Mission Control",
                activateHandler = (searchContext, parentElement) =>
                {
                    var settings = new SerializedObject(MissionControlSettings.Current);
                    var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StyleSheetCommon);

                    var rootElement = (VisualElement)new ScrollView();
                    rootElement.styleSheets.Add(styleSheet);
                    rootElement.AddToClassList("cluster-settings-container");

                    var title = new Label { text = Contents.SettingsName };
                    title.AddToClassList("cluster-settings-header");
                    title.AddToClassList("unity-label");

                    rootElement.Add(title);

                    rootElement.Add(new InspectorElement(settings));

                    rootElement.Bind(settings);
                    parentElement.Add(rootElement);
                },
                keywords = SettingsProvider.GetSearchKeywordsFromGUIContentProperties<Contents>()
            };
    }
}
