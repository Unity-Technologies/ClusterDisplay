using System;
using System.Collections.Generic;
using System.Linq;
using Unity.ClusterDisplay.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Unity.ClusterDisplay.Scripting
{
    /// <summary>
    /// Apply this attribute to a class that handles replication
    /// logic for a specific component type.
    /// </summary>
    /// <remarks>
    /// The class must define a constructor with the signature (Guid, T) where T : Component.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    class SpecializedReplicatorAttribute : Attribute
    {
        public SpecializedReplicatorAttribute(Type componentType)
        {
            ComponentType = componentType;
        }

        public Type ComponentType { get; }
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
    [ExecuteAlways]
#endif
    [DisallowMultipleComponent]
    public class ClusterReplication : MonoBehaviour, ISerializationCallbackReceiver
    {
        static Dictionary<Type, Func<ReplicationTarget, IReplicator>> s_SpecializedReplicators = new();

        struct ReplicationUpdate { }

        [SerializeField]
        EditorLinkConfig m_EditorLinkConfig;

        /// <summary>
        /// For inspector and serialization only. Do not use for business logic. Use
        /// <see cref="Replicators"/> instead.
        /// </summary>
        [SerializeField]
        List<ReplicationTarget> m_ReplicationTargets;

        // For efficiency, all instances of ClusterReplication should share a single
        // EditorLink.
        static SharedReferenceManager<EditorLinkConfig, EditorLink> s_SharedEditorLink = new(CreateEditorLink);
        SharedReferenceManager<EditorLinkConfig, EditorLink>.SharedRef m_EditorLinkReference;

        ReplicatorMode m_Mode = ReplicatorMode.Disabled;

        internal Dictionary<ReplicationTarget, IReplicator> Replicators { get; private set; } = new();
        internal EditorLink EditorLink => m_EditorLinkReference;

        static EditorLink CreateEditorLink(EditorLinkConfig config)
        {
#if UNITY_EDITOR
            return new EditorLink(config, true);
#else
            return new EditorLink(config, false);
#endif
        }

        static ClusterReplication()
        {
            // Register all classes with the SpecializedReplicator attribute. These classes will be used
            // to handle custom replication logic for specific components.
            foreach (var (replicatorType, attribute) in AttributeUtility.GetAllTypes<SpecializedReplicatorAttribute>())
            {
                var ctor = replicatorType.GetConstructor(new[] {typeof(Guid), attribute.ComponentType});

                if (ctor != null)
                {
                    Debug.Log($"Found specialized replicator {replicatorType.Name} for {attribute.ComponentType}");
                    RegisterSpecializedReplicator(attribute.ComponentType,
                        target => ctor.Invoke(new object[] {target.Guid, target.Component}) as IReplicator);
                }
                else
                {
                    Debug.LogError("Specialized replicator must have a publicly accessible constructor with" +
                        " signature (Guid, Component)");
                }
            }
        }

        internal static void RegisterSpecializedReplicator(Type type, Func<ReplicationTarget, IReplicator> createFunc) =>
            s_SpecializedReplicators.Add(type, createFunc);

        internal static bool HasSpecializedReplicator(Type t) => s_SpecializedReplicators.ContainsKey(t);

        void OnEnable()
        {
            // NOTE: We assume that the cluster sync init logic has already been execute by this point.
            // This depends on ClusterDisplayManager executing its initialization during Awake()
            m_Mode = ServiceLocator.TryGet(out IClusterSyncState clusterSyncState)
                ? clusterSyncState.NodeRole switch
                {
                    NodeRole.Emitter => ReplicatorMode.Emitter,
                    NodeRole.Repeater => ReplicatorMode.Repeater,
                    _ => ReplicatorMode.Editor
                }
                : ReplicatorMode.Editor;

            ClusterDebug.Log($"[Cluster Replication] Enable - {m_Mode}");

            EnableReplicators();

            if (m_EditorLinkConfig != null)
                EnableEditorLink();

            ClusterSyncLooper.onInstanceDoPreFrame += OnPreFrame;
            PlayerLoopExtensions.RegisterUpdate<PostLateUpdate.DirectorLateUpdate, ReplicationUpdate>(UpdateReplicators);
        }

        void EnableEditorLink()
        {
            if (m_Mode is not (ReplicatorMode.Editor or ReplicatorMode.Emitter)) return;

            Debug.Assert(m_EditorLinkConfig != null);
            Debug.Assert(m_EditorLinkReference == null);
            m_EditorLinkReference = s_SharedEditorLink.GetReference(m_EditorLinkConfig);

            ClusterDebug.Log("[Cluster Replication] Enabling editor link");

            foreach (var (_, replicator) in Replicators)
            {
                replicator.EditorLink = m_EditorLinkReference;
            }
        }

        void DisableEditorLink()
        {
            ClusterDebug.Log("[Cluster Replication] Disabling editor link");
            m_EditorLinkReference?.Dispose();
            m_EditorLinkReference = null;

            foreach (var (_, replicator) in Replicators)
            {
                replicator.EditorLink = null;
            }
        }

        public void OnEditorLinkChanged()
        {
            ClusterDebug.Log("[Cluster Replication] Link config changed");
            if (m_EditorLinkConfig != null)
            {
                if (isActiveAndEnabled)
                {
                    EnableEditorLink();
                }
            }
            else
            {
                DisableEditorLink();
            }
        }

        void EnableReplicators()
        {
            foreach (var (_, replicator) in Replicators)
            {
                replicator.Initialize(m_Mode);
            }
        }

        void OnDisable()
        {
            DisableEditorLink();

            ClusterSyncLooper.onInstanceDoPreFrame -= OnPreFrame;
            PlayerLoopExtensions.DeregisterUpdate<ReplicationUpdate>(UpdateReplicators);
            DisableReplicators();
        }

        void OnPreFrame()
        {
            foreach (var (_, replicator) in Replicators)
            {
                replicator.OnPreFrame();
            }
        }

        void DisableReplicators()
        {
            m_EditorLinkReference?.Dispose();
            foreach (var (_, replicator) in Replicators)
            {
                replicator.Dispose();
            }
        }

        void UpdateReplicators()
        {
            foreach (var (_, replicator) in Replicators)
            {
                if (!replicator.IsInitialized)
                    replicator.Initialize(m_Mode);

                replicator.Update();
            }
        }

        static IReplicator MakeReplicator(ReplicationTarget target) =>
            s_SpecializedReplicators.TryGetValue(target.Component.GetType(),
                out var createFunc)
                ? createFunc(target)
                : new GenericPropertyReplicator(target);

        void AddTarget(ReplicationTarget target)
        {
            var replicator = MakeReplicator(target);
            Replicators.Add(target, replicator);
        }

        public void AddTarget(Component component, string property = null) =>
            AddTarget(new ReplicationTarget {Component = component, Property = property});

        public void RemoveTarget(ReplicationTarget target) => Replicators.Remove(target);

        public void OnBeforeSerialize()
        {
            m_ReplicationTargets = new List<ReplicationTarget>();
            foreach (var (target, replicator) in Replicators)
            {
                target.IsValid = replicator.IsValid;
                m_ReplicationTargets.Add(target);
            }
        }

        public void OnAfterDeserialize()
        {
            Replicators.Clear();
            foreach (var target in m_ReplicationTargets)
            {
                AddTarget(target);
            }
        }
    }
}
