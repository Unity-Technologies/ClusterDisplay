#if UNITY_LIVE_EDITING_DEBUG_asadfasdf
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

        SceneChangeTracker m_SceneChangeTracker;
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

            m_SceneChangeTracker = new SceneChangeTracker();
            m_SceneChangeTracker.Start();

            CreateTreeView();
        }

        void OnDisable()
        {
            if (m_SceneChangeTracker != null)
            {
                m_SceneChangeTracker.Dispose();
                m_SceneChangeTracker = null;
            }
        }

        void OnGUI()
        {
            if (m_TreeView == null)
            {
                CreateTreeView();
            }

            m_TreeDirty = true;

            if (m_TreeDirty)
            {
                m_TreeView.Reload();
                m_TreeDirty = false;
            }

            EditorGUILayout.LabelField($"Tracked Game Object Count: {m_SceneChangeTracker.TrackedGameObjectCount}");

            EditorGUIUtility.hierarchyMode = true;
            EditorGUIUtility.wideMode = true;

            m_TreeView.OnGUI(new Rect(0, 20, position.width, position.height - EditorGUIUtility.singleLineHeight));
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

                var rootItem = new TreeViewItem
                {
                    id = id++,
                    depth = -1,
                    displayName = "Root",
                };

                var scenes = m_SceneChangeTracker.TrackedScenes;

                foreach (var scene in scenes)
                {
                    var sceneItem = new SceneItem(scene)
                    {
                        id = id++,
                        depth = 0,
                    };

                    if (scene != null)
                    {
                        var sceneRootsItem = new TreeViewItem
                        {
                            id = id++,
                            depth = sceneItem.depth + 1,
                            displayName = "Roots"
                        };

                        foreach (var root in scene.Roots)
                        {
                            sceneRootsItem.AddChild(AddGameObject(root, sceneRootsItem.depth + 1, ref id));
                        };

                        sceneItem.AddChild(sceneRootsItem);
                    }

                    rootItem.AddChild(sceneItem);
                }

                if (rootItem.children == null)
                {
                    rootItem.children = new List<TreeViewItem>();
                }

                return rootItem;
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

                    AddProperties(goState.Properties, propertiesItem, ref id);

                    gameObjectItem.AddChild(propertiesItem);

                    var componentsItem = new TreeViewItem
                    {
                        id = id++,
                        depth = gameObjectItem.depth + 1,
                        displayName = "Components"
                    };

                    foreach (var component in goState.Components)
                    {
                        var componentItem = new ComponentItem(component)
                        {
                            id = id++,
                            depth = componentsItem.depth + 1,
                        };

                        if (component != null)
                        {
                            AddProperties(component.Properties, componentItem, ref id);
                        }

                        componentsItem.AddChild(componentItem);
                    }

                    gameObjectItem.AddChild(componentsItem);

                    if (goState.Children.Count > 0)
                    {
                        var childrenItem = new TreeViewItem
                        {
                            id = id++,
                            depth = gameObjectItem.depth + 1,
                            displayName = "Children"
                        };

                        foreach (var child in goState.Children)
                        {
                            childrenItem.AddChild(AddGameObject(child, childrenItem.depth + 1, ref id));
                        }

                        gameObjectItem.AddChild(childrenItem);
                    }
                }

                return gameObjectItem;
            }

            void AddProperties(SceneChangeTracker.PropertyState propertiesState, TreeViewItem parentItem, ref int id)
            {
                var currProps = propertiesState.CurrentState;
                var currItr = currProps.GetIterator();
                var currValid = currItr.Next(true);

                while (currValid)
                {
                    parentItem.AddChild(new PropertyItem(currProps, currItr.propertyPath)
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
                            GUI.color = Color.black;
                        }
                        else if (!scene.Scene.IsValid())
                        {
                            GUI.color = Color.red;
                            EditorGUI.LabelField(rect, "(Invalid Scene)");
                            GUI.color = Color.black;
                        }
                        else
                        {
                            EditorGUI.LabelField(rect, scene.Scene.path);
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
                            GUI.color = Color.black;
                        }
                        else if (gameObject.GameObject == null)
                        {
                            GUI.color = Color.red;
                            EditorGUI.LabelField(rect, "(Null GameObject)");
                            GUI.color = Color.black;
                        }
                        else
                        {
                            EditorGUI.LabelField(rect, gameObject.GameObject.name);
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
                            GUI.color = Color.black;
                        }
                        else if (component.Component == null)
                        {
                            GUI.color = Color.red;
                            EditorGUI.LabelField(rect, "(Null Component)");
                            GUI.color = Color.black;
                        }
                        else
                        {
                            EditorGUI.LabelField(rect, component.Component.GetType().Name);
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

        void CreateTreeView()
        {
            if (m_TreeViewState == null)
            {
                m_TreeViewState = new TreeViewState();
            }

            m_TreeView = new SceneChangeTrackerTreeView(m_TreeViewState, m_SceneChangeTracker);
        }
    }
}
#endif
