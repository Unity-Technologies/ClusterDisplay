using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor
{
    using UnityObject = UnityEngine.Object;

    static class Test
    {
        static readonly SceneChangeTracker s_Tracker = new SceneChangeTracker();

        [InitializeOnLoadMethod]
        static void Init()
        {
            s_Tracker.Start();

            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                s_Tracker.Stop();
            };
        }

        [MenuItem("Tracking/Start")]
        static void Start()
        {
            s_Tracker.Start();
        }

        [MenuItem("Tracking/Stop")]
        static void Stop()
        {
            s_Tracker.Stop();
        }
    }

    /// <summary>
    /// A class used to detect edits made by the user that change scene content.
    /// </summary>
    class SceneChangeTracker : IDisposable
    {
        class SceneState
        {
            public Scene Scene { get; }

            public SceneState(Scene scene)
            {
                Scene = scene;
            }
        }

        class GameObjectState
        {
            public GameObject GameObject { get; }

            public SceneState Scene { get; set; }
            public GameObjectState Parent { get; set; }
            public List<GameObjectState> Children { get; } = new List<GameObjectState>();
            public List<ComponentState> Components { get; } = new List<ComponentState>();

            public GameObjectState(GameObject gameObject)
            {
                GameObject = gameObject;
            }
        }

        class ComponentState
        {
            public Component Component { get; }

            public ComponentState(Component component)
            {
                Component = component;
            }
        }

        class PropertyState : IDisposable
        {
            public SerializedObject LastState { get; }
            public SerializedObject CurrentState { get; }

            public PropertyState(UnityObject obj)
            {
                LastState = new SerializedObject(obj);
                CurrentState = new SerializedObject(obj);
            }

            public void Dispose()
            {
                LastState?.Dispose();
                CurrentState?.Dispose();
            }
        }

        static readonly List<GameObject> s_TempGameObjects = new List<GameObject>();
        static readonly List<Component> s_TempComponents = new List<Component>();

        bool m_IsRunning;
        readonly Dictionary<Scene, SceneState> m_TrackedScenes = new Dictionary<Scene, SceneState>();
        readonly Dictionary<GameObject, GameObjectState> m_TrackedGameObjects = new Dictionary<GameObject, GameObjectState>();
        readonly Dictionary<UnityObject, PropertyState> m_TrackedProperties = new Dictionary<UnityObject, PropertyState>();

        /// <summary>
        /// Releases resources held by this instance.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Start tracking changes made to any loaded scenes.
        /// </summary>
        public void Start()
        {
            if (m_IsRunning)
            {
                return;
            }

            Debug.Log("Start");

            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            ObjectChangeEvents.changesPublished += OnChangesPublished;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                StartTrackingScene(SceneManager.GetSceneAt(i));
            }

            m_IsRunning = true;
        }

        /// <summary>
        /// Stop tracking changes made to and loaded scenes.
        /// </summary>
        public void Stop()
        {
            if (!m_IsRunning)
            {
                return;
            }

            Debug.Log("Stop");

            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            ObjectChangeEvents.changesPublished -= OnChangesPublished;

            foreach (var (_, propertyState) in m_TrackedProperties)
            {
                propertyState.Dispose();
            }

            m_TrackedScenes.Clear();
            m_TrackedGameObjects.Clear();
            m_TrackedProperties.Clear();

            m_IsRunning = false;
        }

        void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            StartTrackingScene(scene);
        }

        void OnSceneClosing(Scene scene, bool removingScene)
        {
            StopTrackingScene(scene);
        }

        void StartTrackingScene(Scene scene)
        {
            Debug.Log($"StartTrackingScene {scene.name}");

            if (!scene.IsValid() || m_TrackedScenes.ContainsKey(scene))
            {
                return;
            }

            // The scene needs to be loaded to access the scene contents.
            if (!scene.isLoaded)
            {
                return;
            }

            // TODO: we can make this class more generic for general change detection without this...
            // Only consider scenes that are saved, don't sync untitled scenes.
            if (string.IsNullOrEmpty(scene.path))
            {
                return;
            }

            m_TrackedScenes.Add(scene, new SceneState(scene));

            scene.GetRootGameObjects(s_TempGameObjects);

            foreach (var root in s_TempGameObjects)
            {
                ForEachInHierarchy(root, StartTrackingGameObject);
            }
        }

        void StopTrackingScene(Scene scene)
        {
            Debug.Log($"StopTrackingScene {scene.name}");

            if (!scene.IsValid() || !m_TrackedScenes.ContainsKey(scene))
            {
                return;
            }

            m_TrackedScenes.Remove(scene);

            scene.GetRootGameObjects(s_TempGameObjects);

            foreach (var root in s_TempGameObjects)
            {
                ForEachInHierarchy(root, StopTrackingGameObject);
            }
        }

        void StartTrackingGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }
            if (!m_TrackedScenes.TryGetValue(gameObject.scene, out var sceneState))
            {
                Debug.LogError($"Cannot track Game Object \"{gameObject}\" in untracked scene \"{gameObject.scene}\".");
                return;
            }

            // Track the parent game objects if they are not tracked yet.
            var transform = gameObject.transform;
            var parent = transform.parent;
            var parentState = default(GameObjectState);

            if (parent != null && !m_TrackedGameObjects.TryGetValue(parent.gameObject, out parentState))
            {
                StartTrackingGameObject(parent.gameObject);
                return;
            }

            // Capture the game object's initial state.
            var state = new GameObjectState(gameObject)
            {
                Scene = sceneState,
                Parent = parentState,
            };

            m_TrackedGameObjects.Add(gameObject, state);

            StartTrackingProperties(gameObject);

            // Track the game object's components.
            gameObject.GetComponents(s_TempComponents);

            foreach (var component in s_TempComponents)
            {
                state.Components.Add(new ComponentState(component));
                StartTrackingProperties(component);
            }

            // Track the child game objects.
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;

                StartTrackingGameObject(child);

                if (m_TrackedGameObjects.TryGetValue(child, out var childState))
                {
                    state.Children.Add(childState);
                }
            }
        }

        void StopTrackingGameObject(GameObject gameObject)
        {

            StopTrackingProperties(gameObject);
        }

        void StartTrackingProperties(UnityObject obj)
        {
            m_TrackedProperties.Add(obj, new PropertyState(obj));
        }

        void StopTrackingProperties(UnityObject obj)
        {
            if (m_TrackedProperties.TryGetValue(obj, out var state))
            {
                m_TrackedProperties.Remove(obj);
                state.Dispose();
            }
        }

        void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (!stream.isCreated)
            {
                return;
            }

            for (var i = 0; i < stream.length; i++)
            {
                var type = stream.GetEventType(i);

                Debug.Log(type);

                switch (type)
                {
                    case ObjectChangeKind.None:
                        break;
                    case ObjectChangeKind.ChangeScene:
                        break;
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                        break;
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructure:
                        break;
                    case ObjectChangeKind.ChangeGameObjectParent:
                        break;
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    {
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var change);
                        CheckForChanges(EditorUtility.InstanceIDToObject(change.instanceId));
                        break;
                    }
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                        break;
                    case ObjectChangeKind.CreateAssetObject:
                        break;
                    case ObjectChangeKind.DestroyAssetObject:
                        break;
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        break;
                    case ObjectChangeKind.UpdatePrefabInstances:
                        break;
                    default:
                    {
                        Debug.LogError($"Unknown change type: \"{type}\".");
                        break;
                    }
                }
            }
        }

        void CheckForChanges(UnityObject obj)
        {
            if (obj == null || !m_TrackedProperties.TryGetValue(obj, out var state))
            {
                return;
            }

            state.CurrentState.UpdateIfRequiredOrScript();
            var currentProp = state.CurrentState.GetIterator();

            while (currentProp.Next(true))
            {
                if (state.LastState.CopyFromSerializedPropertyIfDifferent(currentProp))
                {
                }
                Debug.Log($"{currentProp.name} {currentProp.ToString()}");
            }
        }

        static void ForEachInHierarchy(GameObject gameObject, Action<GameObject> predicate)
        {
            if (gameObject == null || predicate == null)
            {
                return;
            }

            predicate(gameObject);

            var transform = gameObject.transform;

            for (var i = 0; i < transform.childCount; i++)
            {
                ForEachInHierarchy(transform.GetChild(i).gameObject, predicate);
            }
        }
    }
}
