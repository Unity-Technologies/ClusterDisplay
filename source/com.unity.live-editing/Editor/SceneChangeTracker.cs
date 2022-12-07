﻿using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
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

        [MenuItem("Tracking/Dirty Scene")]
        static void Dirty()
        {
            EditorSceneManager.MarkAllScenesDirty();
        }
    }

    /// <summary>
    /// A class used to detect edits made by the user that change scene content.
    /// </summary>
    class SceneChangeTracker : IDisposable
    {
        class SceneState : IDisposable
        {
            public Scene Scene { get; }

            public List<GameObjectState> Roots { get; } = new List<GameObjectState>();

            public SceneState(Scene scene)
            {
                Scene = scene;
            }

            public void Dispose()
            {
                foreach (var rootState in Roots)
                {
                    rootState.Dispose();
                }
            }
        }

        class GameObjectState : IDisposable
        {
            public GameObject GameObject { get; }
            public int InstanceID { get; }
            public PropertyState Properties { get; }

            public SceneState Scene { get; set; }
            public GameObjectState Parent { get; set; }
            public List<GameObjectState> Children { get; } = new List<GameObjectState>();
            public List<ComponentState> Components { get; } = new List<ComponentState>();

            public GameObjectState(GameObject gameObject)
            {
                GameObject = gameObject;
                InstanceID = gameObject.GetInstanceID();
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
                PreviousState.Dispose();
                CurrentState.Dispose();
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
        readonly Dictionary<int, GameObjectState> m_TrackedGameObjects = new Dictionary<int, GameObjectState>();

        // TODO: time-sliced based polling to catch changes not caught by undo?
        // TODO: scene parameters (lighting, etc.)
        // TODO: use index in parent or scene and transform path to specify things like deleted game objects so we can validate the correct thing is removed.

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

            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            ObjectChangeEvents.changesPublished -= OnChangesPublished;

            foreach (var (_, state) in m_TrackedScenes)
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
            // The scene needs to be loaded to access the scene objects.
            if (scene.IsValid() && scene.isLoaded && !m_TrackedScenes.ContainsKey(scene))
            {
                var sceneState = new SceneState(scene);
                m_TrackedScenes.Add(scene, sceneState);

                scene.GetRootGameObjects(s_TempGameObjects);

                for (var i = 0; i < s_TempGameObjects.Count; i++)
                {
                    StartTrackingGameObject(sceneState, null, s_TempGameObjects[i]);
                }
            }
        }

        void StopTrackingScene(Scene scene)
        {
            if (scene.IsValid() && m_TrackedScenes.Remove(scene, out var sceneState))
            {
                sceneState.Dispose();
            }
        }

        GameObjectState StartTrackingGameObject(SceneState sceneState, GameObjectState parentState, GameObject gameObject)
        {
            var goState = new GameObjectState(gameObject)
            {
                Scene = sceneState,
                Parent = parentState,
            };

            m_TrackedGameObjects.Add(goState.InstanceID, goState);

            if (goState.Parent == null)
            {
                sceneState.Roots.Add(goState);
            }

            // Track the components.
            gameObject.GetComponents(s_TempComponents);

            foreach (var component in s_TempComponents)
            {
                goState.Components.Add(new ComponentState(component));
            }

            // Track the child game objects.
            var transform = gameObject.transform;

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                var childState = StartTrackingGameObject(sceneState, goState, child);
                goState.Children.Add(childState);
            }

            return goState;
        }

        void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            try
            {
                Profiler.BeginSample($"{nameof(SceneChangeTracker)}.{nameof(OnChangesPublished)}()");

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

                            if (m_TrackedScenes.TryGetValue(change.scene, out var sceneState))
                            {
                                CheckScene(sceneState);
                            }
                            break;
                        }
                        case ObjectChangeKind.CreateGameObjectHierarchy:
                        {
                            // stream.GetCreateGameObjectHierarchyEvent(i, out var change);
                            //
                            // if (m_TrackedScenes.TryGetValue(change.scene, out var sceneState))
                            // {
                            //     var obj = EditorUtility.InstanceIDToObject(change.instanceId) as GameObject;
                            // }
                            break;
                        }
                        case ObjectChangeKind.DestroyGameObjectHierarchy:
                        {
                            // stream.GetDestroyGameObjectHierarchyEvent(i, out var change);
                            //
                            // if (m_TrackedScenes.TryGetValue(change.scene, out var sceneState))
                            // {
                            //
                            // }
                            break;
                        }
                        case ObjectChangeKind.ChangeGameObjectParent:
                        {
                            // TODO: we must verify that scene changes that don't trigger this event still are tracked as a move instead of destroying/recreating the object
                            //stream.GetChangeGameObjectParentEvent(i, out var change);
                            break;
                        }
                        case ObjectChangeKind.ChangeGameObjectStructure:
                        {
                            stream.GetChangeGameObjectStructureEvent(i, out var change);

                            if (m_TrackedScenes.TryGetValue(change.scene, out var sceneState))
                            {
                                var gameObject = EditorUtility.InstanceIDToObject(change.instanceId) as GameObject;
                                CheckGameObjectStructural(sceneState, gameObject, false);
                            }
                            break;
                        }
                        case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                        {
                            stream.GetChangeGameObjectStructureHierarchyEvent(i, out var change);

                            if (m_TrackedScenes.TryGetValue(change.scene, out var sceneState))
                            {
                                var gameObject = EditorUtility.InstanceIDToObject(change.instanceId) as GameObject;
                                CheckGameObjectStructural(sceneState, gameObject, true);
                            }
                            break;
                        }
                        case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        {
                            stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var change);

                            if (m_TrackedScenes.TryGetValue(change.scene, out var sceneState))
                            {
                                var obj = EditorUtility.InstanceIDToObject(change.instanceId);

                                switch (obj)
                                {
                                    case GameObject gameObject:
                                        // We can't just check the game object properties, components could be reordered.
                                        // Drag-reordering components triggers this, while Move Up/Down in the component
                                        // menu triggers ChangeGameObjectStructure.
                                        CheckGameObjectStructural(sceneState, gameObject, false);
                                        break;
                                    case Component component:
                                        CheckComponent(sceneState, component);
                                        break;
                                }
                            }

                            break;
                        }
                        case ObjectChangeKind.UpdatePrefabInstances:
                        {
                            stream.GetUpdatePrefabInstancesEvent(i, out var change);

                            if (m_TrackedScenes.TryGetValue(change.scene, out var sceneState))
                            {
                                foreach (var instanceId in change.instanceIds)
                                {
                                    var gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                                    CheckGameObjectStructural(sceneState, gameObject, true);
                                }
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
            finally
            {
                Profiler.EndSample();
            }
        }

        /// <summary>
        /// Looks for changes to the scene hierarchy, components, and all properties in a tracked scene.
        /// </summary>
        /// <param name="sceneState">The scene to look for changes in.</param>
        void CheckScene(SceneState sceneState)
        {
            /*
             * Care must be taken ensure that the stream of changes can be applied in order and get correct results,
             * For example, we shouldn't try to update component properties before adding new game objects, as the
             * properties could reference a game object that hasn't been created yet. Therefore, we do the following:
             *
             * 1) Add gameobjects
             * 2) Reparent gameobjects
             * 3) Remove gameobjects
             * 4) Reorder gameobjects
             * 6) Add components
             * 5) Remove components
             * 7) Reorder components
             * 8) Update game object (maybe before adding components?) and component properties
             */

            // Detect added or re-parented game objects.
            sceneState.Scene.GetRootGameObjects(s_TempGameObjects);

            for (var i = 0; i < s_TempGameObjects.Count; i++)
            {
                FindAddedGameObjects(sceneState, null, s_TempGameObjects[i]);
            }

            // Detect removed root game objects.
            for (var i = sceneState.Roots.Count - 1; i >= 0; i--)
            {
                if (FindRemovedGameObjects(sceneState.Roots[i]))
                {
                    Debug.Log($"Change: Removed root of {sceneState.Scene.name} at index {i}.");
                    sceneState.Roots.RemoveAt(i);
                }
            }

            // Detect reordered root game objects.
            for (var currIndex = 0; currIndex < s_TempGameObjects.Count; currIndex++)
            {
                if (HandleReordered(s_TempGameObjects[currIndex], sceneState.Roots, currIndex, out var prevIndex, out var rootState))
                {
                    Debug.Log($"Change: Reorder root game object from {prevIndex} to {currIndex}.");
                }

                FindReorderedGameObjects(rootState);
            }
        }

        GameObjectState FindAddedGameObjects(SceneState sceneState, GameObjectState parentState, GameObject gameObject)
        {
            if (sceneState == null || gameObject == null)
            {
                return null;
            }

            // Check if the game object is new.
            if (!m_TrackedGameObjects.TryGetValue(gameObject.GetInstanceID(), out var goState))
            {
                goState = new GameObjectState(gameObject);
                m_TrackedGameObjects.Add(goState.InstanceID, goState);

                Debug.Log($"Change: Added game object {gameObject}");
            }

            // Check if the game object's parent has changed.
            var sceneChanged = goState.Scene != sceneState;
            var parentChanged = goState.Parent != parentState;

            if (sceneChanged || parentChanged)
            {
                if (goState.Parent == null)
                {
                    goState.Scene?.Roots.Remove(goState);
                }
                else if (parentChanged)
                {
                    goState.Parent.Children.Remove(goState);
                }

                goState.Scene = sceneState;
                goState.Parent = parentState;

                if (goState.Parent == null)
                {
                    goState.Scene.Roots.Add(goState);
                }
                else if (parentChanged)
                {
                    goState.Parent.Children.Add(goState);
                }

                if (goState.Parent == null)
                {
                    Debug.Log($"Change: Set Parent {goState.Scene.Scene.name}->{gameObject.name}");
                }
                else
                {
                    Debug.Log($"Change: Set parent {goState.Parent.GameObject.name}->{gameObject.name}");
                }
            }

            // Check children for changes.
            var transform = gameObject.transform;

            for (var i = 0; i < transform.childCount; i++)
            {
                FindAddedGameObjects(sceneState, goState, transform.GetChild(i).gameObject);
            }

            return goState;
        }

        bool FindRemovedGameObjects(GameObjectState goState)
        {
            // Check if this game object is destroyed.
            if (goState.GameObject == null)
            {
                m_TrackedGameObjects.Remove(goState.InstanceID);
                goState.Dispose();
                return true;
            }

            // Check children for removed game objects.
            for (var i = goState.Children.Count - 1; i >= 0; i--)
            {
                if (FindRemovedGameObjects(goState.Children[i]))
                {
                    Debug.Log($"Change: Removed child of {goState.GameObject.name} at index {i}.");
                    goState.Children.RemoveAt(i);
                }
            }

            return false;
        }

        void FindReorderedGameObjects(GameObjectState goState)
        {
            var transform = goState.GameObject.transform;

            for (var currIndex = 0; currIndex < transform.childCount; currIndex++)
            {
                var child = transform.GetChild(currIndex).gameObject;

                if (HandleReordered(child, goState.Children, currIndex, out var prevIndex, out var childState))
                {
                    Debug.Log($"Change: Reorder game object from {prevIndex} to {currIndex}.");
                }

                FindReorderedGameObjects(childState);
            }
        }

        static bool HandleReordered(GameObject obj, List<GameObjectState> siblings, int currIndex, out int prevIndex, out GameObjectState sibling)
        {
            for (prevIndex = 0; prevIndex < siblings.Count; prevIndex++)
            {
                sibling = siblings[prevIndex];

                if (sibling.GameObject != obj)
                {
                    continue;
                }

                if (currIndex != prevIndex)
                {
                    siblings.RemoveAt(prevIndex);
                    siblings.Insert(currIndex, sibling);
                    return true;
                }

                break;
            }

            sibling = default;
            return false;
        }

        /// <summary>
        /// Looks for changes to the properties, components, and children of a game object.
        /// </summary>
        /// <remarks>
        /// If the game object is not yet tracked, it will be tracked and the initial state of the game object will be reported.
        /// </remarks>
        /// <param name="sceneState">The scene the game object is in.</param>
        /// <param name="gameObject">The game object to look for changes in.</param>
        /// <param name="includeChildren">Look for changes in all children of the game object.</param>
        GameObjectState CheckGameObjectStructural(SceneState sceneState, GameObject gameObject, bool includeChildren)
        {
            if (gameObject == null)
            {
                return null;
            }

            var previouslyTracked = sceneState.TryGetTrackedObject(gameObject, out var goState);

            if (!previouslyTracked)
            {
                goState = new GameObjectState(gameObject)
                {
                    Scene = sceneState,
                };

                sceneState.AddTrackedObject(goState);

                Debug.Log($"Change: Added game object {gameObject}");

                var parent = gameObject.transform.parent;

                if (parent != null && sceneState.TryGetTrackedObject(parent.gameObject, out var parentState))
                {
                    goState.Parent = parentState;
                    parentState.Children.Add(goState);

                    Debug.Log($"Change: Set parent {parent.name}->{gameObject.name}");
                }

                if (goState.Parent == null)
                {
                    sceneState.Roots.Add(goState);
                }
            }

            // TODO: we must ensure that all gameobjects are created before components, then components before all properties,
            // otherwise, it might not be possible to fill the serialized properties values.

            if (goState.Properties.Update() || !previouslyTracked)
            {
                CheckProperties(goState.Properties, previouslyTracked);
                CheckComponentsStructural(goState);
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

            if (includeChildren)
            {
                CheckChildrenStructural(goState);
            }

            return goState;
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

                    goState.Scene.RemoveTrackedObject(prevChild);
                    goState.Children.RemoveAt(i);
                    prevChild.Dispose();
                }

                // TODO: handle re-parented objects, they won't be null
            }

            // Detect added and reordered children.
            var transform = goState.GameObject.transform;

            for (var currIndex = 0; currIndex < transform.childCount; currIndex++)
            {
                var child = transform.GetChild(currIndex).gameObject;

                // Find the previous index of the child.
                var prevIndex = goState.Children.Count;

                for (var i = 0; i < goState.Children.Count; i++)
                {
                    var prevChild = goState.Children[i];

                    if (prevChild.GameObject == child)
                    {
                        prevIndex = i;
                        break;
                    }
                }

                // Check the child for changes.
                var childState = CheckGameObjectStructural(goState.Scene, child, true);

                // Check if the child index has changed.
                if (currIndex != prevIndex)
                {
                    Debug.Log($"Change: Reorder child from {prevIndex} to {currIndex}.");

                    goState.Children.RemoveAt(prevIndex);
                    goState.Children.Insert(currIndex, childState);
                }
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
                var previouslyTracked = false;
                var componentState = default(ComponentState);
                var prevIndex = goState.Components.Count;

                for (var i = 0; i < goState.Components.Count; i++)
                {
                    var prevComponent = goState.Components[i];

                    if (prevComponent.Component == component)
                    {
                        previouslyTracked = true;
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
                CheckProperties(componentState.Properties, previouslyTracked);
            }
        }

        /// <summary>
        /// Looks for changes to the properties of a single component.
        /// </summary>
        /// <param name="sceneState">The scene the component is in.</param>
        /// <param name="component">The component to look for changes in.</param>
        void CheckComponent(SceneState sceneState, Component component)
        {
            if (component == null || !sceneState.TryGetTrackedObject(component.gameObject, out var goState))
            {
                return;
            }

            // Find the properties that correspond to the given component.
            foreach (var componentState in goState.Components)
            {
                if (componentState.Component == component)
                {
                    if (componentState.Properties.Update())
                    {
                        CheckProperties(componentState.Properties);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Finds all changes made to a Unity Object.
        /// </summary>
        /// <param name="state">The state of the object.</param>
        /// <param name="onlyChanged">
        /// When <see langword="true"/>, reports only modified properties as changed.
        /// When <see langword="false"/>, reports all properties as changed regardless of any previous value.
        /// </param>
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
                // TODO: validate this doesn't need special handling, I suspect it won't
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
