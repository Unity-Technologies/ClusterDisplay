using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    class ClusterRenderingSettingsProvider : SettingsProvider
    {
        static readonly string k_SettingsPath = "Project/ClusterDisplaySettings/ClusterRendering";
        const string k_StyleSheetCommon = "Packages/com.unity.cluster-display/Editor/UI/SettingsWindowCommon.uss";

        VisualElement m_RootElement;
        VisualElement m_CreateRenderer;
        VisualElement m_InspectorParent;
        IMGUIContainer m_EmbeddedInspector;
        Toggle m_EnableRendererToggle;

        ClusterRenderer m_ActiveClusterRenderer;
        UnityEditor.Editor m_RendererInspector;
        VisualElement m_SettingsElement;

        class Contents
        {
            public const string SettingsName = "Cluster Rendering Settings";
            public const string NoRenderer = "There is no Cluster Renderer in the scene.";
            public const string CreateRenderer = "Set up Cluster Renderer";
            public const string SelectRenderer = "Select active Cluster Renderer component";
        }

        [SettingsProvider]
        static SettingsProvider CreateSettingsProvider() =>
            new ClusterRenderingSettingsProvider(k_SettingsPath, SettingsScope.Project,
                GetSearchKeywordsFromGUIContentProperties<Contents>()) { label = "Cluster Rendering" };

        ClusterRenderingSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
            m_RootElement = new ScrollView();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StyleSheetCommon);

            m_RootElement.styleSheets.Add(styleSheet);
            m_RootElement.AddToClassList("cluster-settings-container");

            var title = new Label { text = Contents.SettingsName };
            title.AddToClassList("cluster-settings-header");
            title.AddToClassList("unity-label");

            m_RootElement.Add(title);

            // Elements to show if there's no existing ClusterRenderer
            m_CreateRenderer = new VisualElement();
            m_RootElement.Add(m_CreateRenderer);
            m_CreateRenderer.Add(new HelpBox(Contents.NoRenderer, HelpBoxMessageType.Info));
            var createRendererButton = new Button { text = Contents.CreateRenderer };
            createRendererButton.clicked += ClusterDisplayGraphicsSetup.SetupComponents;
            m_CreateRenderer.Add(createRendererButton);

            // Settings to show if we have an active ClusterRenderer
            m_SettingsElement = new VisualElement();
            m_EnableRendererToggle = new Toggle { label = "Enable Cluster Rendering" };
            m_EnableRendererToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                if (m_ActiveClusterRenderer != null)
                {
                    m_ActiveClusterRenderer.enabled = evt.newValue;
                    m_ActiveClusterRenderer.gameObject.SetActive(evt.newValue);
                }
            });
            m_SettingsElement.Add(m_EnableRendererToggle);

            // Option to enable headless emitter
            // FIXME: Some settings from ClusterParams are only relevant for rendering
            var clusterSettings = new SerializedObject(ClusterDisplaySettings.Current);
            var headlessToggle = new PropertyField(clusterSettings.FindProperty("m_ClusterParams.HeadlessEmitter"));
            var replaceHeadlessToggle = new PropertyField(clusterSettings.FindProperty("m_ClusterParams.ReplaceHeadlessEmitter"));
            m_SettingsElement.Add(headlessToggle);
            m_SettingsElement.Add(replaceHeadlessToggle);
            m_SettingsElement.Bind(clusterSettings);
            headlessToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
                replaceHeadlessToggle.SetHidden(!evt.newValue));
            replaceHeadlessToggle.AddToClassList("cluster-settings-indented");

            // Button that will select the active ClusterRenderer
            var selectButton = new Button { text = Contents.SelectRenderer };
            selectButton.clicked += () =>
            {
                if (m_ActiveClusterRenderer != null)
                {
                    Selection.activeGameObject = m_ActiveClusterRenderer.gameObject;
                }
            };

            m_SettingsElement.Add(selectButton);

            m_InspectorParent = new Foldout { text = "Active Renderer"};
            m_SettingsElement.Add(m_InspectorParent);
            m_RootElement.Add(m_SettingsElement);

            SceneManager.sceneLoaded += OnSceneLoaded;
            ClusterRenderer.Enabled += ClusterRendererChanged;
            ClusterRenderer.Disabled += ClusterRendererChanged;
        }

        void ClusterRendererChanged()
        {
            m_RootElement.schedule.Execute(RefreshRendererOptions);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshRendererOptions();
        }

        void RefreshRendererOptions()
        {
            var clusterRendererExists = ClusterRenderer.TryGetInstance(out m_ActiveClusterRenderer);
            m_CreateRenderer.SetHidden(clusterRendererExists);
            m_SettingsElement.SetHidden(!clusterRendererExists);

            if (clusterRendererExists)
            {
                Debug.Assert(m_ActiveClusterRenderer != null);
                UnityEditor.Editor rendererInspector = m_RendererInspector;
                m_EnableRendererToggle.SetValueWithoutNotify(m_ActiveClusterRenderer.isActiveAndEnabled);
                UnityEditor.Editor.CreateCachedEditor(m_ActiveClusterRenderer, null, ref rendererInspector);
                if (rendererInspector != m_RendererInspector)
                {
                    if (m_EmbeddedInspector != null)
                    {
                        m_InspectorParent.Remove(m_EmbeddedInspector);
                    }

                    if (m_RendererInspector != null)
                    {
                        Object.DestroyImmediate(m_RendererInspector);
                    }

                    if (rendererInspector != null)
                    {
                        m_EmbeddedInspector = new IMGUIContainer(onGUIHandler: () =>
                        {
                            rendererInspector.OnInspectorGUI();
                        });
                        m_InspectorParent.Add(m_EmbeddedInspector);
                        m_RendererInspector = rendererInspector;
                    }
                }
            }
        }

        public override void OnActivate(string searchContext, VisualElement parentElement)
        {
            base.OnActivate(searchContext, parentElement);

            parentElement.Add(m_RootElement);
            RefreshRendererOptions();
        }

        public override void OnDeactivate()
        {
            if (m_RendererInspector != null)
            {
                Object.DestroyImmediate(m_RendererInspector);
            }
        }
    }
}
