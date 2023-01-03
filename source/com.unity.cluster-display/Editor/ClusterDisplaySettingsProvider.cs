using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.ClusterDisplay.Editor
{
    static class ClusterDisplaySettingsProvider
    {
        static readonly string k_SettingsPath = "Project/ClusterDisplaySettings";
        const string k_StyleSheetCommon = "Packages/com.unity.cluster-display/Editor/UI/SettingsWindowCommon.uss";

        class Contents
        {
            public const string SettingsName = "Cluster Display Settings";
            public const string InitializeOnPlay = "Enable On Play";
        }

        [MenuItem("Cluster Display/Settings...")]
        static void ShowClusterDisplaySettings()
        {
            SettingsService.OpenProjectSettings(k_SettingsPath);
        }

        [SettingsProvider]
        static SettingsProvider CreateSettingsProvider() =>
            new (k_SettingsPath, SettingsScope.Project)
            {
                label = "Cluster Display",
                activateHandler = (searchContext, parentElement) =>
                {
                    var settings = new SerializedObject(ClusterDisplaySettings.CurrentSettings);
                    var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StyleSheetCommon);

                    var rootElement = (VisualElement)new ScrollView();
                    rootElement.styleSheets.Add(styleSheet);
                    rootElement.AddToClassList("cluster-settings-container");

                    var title = new Label { text = Contents.SettingsName };
                    title.AddToClassList("cluster-settings-header");
                    title.AddToClassList("unity-label");

                    rootElement.Add(title);

                    var properties = new VisualElement();
                    rootElement.Add(properties);
                    properties.Add(new PropertyField(settings.FindProperty("m_EnableOnPlay"), Contents.InitializeOnPlay));

                    var parameters = new VisualElement();
                    rootElement.Add(parameters);
                    parameters.Add(new PropertyField(settings.FindProperty("m_ClusterParams")));

                    var missionControl = new VisualElement();
                    rootElement.Add(missionControl);
                    missionControl.Add(new PropertyField(settings.FindProperty("m_MissionControlSettings")));

                    rootElement.Bind(settings);
                    parentElement.Add(rootElement);
                },
                keywords = SettingsProvider.GetSearchKeywordsFromGUIContentProperties<Contents>()
            };
    }
}
