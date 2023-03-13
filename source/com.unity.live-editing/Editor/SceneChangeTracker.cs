using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Unity.LiveEditing.Editor
{
    using Debug = UnityEngine.Debug;
    using UnityObject = UnityEngine.Object;

    /// <summary>
    /// A class used to detect edits made by the user that change scene content.
    /// </summary>
    class SceneChangeTracker : IDisposable
    {
        /*
         * Change detection is achieved by two means:
         *
         * 1) The ObjectChangeEvents class, which gives access to the stream of changes registered with the Undo system.
         * This is the preferred approach in terms of performance, and is able to isolate user-made scene changes
         * while in Play Mode. However, it fails to detect all types of changes, notably ones made by scripts
         * and editor tools that don't properly use the Undo class.
         *
         * 2) Polling the scene (periodically iterating through all game objects and components in open scenes). This
         * is able to catch all changes, but is hard on performance and is not useful in Play Mode, since it cannot
         * isolate user-made scene changes. To reduce the performance impact, the polling operation is spread out over
         * multiple frames if needed.
         *
         * By using both techniques in a hybrid approach, we can strike a good balance between correctness and performance.
         *
         * In order to precisely detect changes, both the previous scene state and the current scene state must be compared
         * for differences. To do this, we buffer two copies of the scene state in a custom representation in addition to
         * the actual scenes, effectively triple buffering the scene state. Therefore, the following steps are taken to
         * find changes:
         *
         * 1) An undo event is received identifying objects that have changed, or it is time to poll the scene again.
         * 2) The relevant scene state is buffered. This may include the entire scene or a subset of its game objects.
         *    This operation should be completed in a single frame so that the buffered scene state is self consistent,
         *    so this operation should be kept as fast as possible.
         * 3) The previous and current buffered states are compared for any game objects that were buffered. When differences
         *    are found, the change is recorded in a queue corresponding to the type of change. This operation can be done over
         *    multiple frames, and the actual scene can continue to be modified without interfering with this step.
         * 4) The queued changes are presented using events.
         *
         * These steps must be completed in order from start to finish before starting to process new changes, or else
         * changes might be skipped.
         */

        internal abstract class BaseState<TObject, TKey, TData, TState> : IDisposable
            where TData : IDisposable
            where TState : BaseState<TObject, TKey, TData, TState>
        {
            public TObject Instance { get; }
            public TData Previous { get; protected set; }
            public TData Current { get; protected set; }
            public bool IsNew { get; set; }

            Dictionary<TKey, TState> m_TrackedObjects;
            TKey m_Key;

            protected BaseState(TObject instance, bool isNew, Dictionary<TKey, TState> trackedObjects, TKey key)
            {
                Instance = instance;
                IsNew = isNew;

                m_Key = key;
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
                (Previous, Current) = (Current, Previous);
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

            public int InstanceId { get; }

            protected UnityObjectState(TObject instance, bool isNew, Dictionary<int, TState> trackedObjects)
                : base(instance, isNew, trackedObjects, instance.GetInstanceID())
            {
                InstanceId = instance.GetInstanceID();
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
                : base(instance, false, trackedObjects, instance)
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

            public ComponentState(GameObjectState goState, Component instance, bool isNew, Dictionary<int, ComponentState> trackedObjects)
                : base(instance, isNew, trackedObjects)
            {
                Type = instance.GetType();
                GameObject = goState;
                Previous = new Data(instance);
                Current = new Data(instance);
            }
        }

        /// <summary>
        /// A union struct that is used to store different types of undo change events.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        struct ChangeEvent
        {
            public struct ChangeScene
            {
                public Scene scene;
            }

            public struct CreateGameObjectHierarchy
            {
                public int instanceId;
                public Scene scene;
            }

            public struct DestroyGameObjectHierarchy
            {
                public int instanceId;
                public int parentInstanceId;
                public Scene scene;
            }

            public struct ChangeGameObjectParent
            {
                public int instanceId;
                public int previousParentInstanceId;
                public int newParentInstanceId;
                public Scene previousScene;
                public Scene newScene;
            }

            public struct ChangeGameObjectStructure
            {
                public int instanceId;
                public Scene scene;
            }

            public struct ChangeGameObjectStructureHierarchy
            {
                public int instanceId;
                public Scene scene;
            }

            public struct ChangeGameObjectOrComponentProperties
            {
                public int instanceId;
                public Scene scene;
            }

            public struct UpdatePrefabInstances
            {
                public int instanceId;
                public Scene scene;
            }

            public struct ChangeChildrenOrder
            {
                public int instanceId;
                public Scene scene;
            }

            [FieldOffset(0)] public ObjectChangeKind type;
            [FieldOffset(4)] public ChangeScene changeScene;
            [FieldOffset(4)] public CreateGameObjectHierarchy createGameObjectHierarchy;
            [FieldOffset(4)] public DestroyGameObjectHierarchy destroyGameObjectHierarchy;
            [FieldOffset(4)] public ChangeGameObjectParent changeGameObjectParent;
            [FieldOffset(4)] public ChangeGameObjectStructure changeGameObjectStructure;
            [FieldOffset(4)] public ChangeGameObjectStructureHierarchy changeGameObjectStructureHierarchy;
            [FieldOffset(4)] public ChangeGameObjectOrComponentProperties changeGameObjectOrComponentProperties;
            [FieldOffset(4)] public UpdatePrefabInstances updatePrefabInstances;
            [FieldOffset(4)] public ChangeChildrenOrder changeChildrenOrder;
        }

        static SceneChangeTracker s_Instance;
        static readonly List<GameObject> s_TempGameObjects = new List<GameObject>();
        static readonly List<Component> s_TempComponents = new List<Component>();

        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static SceneChangeTracker Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = new SceneChangeTracker();
                    EditorApplication.update += s_Instance.Update;
                }
                return s_Instance;
            }
        }

        bool m_IsRunning;
        readonly Stopwatch m_UpdateStopwatch = new Stopwatch();
        readonly Stopwatch m_TimeSliceStopwatch = new Stopwatch();

        internal readonly Dictionary<Scene, SceneState> m_TrackedScenes = new Dictionary<Scene, SceneState>();
        internal readonly Dictionary<int, GameObjectState> m_TrackedGameObjects = new Dictionary<int, GameObjectState>();
        internal readonly Dictionary<int, ComponentState> m_TrackedComponents = new Dictionary<int, ComponentState>();

        readonly Queue<ChangeEvent> m_ChangeEvents = new Queue<ChangeEvent>();

        readonly Queue<SceneState> m_ScenesToCheckForChanges = new Queue<SceneState>();
        readonly Queue<GameObjectState> m_GameObjectsToCheckForChanges = new Queue<GameObjectState>();

        readonly Queue<GameObjectState> m_AddedGameObjects = new Queue<GameObjectState>();
        readonly Queue<GameObjectState> m_DestroyedGameObjects = new Queue<GameObjectState>();
        readonly Queue<GameObjectState> m_ReparentedGameObjects = new Queue<GameObjectState>();
        readonly Queue<GameObjectState> m_ReorderedGameObjects = new Queue<GameObjectState>();
        readonly Queue<(GameObjectState, string)> m_ModifiedGameObjects = new Queue<(GameObjectState, string)>();
        readonly Queue<ComponentState> m_AddedComponents = new Queue<ComponentState>();
        readonly Queue<ComponentState> m_DestroyedComponents = new Queue<ComponentState>();
        readonly Queue<ComponentState> m_ReorderedComponents = new Queue<ComponentState>();
        readonly Queue<(ComponentState, string)> m_ModifiedComponents = new Queue<(ComponentState, string)>();

        /// <summary>
        /// The maximum time that the scene tracking will try to use in a single frame.
        /// </summary>
        /// <remarks>
        /// Lowering this value may increase performance at the cost of latency.
        /// </remarks>
        public TimeSpan MaxUpdateTimeSlice { get; set; } = TimeSpan.FromMilliseconds(5);

        /// <summary>
        /// Periodically iterate through the scene to find changes not registered through the undo system.
        /// </summary>
        /// <remarks>
        /// Try enabling this option if relevant changes to the scene are not being detected. Scene polling only
        /// works while in Edit Mode. This may be performance intensive for large scenes.
        /// </remarks>
        public bool EnablePolling { get; set; } = true;

        /// <summary>
        /// The time to wait after polling the scene for changes to poll again.
        /// </summary>
        /// <remarks>
        /// This value is only used when <see cref="EnablePolling"/> is set to <see langword="true"/>.
        /// Increasing this value can help reduce the performance impact of scene polling at the cost
        /// of latency.
        /// </remarks>
        public TimeSpan PollingPeriod { get; set; } = TimeSpan.FromMilliseconds(1000);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.GameObjectAdded"/> event.
        /// </summary>
        /// <param name="instanceID">The instance ID of the added game object.</param>
        /// <param name="gameObject">The game object that was added.</param>
        public delegate void GameObjectAddedEventHandler(int instanceID, GameObject gameObject);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.GameObjectDestroyed"/> event.
        /// </summary>
        /// <param name="instanceID">The instance ID of the destroyed game object.</param>
        public delegate void GameObjectDestroyedEventHandler(int instanceID);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.GameObjectParentChanged"/> event.
        /// </summary>
        /// <param name="instanceID">The instance ID of the game object.</param>
        /// <param name="gameObject">The game object whose parent was changed.</param>
        /// <param name="newScene">The new scene of the game object.</param>
        /// <param name="newParent">The new parent of the game object.</param>
        public delegate void GameObjectParentChangedEventHandler(int instanceID, GameObject gameObject, Scene newScene, GameObject newParent);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.GameObjectIndexChanged"/> event.
        /// </summary>
        /// <param name="instanceID">The instance ID of the game object.</param>
        /// <param name="gameObject">The game object whose index was changed.</param>
        /// <param name="index">The sibling index of the game object in the hierarchy.</param>
        public delegate void GameObjectIndexChangedEventHandler(int instanceID, GameObject gameObject, int index);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.GameObjectPropertyChanged"/> event.
        /// </summary>
        /// <param name="instanceID">The instance ID of the game object.</param>
        /// <param name="gameObject">The game object whose property were changed.</param>
        /// <param name="property">The serialized property that has changed.</param>
        public delegate void GameObjectPropertyChangedEventHandler(int instanceID, GameObject gameObject, SerializedProperty property);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.ComponentAdded"/> event.
        /// </summary>
        /// <param name="instanceID">The instance ID of the added component.</param>
        /// <param name="component">The component that was added.</param>
        public delegate void ComponentAddedEventHandler(int instanceID, Component component);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.ComponentDestroyed"/> event.
        /// </summary>
        /// <param name="instanceID">The instance ID of the destroyed component.</param>
        public delegate void ComponentDestroyedEventHandler(int instanceID);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.ComponentIndexChanged"/> event.
        /// </summary>
        /// <param name="instanceID">The instance ID of the component.</param>
        /// <param name="component">The component whose index was changed.</param>
        /// <param name="index">The index of the component in the component list of the game object.</param>
        public delegate void ComponentIndexChangedEventHandler(int instanceID, Component component, int index);

        /// <summary>
        /// Represents a method that handles the <see cref="SceneChangeTracker.ComponentPropertyChanged"/> event.
        /// </summary>
        /// <param name="instanceID">The instance ID of the component.</param>
        /// <param name="component">The component whose property were changed.</param>
        /// <param name="property">The serialized property that has changed.</param>
        public delegate void ComponentPropertyChangedEventHandler(int instanceID, Component component, SerializedProperty property);

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
        /// An event invoked when a property of a game object is modified.
        /// </summary>
        public event GameObjectPropertyChangedEventHandler GameObjectPropertyChanged;

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
        /// An event invoked when a property of a component is modified.
        /// </summary>
        public event ComponentPropertyChangedEventHandler ComponentPropertyChanged;

        /// <summary>
        /// Creates a new <see cref="SceneChangeTracker"/> instance.
        /// </summary>
        internal SceneChangeTracker()
        {
        }

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

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorSceneManager.sceneManagerSetupRestored += SceneSetupRestored;
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

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ObjectChangeEvents.changesPublished -= OnChangesPublished;

            // Copy values to allow modifying the source dictionary while iterating.
            foreach (var state in m_TrackedScenes.Values.ToArray())
            {
                state.Dispose();
            }

            m_UpdateStopwatch.Reset();
            m_TimeSliceStopwatch.Reset();

            m_TrackedScenes.Clear();
            m_TrackedGameObjects.Clear();
            m_TrackedComponents.Clear();

            m_ChangeEvents.Clear();

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
        /// Call every frame to check for recent changes.
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
                var timeSliceTicks = (MaxUpdateTimeSlice.Ticks * Stopwatch.Frequency) / TimeSpan.TicksPerSecond;
                
                m_TimeSliceStopwatch.Restart();

                // Periodically iterate through the scene to find any undetected changes.
                if (EnablePolling && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // The polling rate is limited to prevent using excessive resources.
                    // When there are still buffered states to check for changes, they must be processed before the up-to-date
                    // scene states can be buffered, or else some changes could be missed.
                    if ((!m_UpdateStopwatch.IsRunning || m_UpdateStopwatch.ElapsedMilliseconds >= PollingPeriod.Milliseconds) &&
                        m_ScenesToCheckForChanges.Count == 0 && m_GameObjectsToCheckForChanges.Count == 0)
                    {
                        // Buffering the scene state must be completed in a single update, or else the state could be inconsistent.
                        BufferAllSceneStates(false);

                        m_UpdateStopwatch.Restart();
                    }

                    // Look for changes in the buffered states until completed or the time slice is over.
                    while (m_TimeSliceStopwatch.ElapsedTicks < timeSliceTicks && m_ScenesToCheckForChanges.TryDequeue(out var sceneState))
                    {
                        FindSceneChanges(sceneState);
                    }

                    while (m_TimeSliceStopwatch.ElapsedTicks < timeSliceTicks && m_GameObjectsToCheckForChanges.TryDequeue(out var goState))
                    {
                        FindGameObjectChanges(goState);
                    }

                    // When finished checking all buffered states for changes, report all the detected changes.
                    if (m_ScenesToCheckForChanges.Count == 0 && m_GameObjectsToCheckForChanges.Count == 0)
                    {
                        InvokeEvents();
                    }
                }

                // When not in the middle of polling a scene, go through recent undo system events to find changes.
                if (m_ScenesToCheckForChanges.Count == 0 && m_GameObjectsToCheckForChanges.Count == 0)
                {
                    while (m_TimeSliceStopwatch.ElapsedTicks < timeSliceTicks && m_ChangeEvents.TryDequeue(out var changeEvent))
                    {
                        ProcessChangeEvent(changeEvent);
                    }
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartTrackingScene(scene);
        }

        void OnSceneUnloaded(Scene scene)
        {
            StopTrackingScene(scene);
        }

        void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            StartTrackingScene(scene);
        }

        void OnSceneClosing(Scene scene, bool removingScene)
        {
            StopTrackingScene(scene);
        }

        void SceneSetupRestored(Scene[] scenes)
        {
            StartTrackingAllScenes();
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

        void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            try
            {
                Profiler.BeginSample($"{nameof(SceneChangeTracker)}.{nameof(OnChangesPublished)}()");

                if (!stream.isCreated)
                {
                    return;
                }

                // Buffer the change events into a queue for processing later.
                for (var i = 0; i < stream.length; i++)
                {
                    var type = stream.GetEventType(i);

                    switch (type)
                    {
                        case ObjectChangeKind.ChangeScene:
                        {
                            stream.GetChangeSceneEvent(i, out var change);

                            m_ChangeEvents.Enqueue(new ChangeEvent
                            {
                                type = type,
                                changeScene =
                                {
                                    scene = change.scene,
                                },
                            });
                            break;
                        }
                        case ObjectChangeKind.CreateGameObjectHierarchy:
                        {
                            stream.GetCreateGameObjectHierarchyEvent(i, out var change);

                            m_ChangeEvents.Enqueue(new ChangeEvent
                            {
                                type = type,
                                createGameObjectHierarchy =
                                {
                                    instanceId = change.instanceId,
                                    scene = change.scene,
                                },
                            });
                            break;
                        }
                        case ObjectChangeKind.DestroyGameObjectHierarchy:
                        {
                            stream.GetDestroyGameObjectHierarchyEvent(i, out var change);

                            m_ChangeEvents.Enqueue(new ChangeEvent
                            {
                                type = type,
                                destroyGameObjectHierarchy =
                                {
                                    instanceId = change.instanceId,
                                    parentInstanceId = change.parentInstanceId,
                                    scene = change.scene,
                                },
                            });
                            break;
                        }
                        case ObjectChangeKind.ChangeGameObjectParent:
                        {
                            stream.GetChangeGameObjectParentEvent(i, out var change);

                            m_ChangeEvents.Enqueue(new ChangeEvent
                            {
                                type = type,
                                changeGameObjectParent =
                                {
                                    instanceId = change.instanceId,
                                    previousParentInstanceId = change.previousParentInstanceId,
                                    newParentInstanceId = change.newParentInstanceId,
                                    previousScene = change.previousScene,
                                    newScene = change.newScene,
                                },
                            });
                            break;
                        }
                        case ObjectChangeKind.ChangeGameObjectStructure:
                        {
                            stream.GetChangeGameObjectStructureEvent(i, out var change);

                            m_ChangeEvents.Enqueue(new ChangeEvent
                            {
                                type = type,
                                changeGameObjectStructure =
                                {
                                    instanceId = change.instanceId,
                                    scene = change.scene,
                                },
                            });
                            break;
                        }
                        case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                        {
                            stream.GetChangeGameObjectStructureHierarchyEvent(i, out var change);

                            m_ChangeEvents.Enqueue(new ChangeEvent
                            {
                                type = type,
                                changeGameObjectStructureHierarchy =
                                {
                                    instanceId = change.instanceId,
                                    scene = change.scene,
                                },
                            });
                            break;
                        }
                        case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        {
                            stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var change);

                            m_ChangeEvents.Enqueue(new ChangeEvent
                            {
                                type = type,
                                changeGameObjectOrComponentProperties =
                                {
                                    instanceId = change.instanceId,
                                    scene = change.scene,
                                },
                            });
                            break;
                        }
                        case ObjectChangeKind.UpdatePrefabInstances:
                        {
                            stream.GetUpdatePrefabInstancesEvent(i, out var change);

                            foreach (var instanceId in change.instanceIds)
                            {
                                m_ChangeEvents.Enqueue(new ChangeEvent
                                {
                                    type = type,
                                    updatePrefabInstances =
                                    {
                                        instanceId = instanceId,
                                        scene = change.scene,
                                    },
                                });
                            }
                            break;
                        }
                        case ObjectChangeKind.ChangeChildrenOrder:
                        {
                            stream.GetChangeChildrenOrderEvent(i, out var change);

                            m_ChangeEvents.Enqueue(new ChangeEvent
                            {
                                type = type,
                                changeChildrenOrder =
                                {
                                    instanceId = change.instanceId,
                                    scene = change.scene,
                                },
                            });
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

        void ProcessChangeEvent(ChangeEvent evt)
        {
            try
            {
                Profiler.BeginSample($"{nameof(SceneChangeTracker)}.{nameof(ProcessChangeEvent)}()");

                switch (evt.type)
                {
                    case ObjectChangeKind.ChangeScene:
                    {
                        var change = evt.changeScene;
                        ProcessSceneChangeEvent(change.scene);
                        break;
                    }
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    {
                        var change = evt.createGameObjectHierarchy;
                        ProcessGameObjectAddedEvent(change.instanceId);
                        break;
                    }
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                    {
                        var change = evt.destroyGameObjectHierarchy;
                        ProcessGameObjectDestroyedEvent(change.parentInstanceId, change.scene);
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectParent:
                    {
                        var change = evt.changeGameObjectParent;
                        ProcessGameObjectParentChangedEvent(
                            change.instanceId,
                            change.previousParentInstanceId,
                            change.newParentInstanceId,
                            change.previousScene,
                            change.newScene
                        );
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    {
                        var change = evt.changeGameObjectStructure;
                        ProcessGameObjectChangeEvent(change.instanceId, 0);
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    {
                        var change = evt.changeGameObjectStructureHierarchy;
                        ProcessGameObjectChangeEvent(change.instanceId, int.MaxValue);
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    {
                        var change = evt.changeGameObjectOrComponentProperties;
                        var obj = EditorUtility.InstanceIDToObject(change.instanceId);

                        switch (obj)
                        {
                            case GameObject:
                                ProcessGameObjectChangeEvent(change.instanceId, 0);
                                break;
                            case Component:
                                ProcessComponentChangeEvent(change.instanceId);
                                break;
                        }
                        break;
                    }
                    case ObjectChangeKind.UpdatePrefabInstances:
                    {
                        var change = evt.updatePrefabInstances;
                        ProcessGameObjectChangeEvent(change.instanceId, int.MaxValue);
                        break;
                    }
                    case ObjectChangeKind.ChangeChildrenOrder:
                    {
                        var change = evt.changeChildrenOrder;
                        ProcessGameObjectChangeEvent(change.instanceId, 1);
                        break;
                    }
                    default:
                    {
                        Debug.LogError($"Unsupported change type: \"{evt.type}\".");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        void ProcessSceneChangeEvent(Scene scene)
        {
            if (!m_TrackedScenes.TryGetValue(scene, out var sceneState))
            {
                return;
            }

            BufferSceneState(sceneState, int.MaxValue);

            FindChangesInBufferedState();
        }

        void ProcessGameObjectAddedEvent(int instanceId)
        {
            var gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;

            if (gameObject == null)
            {
                return;
            }

            var parent = gameObject.transform.parent;

            if (parent != null)
            {
                var parentInstanceId = parent.gameObject.GetInstanceID();

                if (!m_TrackedGameObjects.TryGetValue(parentInstanceId, out var parentState))
                {
                    ProcessGameObjectAddedEvent(parentInstanceId);
                    return;
                }

                BufferGameObjectState(parentState.Current.Scene, parentState, gameObject, 1);
            }
            else if (m_TrackedScenes.TryGetValue(gameObject.scene, out var sceneState))
            {
                BufferSceneState(sceneState, 1);
            }

            FindChangesInBufferedState();
        }

        void ProcessGameObjectDestroyedEvent(int parentInstanceId, Scene scene)
        {
            if (m_TrackedGameObjects.TryGetValue(parentInstanceId, out var parentState))
            {
                var parent = parentState.Instance;

                if (parent != null)
                {
                    BufferGameObjectState(parentState.Current.Scene, parentState.Current.Parent, parent, 1);
                }
            }
            else if (m_TrackedScenes.TryGetValue(scene, out var sceneState))
            {
                BufferSceneState(sceneState, 1);
            }

            FindChangesInBufferedState();
        }

        void ProcessGameObjectParentChangedEvent(
            int instanceId,
            int previousParentInstanceId,
            int newParentInstanceId,
            Scene previousScene,
            Scene newScene
        )
        {
            var gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;

            if (gameObject == null)
            {
                return;
            }

            if (m_TrackedGameObjects.TryGetValue(previousParentInstanceId, out var prevParentState))
            {
                var parent = prevParentState.Instance;

                if (parent != null)
                {
                    BufferGameObjectState(prevParentState.Current.Scene, prevParentState.Current.Parent, parent, 1);
                }
            }
            else if (m_TrackedScenes.TryGetValue(previousScene, out var prevSceneState))
            {
                BufferSceneState(prevSceneState, 1);
            }

            if (m_TrackedGameObjects.TryGetValue(newParentInstanceId, out var newParentState))
            {
                var parent = newParentState.Instance;

                if (parent != null)
                {
                    BufferGameObjectState(newParentState.Current.Scene, newParentState.Current.Parent, parent, 1);
                }
            }
            else if (m_TrackedScenes.TryGetValue(newScene, out var newSceneState))
            {
                BufferSceneState(newSceneState, 1);
            }

            FindChangesInBufferedState();
        }

        void ProcessGameObjectChangeEvent(int instanceId, int maxRecursionDepth)
        {
            var gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;

            if (gameObject == null || !m_TrackedGameObjects.TryGetValue(instanceId, out var goState))
            {
                return;
            }

            BufferGameObjectState(goState.Current.Scene, goState.Current.Parent, gameObject, maxRecursionDepth);

            FindChangesInBufferedState();
        }

        void ProcessComponentChangeEvent(int instanceId)
        {
            var component = EditorUtility.InstanceIDToObject(instanceId) as Component;

            if (component == null || !m_TrackedComponents.TryGetValue(instanceId, out var compState))
            {
                return;
            }

            // Check the entire game object for changes for simplicity. This could be optimized to check just
            // the specific component if needed.
            var goState = compState.GameObject;
            BufferGameObjectState(goState.Current.Scene, goState.Current.Parent, goState.Instance, 0);

            FindChangesInBufferedState();
        }

        void FindChangesInBufferedState()
        {
            while (m_ScenesToCheckForChanges.TryDequeue(out var sceneState))
            {
                FindSceneChanges(sceneState);
            }
            while (m_GameObjectsToCheckForChanges.TryDequeue(out var goState))
            {
                FindGameObjectChanges(goState);
            }

            InvokeEvents();
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
                BufferSceneState(sceneState, int.MaxValue, true);
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
                BufferSceneState(scene.Value, int.MaxValue, loadingScene);
            }
        }

        void BufferSceneState(SceneState sceneState, int maxRecursionDepth, bool loadingScene = false)
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
                    var rootState = BufferGameObjectState(sceneState, null, root, maxRecursionDepth - 1, loadingScene);
                    sceneState.Current.Roots.Add(rootState);
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        GameObjectState BufferGameObjectState(
            SceneState sceneState,
            GameObjectState parentState,
            GameObject gameObject,
            int maxRecursionDepth,
            bool loadingScene = false
        )
        {
            // Get the tracked state for the game object, or start tracking it if it is not yet tracked.
            if (!m_TrackedGameObjects.TryGetValue(gameObject.GetInstanceID(), out var goState))
            {
                goState = new GameObjectState(gameObject, !loadingScene, m_TrackedGameObjects);

                // When a new game object is found in the hierarchy, its children should be tracked too.
                maxRecursionDepth = int.MaxValue;
            }

            if (maxRecursionDepth < 0)
            {
                return goState;
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

            // Buffer the state of all child game objects, up to the permitted recursion depth.
            goState.Current.Children.Clear();

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                var childState = BufferGameObjectState(sceneState, goState, child, maxRecursionDepth - 1, loadingScene);
                goState.Current.Children.Add(childState);
            }

            return goState;
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
            if (goState.IsNew || goState.Current.PropertiesChanged)
            {
                foreach (var prop in FindPropertyChanges(goState.Previous.Properties, goState.Current.Properties, !goState.IsNew))
                {
                    m_ModifiedGameObjects.Enqueue((goState, prop));
                }
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
                }

                // Detect reordered components.
                if (compState.Previous.Index != compState.Current.Index)
                {
                    m_ReorderedComponents.Enqueue(compState);
                }

                // Detect component property modifications.
                if (compState.IsNew || compState.Current.PropertiesChanged)
                {
                    foreach (var prop in FindPropertyChanges(compState.Previous.Properties, compState.Current.Properties, !compState.IsNew))
                    {
                        m_ModifiedComponents.Enqueue((compState, prop));
                    }
                }

                compState.IsNew = false;
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

            goState.IsNew = false;
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
                    try
                    {
                        GameObjectDestroyed?.Invoke(goState.InstanceId);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                while (m_DestroyedComponents.TryDequeue(out var compState))
                {
                    var gameObject = compState.GameObject.Instance;

                    if (gameObject != null)
                    {
                        try
                        {
                            ComponentDestroyed?.Invoke(compState.InstanceId);
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
                            GameObjectAdded?.Invoke(goState.InstanceId, goState.Instance);
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
                    var parentState = goState.Current.Parent;

                    var gameObject = goState.Instance;
                    var scene = sceneState.Instance;
                    var parent = parentState?.Instance;

                    if (gameObject != null && (parentState == null || parent != null))
                    {
                        try
                        {
                            GameObjectParentChanged?.Invoke(goState.InstanceId, gameObject, scene, parent);
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
                            GameObjectIndexChanged?.Invoke(goState.InstanceId, goState.Instance, goState.Current.Index);
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
                            ComponentAdded?.Invoke(compState.InstanceId, compState.Instance);
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
                            ComponentIndexChanged?.Invoke(compState.InstanceId, compState.Instance, compState.Current.Index);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }

                while (m_ModifiedGameObjects.TryDequeue(out var value))
                {
                    var goState = value.Item1;

                    if (goState.Instance != null)
                    {
                        var property = goState.Current.Properties.FindProperty(value.Item2);

                        try
                        {
                            GameObjectPropertyChanged?.Invoke(goState.InstanceId, goState.Instance, property);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }

                while (m_ModifiedComponents.TryDequeue(out var value))
                {
                    var compState = value.Item1;

                    if (compState.Instance != null)
                    {
                        var property = compState.Current.Properties.FindProperty(value.Item2);

                        try
                        {
                            ComponentPropertyChanged?.Invoke(compState.InstanceId, compState.Instance, property);
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

        static IEnumerable<string> FindPropertyChanges(SerializedObject previous, SerializedObject current, bool onlyChanged = true)
        {
            var prevItr = onlyChanged ? previous.GetIterator() : null;
            var currItr = current.GetIterator();
            var prevValid = prevItr?.Next(true) ?? false;
            var currValid = currItr.Next(true);

            while (currValid)
            {
                foreach (var property in FindPropertyChanges(prevItr, currItr))
                {
                    yield return property;
                }

                if (prevValid)
                {
                    prevValid = prevItr.Next(false);
                }

                currValid = currItr.Next(false);
            }
        }

        static IEnumerable<string> FindPropertyChanges(SerializedProperty previousProp, SerializedProperty currentProp)
        {
            if (previousProp != null && SerializedProperty.DataEquals(previousProp, currentProp))
            {
                yield break;
            }

            switch (currentProp.propertyType)
            {
                case SerializedPropertyType.Generic:
                {
                    // Find the specific changes changes in the child properties by iterating over them.
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
                        foreach (var property in FindPropertyChanges(prevValid ? prevItr : null, currItr))
                        {
                            yield return property;
                        }

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
                    yield return currentProp.propertyPath;
                    break;
                }
            }
        }
    }
}
