using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Unity.LiveEditing.Editor
{
    using Debug = UnityEngine.Debug;
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

            EditorApplication.update += Update;
        }

        static void Update()
        {
            s_Tracker.Update();
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
        internal abstract class BaseState<TObject, TKey, TData, TState> : IDisposable
            where TData : IDisposable
            where TState : BaseState<TObject, TKey, TData, TState>
        {
            public Guid Guid { get; }
            public TObject Instance { get; }
            public TData Previous { get; protected set; }
            public TData Current { get; protected set; }
            public bool IsNew { get; set; }

            Dictionary<TKey, TState> m_TrackedObjects;
            TKey m_Key;

            protected BaseState(TObject instance, bool isNew, Dictionary<TKey, TState> trackedObjects, Func<TObject, TKey> getKey)
            {
                Guid = Guid.NewGuid();
                Instance = instance;
                IsNew = isNew;

                m_Key = getKey(instance);
                m_TrackedObjects = trackedObjects;
                m_TrackedObjects.Add(m_Key, (TState)this);
            }

            public virtual void Dispose()
            {
                Previous.Dispose();
                Current.Dispose();

                m_TrackedObjects.Remove(m_Key);
            }

            public void Update()
            {
                var temp = Previous;
                Previous = Current;
                Current = temp;
            }
        }

        internal abstract class UnityObjectState<TObject, TData, TState> : BaseState<TObject, int, TData, TState>
            where TObject : UnityObject
            where TData : IDisposable
            where TState : UnityObjectState<TObject, TData, TState>
        {
            internal class UnityObjectData : IDisposable
            {
                public SerializedObject Properties { get; }
                public bool PropertiesChanged { get; private set; }

                protected UnityObjectData(TObject instance)
                {
                    Properties = new SerializedObject(instance);
                }

                public virtual void Dispose()
                {
                    Properties.Dispose();
                }

                public void UpdateProperties()
                {
                    PropertiesChanged = Properties.UpdateIfRequiredOrScript();
                }
            }

            protected UnityObjectState(TObject instance, bool isNew, Dictionary<int, TState> trackedObjects)
                : base(instance, isNew, trackedObjects, x => x.GetInstanceID())
            {
            }
        }

        internal class SceneState : BaseState<Scene, Scene, SceneState.Data, SceneState>
        {
            internal class Data : IDisposable
            {
                public List<GameObjectState> Roots { get; } = new List<GameObjectState>();

                public void Dispose()
                {
                    foreach (var rootState in Roots)
                    {
                        rootState.Dispose();
                    }
                }
            }

            public SceneState(Scene instance, Dictionary<Scene, SceneState> trackedObjects)
                : base(instance, false, trackedObjects, x => x)
            {
                Previous = new Data();
                Current = new Data();
            }
        }

        internal class GameObjectState : UnityObjectState<GameObject, GameObjectState.Data, GameObjectState>
        {
            internal class Data : UnityObjectData
            {
                public SceneState Scene { get; set; }
                public GameObjectState Parent { get; set; }
                public int Index { get; set; }
                public List<ComponentState> Components { get; } = new List<ComponentState>();
                public List<GameObjectState> Children { get; } = new List<GameObjectState>();

                public Data(GameObject instance) : base(instance)
                {
                }

                public override void Dispose()
                {
                    base.Dispose();

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

            public GameObjectState(GameObject instance, bool isNew, Dictionary<int, GameObjectState> trackedObjects)
                : base(instance, isNew, trackedObjects)
            {
                Previous = new Data(instance);
                Current = new Data(instance);
            }
        }

        internal class ComponentState : UnityObjectState<Component, ComponentState.Data, ComponentState>
        {
            internal class Data : UnityObjectData
            {
                public int Index { get; set; }

                public Data(Component instance) : base(instance)
                {
                }
            }

            public Type Type { get; }
            public GameObjectState GameObject { get; }

            public ComponentState(GameObjectState goState, Component instance, bool isNew,  Dictionary<int, ComponentState> trackedObjects)
                : base(instance, isNew, trackedObjects)
            {
                Type = instance.GetType();
                GameObject = goState;
                Previous = new Data(instance);
                Current = new Data(instance);
            }
        }

        /// <summary>
        /// The maximum time that the scene tracking can consume in a single frame in microseconds.
        /// </summary>
        const long k_MaxUpdateTimeSlice = 5 * 1000L;

        static readonly List<GameObject> s_TempGameObjects = new List<GameObject>();
        static readonly List<Component> s_TempComponents = new List<Component>();

        bool m_IsRunning;
        readonly Stopwatch m_UpdateStopwatch = new Stopwatch();

        internal readonly Dictionary<Scene, SceneState> m_TrackedScenes = new Dictionary<Scene, SceneState>();
        internal readonly Dictionary<int, GameObjectState> m_TrackedGameObjects = new Dictionary<int, GameObjectState>();
        internal readonly Dictionary<int, ComponentState> m_TrackedComponents = new Dictionary<int, ComponentState>();

        readonly Queue<SceneState> m_ScenesToCheckForChanges = new Queue<SceneState>();
        readonly Queue<GameObjectState> m_GameObjectsToCheckForChanges = new Queue<GameObjectState>();

        readonly Queue<GameObjectState> m_AddedGameObjects = new Queue<GameObjectState>();
        readonly Queue<GameObjectState> m_DestroyedGameObjects = new Queue<GameObjectState>();
        readonly Queue<GameObjectState> m_ReparentedGameObjects = new Queue<GameObjectState>();
        readonly Queue<GameObjectState> m_ReorderedGameObjects = new Queue<GameObjectState>();
        readonly Queue<GameObjectState> m_ModifiedGameObjects = new Queue<GameObjectState>();
        readonly Queue<ComponentState> m_AddedComponents = new Queue<ComponentState>();
        readonly Queue<ComponentState> m_DestroyedComponents = new Queue<ComponentState>();
        readonly Queue<ComponentState> m_ModifiedComponents = new Queue<ComponentState>();
        readonly Queue<ComponentState> m_ReorderedComponents = new Queue<ComponentState>();

        // TODO: scene parameters (lighting, etc.)

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.GameObjectAdded"/> event.
        /// </summary>
        /// <param name="guid">The unique ID of the added game object.</param>
        /// <param name="gameObject">The game object that was added.</param>
        public delegate void GameObjectAddedEventHandler(Guid guid, GameObject gameObject);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.GameObjectDestroyed"/> event.
        /// </summary>
        /// <param name="guid">The unique ID of the destroyed game object.</param>
        public delegate void GameObjectDestroyedEventHandler(Guid guid);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.GameObjectParentChanged"/> event.
        /// </summary>
        /// <param name="guid">The unique ID of the game object.</param>
        /// <param name="gameObject">The game object whose parent was changed.</param>
        /// <param name="newScene">The new scene of the game object.</param>
        /// <param name="newParent">The new parent of the game object.</param>
        public delegate void GameObjectParentChangedEventHandler(Guid guid, GameObject gameObject, Scene newScene, GameObject newParent);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.GameObjectIndexChanged"/> event.
        /// </summary>
        /// <param name="guid">The unique ID of the game object.</param>
        /// <param name="gameObject">The game object whose index was changed.</param>
        /// <param name="index">The sibling index of the game object in the hierarchy.</param>
        public delegate void GameObjectIndexChangedEventHandler(Guid guid, GameObject gameObject, int index);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.GameObjectPropertiesChanged"/> event.
        /// </summary>
        /// <param name="guid">The unique ID of the game object.</param>
        /// <param name="gameObject">The game object whose properties were changed.</param>
        /// <param name="properties">A serialized object containing the properties of the game object.</param>
        public delegate void GameObjectPropertiesChangedEventHandler(Guid guid, GameObject gameObject, SerializedObject properties);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.ComponentAdded"/> event.
        /// </summary>
        /// <param name="guid">The unique ID of the added component.</param>
        /// <param name="component">The component that was added.</param>
        public delegate void ComponentAddedEventHandler(Guid guid, Component component);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.ComponentDestroyed"/> event.
        /// </summary>
        /// <param name="guid">The unique ID of the destroyed component.</param>
        public delegate void ComponentDestroyedEventHandler(Guid guid);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.ComponentIndexChanged"/> event.
        /// </summary>
        /// <param name="guid">The unique ID of the component.</param>
        /// <param name="component">The component whose index was changed.</param>
        /// <param name="index">The index of the component in the component list of the game object.</param>
        public delegate void ComponentIndexChangedEventHandler(Guid guid, Component component, int index);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.ComponentPropertiesChanged"/> event.
        /// </summary>
        /// <param name="guid">The unique ID of the component.</param>
        /// <param name="component">The component whose properties were changed.</param>
        /// <param name="properties">A serialized object containing the properties of the component.</param>
        public delegate void ComponentPropertiesChangedEventHandler(Guid guid, Component component, SerializedObject properties);

        /// <summary>
        /// An event invoked when a new game object is added to a tracked scene.
        /// </summary>
        public event GameObjectAddedEventHandler GameObjectAdded;

        /// <summary>
        /// An event invoked when a game object from a tracked scene is destroyed.
        /// </summary>
        public event GameObjectDestroyedEventHandler GameObjectDestroyed;

        /// <summary>
        /// An event invoked when a game object is assigned a new parent.
        /// </summary>
        public event GameObjectParentChangedEventHandler GameObjectParentChanged;

        /// <summary>
        /// An event invoked when a game object is reordered in the transform hierarchy.
        /// </summary>
        public event GameObjectIndexChangedEventHandler GameObjectIndexChanged;

        /// <summary>
        /// An event invoked when a the properties of a game object are modified.
        /// </summary>
        public event GameObjectPropertiesChangedEventHandler GameObjectPropertiesChanged;

        /// <summary>
        /// An event invoked when a new component is added to a tracked scene.
        /// </summary>
        public event ComponentAddedEventHandler ComponentAdded;

        /// <summary>
        /// An event invoked when a component from a tracked scene is destroyed.
        /// </summary>
        public event ComponentDestroyedEventHandler ComponentDestroyed;

        /// <summary>
        /// An event invoked when a component is reordered.
        /// </summary>
        public event ComponentIndexChangedEventHandler ComponentIndexChanged;

        /// <summary>
        /// An event invoked when the properties of a component are modified.
        /// </summary>
        public event ComponentPropertiesChangedEventHandler ComponentPropertiesChanged;

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
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ObjectChangeEvents.changesPublished += OnChangesPublished;

            StartTrackingAllScenes();

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
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ObjectChangeEvents.changesPublished -= OnChangesPublished;

            // Copy values to allow modifying the source dictionary while iterating.
            foreach (var state in m_TrackedScenes.Values.ToArray())
            {
                state.Dispose();
            }

            m_TrackedScenes.Clear();
            m_TrackedGameObjects.Clear();
            m_TrackedComponents.Clear();

            m_ScenesToCheckForChanges.Clear();
            m_GameObjectsToCheckForChanges.Clear();

            m_AddedGameObjects.Clear();
            m_DestroyedGameObjects.Clear();
            m_ModifiedGameObjects.Clear();
            m_ReparentedGameObjects.Clear();
            m_ReorderedGameObjects.Clear();
            m_AddedComponents.Clear();
            m_DestroyedComponents.Clear();
            m_ModifiedComponents.Clear();
            m_ReorderedComponents.Clear();

            m_IsRunning = false;
        }

        /// <summary>
        /// Call every frame to check for changes to the scene.
        /// </summary>
        public void Update()
        {
            if (!m_IsRunning)
            {
                return;
            }

            try
            {
                Profiler.BeginSample($"{nameof(SceneChangeTracker)}.{nameof(Update)}()");

                // Time slicing is used to avoid taking a large amount of time in a single frame to check for scene updates.
                // This enables handling larger scenes with less stuttering, for a slight cost in overhead.
                var targetMaxUpdateTimeSliceTicks = (k_MaxUpdateTimeSlice * Stopwatch.Frequency) / (1000 * 1000);

                m_UpdateStopwatch.Restart();

                // When there are still buffered states to check for changes, they must be processed before the up-to-date
                // scene states can be buffered, or else some changes could be missed.
                // TODO: set a max limit for the rate of updates in case buffering state takes a long time
                if (m_ScenesToCheckForChanges.Count == 0 && m_GameObjectsToCheckForChanges.Count == 0)
                {
                    // Buffering the scene state must be completed in a single update, or else the state could be inconsistent.
                    BufferAllSceneStates(false);
                }

                // Look for changes in the buffered states until completed or the time slice is over.
                while (m_UpdateStopwatch.ElapsedTicks < targetMaxUpdateTimeSliceTicks && m_ScenesToCheckForChanges.TryDequeue(out var sceneState))
                {
                    FindSceneChanges(sceneState);
                }

                while (m_UpdateStopwatch.ElapsedTicks < targetMaxUpdateTimeSliceTicks && m_GameObjectsToCheckForChanges.TryDequeue(out var goState))
                {
                    FindGameObjectChanges(goState);
                }

                // When finished checking all buffered states for changes, report all the detected changes.
                if (m_ScenesToCheckForChanges.Count == 0 && m_GameObjectsToCheckForChanges.Count == 0)
                {
                    InvokeEvents();
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            StartTrackingScene(scene);
        }

        void OnSceneClosing(Scene scene, bool removingScene)
        {
            StopTrackingScene(scene);
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredEditMode:
                case PlayModeStateChange.EnteredPlayMode:
                    StartTrackingAllScenes();
                    break;
            }
        }

        void StartTrackingAllScenes()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                StartTrackingScene(SceneManager.GetSceneAt(i));
            }
        }

        void StartTrackingScene(Scene scene)
        {
            // The scene needs to be loaded to access the scene objects.
            if (scene.IsValid() && scene.isLoaded && !m_TrackedScenes.ContainsKey(scene))
            {
                var sceneState = new SceneState(scene, m_TrackedScenes);
                BufferSceneState(sceneState, true);
            }
        }

        void StopTrackingScene(Scene scene)
        {
            if (scene.IsValid() && m_TrackedScenes.TryGetValue(scene, out var sceneState))
            {
                sceneState.Dispose();
            }
        }

        void BufferAllSceneStates(bool loadingScene)
        {
            foreach (var scene in m_TrackedScenes)
            {
                BufferSceneState(scene.Value, loadingScene);
            }
        }

        void BufferSceneState(SceneState sceneState, bool loadingScene)
        {
            try
            {
                Profiler.BeginSample($"{nameof(SceneChangeTracker)}.{nameof(BufferSceneState)}()");

                // Mark this scene as needing to be evaluated for changes to the buffered state.
                if (!loadingScene)
                {
                    m_ScenesToCheckForChanges.Enqueue(sceneState);
                }

                // Buffer the scene state.
                sceneState.Update();

                sceneState.Current.Roots.Clear();
                sceneState.Instance.GetRootGameObjects(s_TempGameObjects);

                foreach (var root in s_TempGameObjects)
                {
                    var rootState = BufferGameObjectState(sceneState, null, root, loadingScene);
                    sceneState.Current.Roots.Add(rootState);
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        GameObjectState BufferGameObjectState(SceneState sceneState, GameObjectState parentState, GameObject gameObject, bool loadingScene)
        {
            if (!m_TrackedGameObjects.TryGetValue(gameObject.GetInstanceID(), out var goState))
            {
                goState = new GameObjectState(gameObject, !loadingScene, m_TrackedGameObjects);
            }

            // Mark this game object as needing to be evaluated for changes to the buffered state.
            if (!loadingScene)
            {
                m_GameObjectsToCheckForChanges.Enqueue(goState);
            }

            // Buffer the game object state.
            var transform = goState.Instance.transform;

            goState.Update();
            goState.Current.UpdateProperties();
            goState.Current.Scene = sceneState;
            goState.Current.Parent = parentState;
            goState.Current.Index = transform.GetSiblingIndex();

            // Buffer the state of all components on the game object.
            goState.Current.Components.Clear();
            goState.Instance.GetComponents(s_TempComponents);

            for (var i = 0; i < s_TempComponents.Count; i++)
            {
                var component = s_TempComponents[i];

                // Skip over invalid components (ex. when a script is missing).
                if (component == null)
                {
                    continue;
                }

                if (!m_TrackedComponents.TryGetValue(component.GetInstanceID(), out var compState))
                {
                    compState = new ComponentState(goState, component, !loadingScene, m_TrackedComponents);
                }

                compState.Update();
                compState.Current.UpdateProperties();
                compState.Current.Index = i;

                goState.Current.Components.Add(compState);
            }

            // Buffer the state of all child game objects.
            goState.Current.Children.Clear();

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                var childState = BufferGameObjectState(sceneState, goState, child, loadingScene);
                goState.Current.Children.Add(childState);
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


#if TEST
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
                            stream.GetCreateGameObjectHierarchyEvent(i, out var change);

                            if (m_TrackedScenes.TryGetValue(change.scene, out var sceneState))
                            {
                                var gameObject = EditorUtility.InstanceIDToObject(change.instanceId) as GameObject;
                                CheckGameObjectStructural(sceneState, gameObject, true);
                            }
                            break;
                        }
                        case ObjectChangeKind.DestroyGameObjectHierarchy:
                        {
                            stream.GetDestroyGameObjectHierarchyEvent(i, out var change);

                            if (m_TrackedScenes.TryGetValue(change.scene, out var sceneState))
                            {
                                var parent = EditorUtility.InstanceIDToObject(change.parentInstanceId) as GameObject;

                                if (parent != null)
                                {
                                    CheckGameObjectStructural(sceneState, parent, true);
                                }
                                else
                                {
                                    CheckScene(sceneState);
                                }
                            }
                            break;
                        }
                        case ObjectChangeKind.ChangeGameObjectParent:
                        {
                            stream.GetChangeGameObjectParentEvent(i, out var change);

                            if (m_TrackedScenes.TryGetValue(change.newScene, out var newSceneState))
                            {
                                var newParent = EditorUtility.InstanceIDToObject(change.newParentInstanceId) as GameObject;

                                if (newParent != null)
                                {
                                    CheckGameObjectStructural(newSceneState, newParent, true);
                                }
                                else
                                {
                                    CheckScene(newSceneState);
                                }
                            }
                            else if (m_TrackedScenes.TryGetValue(change.previousScene, out var prevSceneState))
                            {
                                var prevParent = EditorUtility.InstanceIDToObject(change.previousParentInstanceId) as GameObject;

                                if (prevParent != null)
                                {
                                    CheckGameObjectStructural(prevSceneState, prevParent, true);
                                }
                                else
                                {
                                    CheckScene(prevSceneState);
                                }
                            }
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
                                        CheckComponent(component);
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
#endif
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        void FindSceneChanges(SceneState sceneState)
        {
            // Detect removed scene roots.
            foreach (var rootState in sceneState.Previous.Roots)
            {
                if (rootState.Instance == null)
                {
                    m_DestroyedGameObjects.Enqueue(rootState);
                    rootState.Dispose();
                }
            }
        }

        void FindGameObjectChanges(GameObjectState goState)
        {
            // Detect added game objects.
            if (goState.IsNew)
            {
                m_AddedGameObjects.Enqueue(goState);
                m_ModifiedGameObjects.Enqueue(goState);
                goState.IsNew = false;
            }

            // Detect parent changes and reordered game objects.
            if (goState.Previous.Parent != goState.Current.Parent || (goState.Current.Parent == null && goState.Previous.Scene != goState.Current.Scene))
            {
                m_ReparentedGameObjects.Enqueue(goState);
                m_ReorderedGameObjects.Enqueue(goState);
            }
            else if (goState.Previous.Index != goState.Current.Index)
            {
                m_ReorderedGameObjects.Enqueue(goState);
            }

            // Detect game object property modifications.
            if (goState.Current.PropertiesChanged && !ArePropertiesEqual(goState.Previous.Properties, goState.Current.Properties))
            {
                m_ModifiedGameObjects.Enqueue(goState);
            }

            // Detect removed components.
            foreach (var compState in goState.Previous.Components)
            {
                if (compState.Instance == null)
                {
                    m_DestroyedComponents.Enqueue(compState);
                    compState.Dispose();
                }
            }

            foreach (var compState in goState.Current.Components)
            {
                // Detect added components.
                if (compState.IsNew)
                {
                    // Skip adding transforms since they are always added by default.
                    if (compState.Type != typeof(Transform))
                    {
                        m_AddedComponents.Enqueue(compState);
                    }

                    m_ModifiedComponents.Enqueue(compState);

                    compState.IsNew = false;
                }

                // Detect reordered components.
                if (compState.Previous.Index != compState.Current.Index)
                {
                    m_ReorderedComponents.Enqueue(compState);
                }

                // Detect component property modifications.
                if (compState.Current.PropertiesChanged && !ArePropertiesEqual(compState.Previous.Properties, compState.Current.Properties))
                {
                    m_ModifiedComponents.Enqueue(compState);
                }
            }

            // Detect removed children.
            foreach (var childState in goState.Previous.Children)
            {
                if (childState.Instance == null)
                {
                    m_DestroyedGameObjects.Enqueue(childState);
                    childState.Dispose();
                }
            }
        }

        void InvokeEvents()
        {
            try
            {
                Profiler.BeginSample($"{nameof(SceneChangeTracker)}.{nameof(InvokeEvents)}()");

                // Care must be taken ensure that the stream of changes can be evaluated in order without problems.
                // For example, we shouldn't try to update component properties before adding new game objects, as the
                // properties could reference a game object that hasn't been created yet.

                while (m_DestroyedGameObjects.TryDequeue(out var goState))
                {
                    var parentState = goState.Current.Parent;

                    if (parentState != null)
                    {
                        var parent = parentState.Instance;

                        if (parent != null)
                        {
                            try
                            {
                                GameObjectDestroyed?.Invoke(goState.Guid);
                                Debug.Log($"Change: Destroyed game object {goState.Guid}.");
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e);
                            }
                        }
                    }
                    else
                    {
                        var sceneState = goState.Current.Scene;
                        var scene = sceneState.Instance;

                        if (scene.IsValid() && scene.isLoaded)
                        {
                            try
                            {
                                GameObjectDestroyed?.Invoke(goState.Guid);
                                Debug.Log($"Change: Removed root of {scene.name} at index {goState.Current.Index}.");
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e);
                            }
                        }
                    }
                }

                while (m_DestroyedComponents.TryDequeue(out var compState))
                {
                    var gameObject = compState.GameObject.Instance;

                    if (gameObject != null)
                    {
                        try
                        {
                            ComponentDestroyed?.Invoke(compState.Guid);
                            Debug.Log($"Change: Removed component on {gameObject.name} at index {compState.Current.Index}.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }

                while (m_AddedGameObjects.TryDequeue(out var goState))
                {
                    if (goState.Instance != null)
                    {
                        try
                        {
                            GameObjectAdded?.Invoke(goState.Guid, goState.Instance);
                            Debug.Log($"Change: Added game object {goState.Instance.name}.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }

                while (m_ReparentedGameObjects.TryDequeue(out var goState))
                {
                    var sceneState = goState.Current.Scene;
                    var scene = sceneState.Instance;

                    var parentState = goState.Current.Parent;
                    var parent = parentState?.Instance;

                    if (goState.Instance != null)
                    {
                        try
                        {
                            GameObjectParentChanged?.Invoke(goState.Guid, goState.Instance, scene, parent);
                            Debug.Log($"Change: Set parent {scene.name} {(parent != null ? parent.name : string.Empty)} {goState.Instance.name}.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }

                while (m_ReorderedGameObjects.TryDequeue(out var goState))
                {
                    if (goState.Instance != null)
                    {
                        try
                        {
                            GameObjectIndexChanged?.Invoke(goState.Guid, goState.Instance, goState.Current.Index);
                            Debug.Log($"Change: Reordered game object {goState.Instance.name} to index {goState.Current.Index}.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }

                while (m_AddedComponents.TryDequeue(out var compState))
                {
                    if (compState.Instance != null)
                    {
                        try
                        {
                            ComponentAdded?.Invoke(compState.Guid, compState.Instance);
                            Debug.Log($"Change: Added component {compState.Instance.GetType()}.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }

                while (m_ReorderedComponents.TryDequeue(out var compState))
                {
                    if (compState.Instance != null)
                    {
                        try
                        {
                            ComponentIndexChanged?.Invoke(compState.Guid, compState.Instance, compState.Current.Index);
                            Debug.Log($"Change: Reordered component {compState.Instance.name} to index {compState.Current.Index}.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }

                while (m_ModifiedGameObjects.TryDequeue(out var goState))
                {
                    if (goState.Instance != null)
                    {
                        try
                        {
                            GameObjectPropertiesChanged?.Invoke(goState.Guid, goState.Instance, goState.Current.Properties);
                            Debug.Log($"Change: Modified game object {goState.Instance}.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }

                while (m_ModifiedComponents.TryDequeue(out var compState))
                {
                    if (compState.Instance != null)
                    {
                        try
                        {
                            ComponentPropertiesChanged?.Invoke(compState.Guid, compState.Instance, compState.Current.Properties);
                            Debug.Log($"Change: Modified component {compState.Instance.GetType().Name} {compState.Instance.gameObject}.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        static bool ArePropertiesEqual(SerializedObject previous, SerializedObject current)
        {
            var prevItr = previous.GetIterator();
            var currItr = current.GetIterator();
            var prevValid = prevItr.Next(true);
            var currValid = currItr.Next(true);

            while (prevValid && currValid)
            {
                if (!SerializedProperty.DataEquals(currItr, prevItr))
                {
                    return false;
                }

                prevValid = prevItr.Next(false);
                currValid = currItr.Next(false);
            }

            return true;
        }
    }
}
