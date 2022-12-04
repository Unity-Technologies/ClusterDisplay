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

        class GameObjectState : IDisposable
        {
            public GameObject GameObject { get; }
            public PropertyState Properties { get; }
            public SceneState Scene { get; set; }
            public GameObjectState Parent { get; set; }
            public List<GameObjectState> Children { get; } = new List<GameObjectState>();
            public List<ComponentState> Components { get; } = new List<ComponentState>();

            public GameObjectState(GameObject gameObject)
            {
                GameObject = gameObject;
                Properties = new PropertyState(gameObject);
            }

            public void Dispose()
            {
                Properties.Dispose();

                foreach (var child in Children)
                {
                    child.Dispose();
                }

                foreach (var component in Components)
                {
                    component.Dispose();
                }
            }
        }

        class ComponentState : IDisposable
        {
            public Component Component { get; }
            public PropertyState Properties { get; }

            public ComponentState(Component component)
            {
                Component = component;
                Properties = new PropertyState(component);
            }

            public void Dispose()
            {
                Properties.Dispose();
            }
        }

        class PropertyState : IDisposable
        {
            public SerializedObject PreviousState { get; private set; }
            public SerializedObject CurrentState { get; private set; }

            public PropertyState(UnityObject obj)
            {
                PreviousState = new SerializedObject(obj);
                CurrentState = new SerializedObject(obj);
            }

            public void Dispose()
            {
                PreviousState?.Dispose();
                CurrentState?.Dispose();
            }

            public bool Update()
            {
                var newState = PreviousState;
                PreviousState = CurrentState;
                CurrentState = newState;

                return CurrentState.UpdateIfRequiredOrScript();
            }
        }

        static readonly List<GameObject> s_TempGameObjects = new List<GameObject>();
        static readonly List<Component> s_TempComponents = new List<Component>();

        bool m_IsRunning;
        readonly Dictionary<Scene, SceneState> m_TrackedScenes = new Dictionary<Scene, SceneState>();
        readonly Dictionary<GameObject, GameObjectState> m_TrackedGameObjects = new Dictionary<GameObject, GameObjectState>();

        // TODO: time-sliced based polling to catch changes not caught by undo?

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

            foreach (var (_, state) in m_TrackedGameObjects)
            {
                state.Dispose();
            }

            m_TrackedScenes.Clear();
            m_TrackedGameObjects.Clear();

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

            Debug.Log($"StartTrackingScene {scene.name}");

            m_TrackedScenes.Add(scene, new SceneState(scene));

            scene.GetRootGameObjects(s_TempGameObjects);

            foreach (var root in s_TempGameObjects)
            {
                StartTrackingGameObject(root);
            }
        }

        void StopTrackingScene(Scene scene)
        {
            if (!scene.IsValid() || !m_TrackedScenes.Remove(scene, out _))
            {
                return;
            }

            Debug.Log($"StopTrackingScene {scene.name}");

            scene.GetRootGameObjects(s_TempGameObjects);

            foreach (var root in s_TempGameObjects)
            {
                StopTrackingGameObject(root);
            }
        }

        void StartTrackingGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }
            if (m_TrackedGameObjects.TryGetValue(gameObject, out _))
            {
                Debug.LogError($"Game Object \"{gameObject}\" in already tracked \"{gameObject.scene}\".");
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

            // Track the game object's components.
            gameObject.GetComponents(s_TempComponents);

            foreach (var component in s_TempComponents)
            {
                state.Components.Add(new ComponentState(component));
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
            if (gameObject == null || !m_TrackedGameObjects.Remove(gameObject, out var state))
            {
                return;
            }

            state.Dispose();
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
                    case ObjectChangeKind.ChangeScene:
                    {
                        stream.GetChangeSceneEvent(i, out var change);

                        change.scene.GetRootGameObjects(s_TempGameObjects);

                        foreach (var root in s_TempGameObjects)
                        {
                            //CheckGameObjectStructural();
                        }
                        break;
                    }
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    {
                        //stream.GetCreateGameObjectHierarchyEvent(i, out var change);
                        break;
                    }
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                    {
                        //stream.GetDestroyGameObjectHierarchyEvent(i, out var change);
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectParent:
                    {
                        //stream.GetChangeGameObjectParentEvent(i, out var change);
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    {
                        stream.GetChangeGameObjectStructureEvent(i, out var change);
                        var obj = EditorUtility.InstanceIDToObject(change.instanceId) as GameObject;
                        CheckGameObjectStructural(obj, false);
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    {
                        stream.GetChangeGameObjectStructureHierarchyEvent(i, out var change);
                        var obj = EditorUtility.InstanceIDToObject(change.instanceId) as GameObject;
                        CheckGameObjectStructural(obj, true);
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    {
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var change);

                        var obj = EditorUtility.InstanceIDToObject(change.instanceId);

                        switch (obj)
                        {
                            case GameObject gameObject:
                                // We can't just check the game object properties, components could be reordered.
                                // Drag-reordering components triggers this, while Move Up/Down in the component
                                // menu triggers ChangeGameObjectStructure.
                                CheckGameObjectStructural(gameObject, false);
                                break;
                            case Component component:
                                CheckComponent(component);
                                break;
                        }
                        break;
                    }
                    case ObjectChangeKind.UpdatePrefabInstances:
                    {
                        stream.GetUpdatePrefabInstancesEvent(i, out var change);

                        foreach (var instanceId in change.instanceIds)
                        {
                            var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                            CheckGameObjectStructural(obj, true);
                        }
                        break;
                    }
                    case ObjectChangeKind.CreateAssetObject:
                    case ObjectChangeKind.DestroyAssetObject:
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                    {
                        // Ignore changes to assets.
                        break;
                    }
                    case ObjectChangeKind.None:
                    {
                        break;
                    }
                    default:
                    {
                        Debug.LogError($"Unknown change type: \"{type}\".");
                        break;
                    }
                }
            }
        }

        void CheckGameObjectStructural(GameObject gameObject, bool includeChildren)
        {
            if (gameObject == null || !m_TrackedGameObjects.TryGetValue(gameObject, out var goState))
            {
                return;
            }

            if (includeChildren)
            {
                CheckChildrenStructural(goState);
            }

            if (goState.Properties.Update())
            {
                CheckComponentsStructural(goState);
                CheckProperties(goState.Properties);
            }
            else
            {
                // If the game object's properties have not changed, we still must check
                // component properties for changes.
                foreach (var componentState in goState.Components)
                {
                    CheckProperties(componentState.Properties);
                }
            }
        }

        void CheckChildrenStructural(GameObjectState goState)
        {
            // Detect removed children.
            for (var i = goState.Children.Count - 1; i >= 0; i--)
            {
                var prevChild = goState.Children[i];

                if (prevChild.GameObject == null)
                {
                    Debug.Log($"Change: Removed child at index {i}.");

                    goState.Children.RemoveAt(i);
                    prevChild.Dispose();
                }
            }

            // Detect added and reordered children.
            var transform = goState.GameObject.transform;

            for (var currIndex = 0; currIndex < transform.childCount; currIndex++)
            {
                var child = transform.GetChild(currIndex).gameObject;

                // Find the state corresponding to the child.
                var childState = default(GameObjectState);
                var prevIndex = -1;

                for (var i = 0; i < goState.Children.Count; i++)
                {
                    var prevChild = goState.Children[i];

                    if (prevChild.GameObject == child)
                    {
                        childState = prevChild;
                        prevIndex = i;
                        break;
                    }
                }

                // If the child was just added, track it.
                if (childState == null)
                {
                    Debug.Log($"Change: Added child {child}.");

                    childState = new GameObjectState(child)
                    {
                        Scene = goState.Scene,
                        Parent = goState,
                    };

                    m_TrackedGameObjects.Add(child, childState);

                    prevIndex = goState.Children.Count;
                    goState.Children.Add(childState);
                }

                // Check if the child index has changed.
                if (currIndex != prevIndex)
                {
                    Debug.Log($"Change: Reorder child from {prevIndex} to {currIndex}.");

                    goState.Children.RemoveAt(prevIndex);
                    goState.Children.Insert(currIndex, childState);
                }

                // Check the child for changes.
                CheckGameObjectStructural(child, true);
            }
        }

        void CheckComponentsStructural(GameObjectState goState)
        {
            // Detect removed components.
            for (var i = goState.Components.Count - 1; i >= 0; i--)
            {
                var prevComponent = goState.Components[i];

                if (prevComponent.Component == null)
                {
                    Debug.Log($"Change: Removed component at index {i}.");

                    goState.Components.RemoveAt(i);
                    prevComponent.Dispose();
                }
            }

            // Detect added and reordered components.
            goState.GameObject.GetComponents(s_TempComponents);

            for (var currIndex = 0; currIndex < s_TempComponents.Count; currIndex++)
            {
                var component = s_TempComponents[currIndex];

                // Find the state corresponding to the component.
                var previouslyExisted = false;
                var componentState = default(ComponentState);
                var prevIndex = -1;

                for (var i = 0; i < goState.Components.Count; i++)
                {
                    var prevComponent = goState.Components[i];

                    if (prevComponent.Component == component)
                    {
                        previouslyExisted = true;
                        componentState = prevComponent;
                        prevIndex = i;
                        break;
                    }
                }

                // If the component was just added, track it.
                if (componentState == null)
                {
                    Debug.Log($"Change: Added component {component}.");

                    componentState = new ComponentState(component);

                    prevIndex = goState.Components.Count;
                    goState.Components.Add(componentState);
                }

                // Check if the component index has changed.
                if (currIndex != prevIndex)
                {
                    Debug.Log($"Change: Reorder component from {prevIndex} to {currIndex}.");

                    goState.Components.RemoveAt(prevIndex);
                    goState.Components.Insert(currIndex, componentState);
                }

                // Check for component properties for changes.
                CheckProperties(componentState.Properties, previouslyExisted);
            }
        }

        void CheckComponent(Component component)
        {
            if (component == null || !m_TrackedGameObjects.TryGetValue(component.gameObject, out var goState))
            {
                return;
            }

            // Find the properties that correspond to the given component.
            foreach (var componentState in goState.Components)
            {
                if (componentState.Component != component)
                {
                    continue;
                }

                if (componentState.Properties.Update())
                {
                    CheckProperties(componentState.Properties);
                }

                break;
            }
        }

        void CheckProperties(PropertyState state, bool onlyChanged = true)
        {
            // Double buffer the serialized state of objects using two serialized objects. When the source object has changed,
            // we update the older serialized object with the source object's current property values. Then iterate through the
            // properties comparing the previous property values with the current property values. If the values are different,
            // we know that property has changed.
            var prevItr = onlyChanged ? state.PreviousState.GetIterator() : null;
            var currItr = state.CurrentState.GetIterator();
            var prevValid = prevItr?.Next(true) ?? false;
            var currValid = currItr.Next(true);

            while (currValid)
            {
                CheckProperties(prevItr, currItr);

                if (prevValid)
                {
                    prevValid = prevItr.Next(false);
                }

                currValid = currItr.Next(false);
            }
        }

        void CheckProperties(SerializedProperty previousProp, SerializedProperty currentProp)
        {
            switch (currentProp.propertyType)
            {
                // TODO:
                //case SerializedPropertyType.ManagedReference:

                case SerializedPropertyType.Generic:
                {
                    // Check for changes in any child properties by iterating over them.
                    // If this generic property is an array, there could be a difference in the number of elements
                    // between the previous and current state. In general, we notify if the array size has changed,
                    // and update different values in the array elements, which implicitly handles inserting or
                    // removing from the middle of the list. A special case is when adding to the list, there is no
                    // previous value to compare against, so we say that every new value is "changed" and set the previous.
                    // value as null.
                    var prevItr = previousProp?.Copy();
                    var currItr = currentProp.Copy();
                    var prevEnd = previousProp?.GetEndProperty(true);
                    var currEnd = currentProp.GetEndProperty(true);
                    var prevValid = prevItr?.Next(true) ?? false;
                    var currValid = currItr.Next(true);

                    while (currValid)
                    {
                        CheckProperties(prevValid ? prevItr : null, currItr);

                        if (prevValid)
                        {
                            prevValid = prevItr.Next(false) && !SerializedProperty.EqualContents(prevItr, prevEnd);
                        }

                        currValid = currItr.Next(false) && !SerializedProperty.EqualContents(currItr, currEnd);
                    }
                    break;
                }
                default:
                {
                    if (previousProp == null || !SerializedProperty.DataEquals(previousProp, currentProp))
                    {
                        Debug.Log($"Change: {currentProp.propertyPath} {currentProp.propertyType} {currentProp.type}");
                    }
                    break;
                }
            }
        }
    }
}
