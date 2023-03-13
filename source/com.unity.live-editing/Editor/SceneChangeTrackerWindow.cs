#if UNITY_LIVE_EDITING_DEBUG
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Unity.LiveEditing.Editor
{
    class SceneChangeTrackerWindow : EditorWindow
    {
        static class Contents
        {
            public const string WindowName = "Scene Change Tracker";
            public const string WindowPath = "Window/Live Editing/" + WindowName;
            public static readonly GUIContent WindowTitle = new GUIContent(WindowName);
        }

        [SerializeField]
        TreeViewState m_TreeViewState;

        SceneChangeTrackerTreeView m_TreeView;
        bool m_TreeDirty;

        [MenuItem(Contents.WindowPath)]
        public static void ShowWindow()
        {
            GetWindow<SceneChangeTrackerWindow>();
        }

        void OnEnable()
        {
            titleContent = Contents.WindowTitle;

            CreateTreeView();
        }

        void Update()
        {
            m_TreeView.Reload();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField($"Tracked Game Object Count: {SceneChangeTracker.Instance.m_TrackedGameObjects.Count}");
            EditorGUILayout.LabelField($"Tracked Component Count: {SceneChangeTracker.Instance.m_TrackedComponents.Count}");

            var rect = EditorGUILayout.GetControlRect();

            EditorGUIUtility.hierarchyMode = true;
            EditorGUIUtility.wideMode = true;

            m_TreeView.OnGUI(new Rect(0, rect.y, position.width, position.height - rect.y));
        }

        void CreateTreeView()
        {
            m_TreeViewState ??= new TreeViewState();
            m_TreeView = new SceneChangeTrackerTreeView(m_TreeViewState, SceneChangeTracker.Instance);
        }

        class SceneItem : TreeViewItem
        {
            public readonly SceneChangeTracker.SceneState Scene;

            public SceneItem(SceneChangeTracker.SceneState scene)
            {
                Scene = scene;
            }
        }

        class GameObjectItem : TreeViewItem
        {
            public readonly SceneChangeTracker.GameObjectState GameObject;

            public GameObjectItem(SceneChangeTracker.GameObjectState gameObject)
            {
                GameObject = gameObject;
            }
        }

        class ComponentItem : TreeViewItem
        {
            public readonly SceneChangeTracker.ComponentState Component;

            public ComponentItem(SceneChangeTracker.ComponentState component)
            {
                Component = component;
            }
        }

        class PropertyItem : TreeViewItem
        {
            public readonly SerializedObject SerializedObject;
            public readonly string Path;

            public PropertyItem(SerializedObject serializedObject, string path)
            {
                SerializedObject = serializedObject;
                Path = path;
            }
        }

        class SceneChangeTrackerTreeView : TreeView
        {
            SceneChangeTracker m_SceneChangeTracker;

            public SceneChangeTrackerTreeView(TreeViewState treeViewState, SceneChangeTracker sceneChangeTracker) : base(treeViewState)
            {
                m_SceneChangeTracker = sceneChangeTracker;

                useScrollView = true;
                rowHeight = EditorGUIUtility.singleLineHeight;

                Reload();
            }

            protected override TreeViewItem BuildRoot()
            {
                var id = 0;

                var scenesRootItem = new TreeViewItem
                {
                    id = id++,
                    depth = -1,
                    displayName = "Root",
                };

                foreach (var scene in m_SceneChangeTracker.m_TrackedScenes)
                {
                    var sceneItem = new SceneItem(scene.Value)
                    {
                        id = id++,
                        depth = 0,
                    };

                    if (scene.Value != null)
                    {
                        var sceneRootsItem = new TreeViewItem
                        {
                            id = id++,
                            depth = sceneItem.depth + 1,
                            displayName = "Roots"
                        };

                        foreach (var root in scene.Value.Current.Roots)
                        {
                            sceneRootsItem.AddChild(AddGameObject(root, sceneRootsItem.depth + 1, ref id));
                        }

                        sceneItem.AddChild(sceneRootsItem);
                    }

                    scenesRootItem.AddChild(sceneItem);
                }

                if (scenesRootItem.children == null)
                {
                    scenesRootItem.children = new List<TreeViewItem>();
                }

                return scenesRootItem;
            }

            GameObjectItem AddGameObject(SceneChangeTracker.GameObjectState goState, int depth, ref int id)
            {
                var gameObjectItem = new GameObjectItem(goState)
                {
                    id = id++,
                    depth = depth,
                };

                if (goState != null)
                {
                    var propertiesItem = new TreeViewItem
                    {
                        id = id++,
                        depth = gameObjectItem.depth + 1,
                        displayName = "Properties"
                    };

                    AddProperties(goState.Current.Properties, propertiesItem, ref id);

                    gameObjectItem.AddChild(propertiesItem);

                    var componentsItem = new TreeViewItem
                    {
                        id = id++,
                        depth = gameObjectItem.depth + 1,
                        displayName = "Components"
                    };

                    foreach (var component in goState.Current.Components)
                    {
                        var componentItem = new ComponentItem(component)
                        {
                            id = id++,
                            depth = componentsItem.depth + 1,
                        };

                        if (component != null)
                        {
                            AddProperties(component.Current.Properties, componentItem, ref id);
                        }

                        componentsItem.AddChild(componentItem);
                    }

                    gameObjectItem.AddChild(componentsItem);

                    if (goState.Current.Children.Count > 0)
                    {
                        var childrenItem = new TreeViewItem
                        {
                            id = id++,
                            depth = gameObjectItem.depth + 1,
                            displayName = "Children"
                        };

                        foreach (var child in goState.Current.Children)
                        {
                            childrenItem.AddChild(AddGameObject(child, childrenItem.depth + 1, ref id));
                        }

                        gameObjectItem.AddChild(childrenItem);
                    }
                }

                return gameObjectItem;
            }

            void AddProperties(SerializedObject serializedObject, TreeViewItem parentItem, ref int id)
            {
                var currItr = serializedObject.GetIterator();
                var currValid = currItr.Next(true);

                while (currValid)
                {
                    parentItem.AddChild(new PropertyItem(serializedObject, currItr.propertyPath)
                    {
                        id = id++,
                        depth = parentItem.depth + 1,
                    });

                    currValid = currItr.Next(false);
                }
            }

            /// <inheritdoc />
            protected override float GetCustomRowHeight(int row, TreeViewItem item)
            {
                if (item is PropertyItem propertyItem)
                {
                    var prop = propertyItem.SerializedObject.FindProperty(propertyItem.Path);
                    return EditorGUI.GetPropertyHeight(prop);
                }

                return base.GetCustomRowHeight(row, item);
            }

            /// <inheritdoc />
            protected override void RowGUI(RowGUIArgs args)
            {
                var rect = args.rowRect;
                rect.xMin = GetContentIndent(args.item);

                switch (args.item)
                {
                    case SceneItem sceneItem:
                    {
                        var scene = sceneItem.Scene;

                        if (scene == null)
                        {
                            GUI.color = Color.red;
                            EditorGUI.LabelField(rect, "(Null SceneState)");
                            GUI.color = Color.white;
                        }
                        else if (!scene.Instance.IsValid())
                        {
                            GUI.color = Color.red;
                            EditorGUI.LabelField(rect, "(Invalid Scene)");
                            GUI.color = Color.white;
                        }
                        else
                        {
                            var instance = scene.Instance;
                            var path = string.IsNullOrEmpty(instance.path) ? "Untitled" : instance.path;
                            EditorGUI.LabelField(rect, path);
                        }
                        break;
                    }
                    case GameObjectItem gameObjectItem:
                    {
                        var gameObject = gameObjectItem.GameObject;

                        if (gameObject == null)
                        {
                            GUI.color = Color.red;
                            EditorGUI.LabelField(rect, "(Null GameObjectState)");
                            GUI.color = Color.white;
                        }
                        else if (gameObject.Instance == null)
                        {
                            GUI.color = Color.red;
                            EditorGUI.LabelField(rect, "(Null GameObject)");
                            GUI.color = Color.white;
                        }
                        else
                        {
                            EditorGUI.LabelField(rect, gameObject.Instance.name);
                        }
                        break;
                    }
                    case ComponentItem componentItem:
                    {
                        var component = componentItem.Component;

                        if (component == null)
                        {
                            GUI.color = Color.red;
                            EditorGUI.LabelField(rect, "(Null ComponentState)");
                            GUI.color = Color.white;
                        }
                        else if (component.Instance == null)
                        {
                            GUI.color = Color.red;
                            EditorGUI.LabelField(rect, "(Null Component)");
                            GUI.color = Color.white;
                        }
                        else
                        {
                            EditorGUI.LabelField(rect, component.Instance.GetType().Name);
                        }
                        break;
                    }
                    case PropertyItem propertyItem:
                    {
                        try
                        {
                            var prop = propertyItem.SerializedObject.FindProperty(propertyItem.Path);

                            GUI.enabled = false;
                            EditorGUI.PropertyField(rect, prop, true);
                            GUI.enabled = true;
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                        break;
                    }
                    default:
                    {
                        EditorGUI.LabelField(rect, args.item.displayName);
                        break;
                    }
                }
            }
        }
    }
}
#endif
