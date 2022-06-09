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
        bool m_EditorLinkConfigChanged;

        /// <summary>
        /// For inspector and serialization only. Do not use for business logic. Use
        /// <see cref="m_Replicators"/> instead.
        /// </summary>
        [SerializeField]
        List<ReplicationTarget> m_ReplicationTargets;

        Dictionary<ReplicationTarget, IReplicator> m_Replicators = new();


        // For efficiency, all instances of ClusterReplication should share a single
        // EditorLink.
        static AutoReleaseReference<EditorLinkConfig, EditorLink> s_SharedEditorLink = new(CreateEditorLink);
        AutoReleaseReference<EditorLinkConfig, EditorLink>.SharedRef m_EditorLinkReference;

        ReplicatorMode m_Mode = ReplicatorMode.Disabled;

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

        internal static bool HasSpecializedReplicator(Type t) => s_SpecializedReplicators.TryGetValue(t, out _);

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

            foreach (var (_, replicator) in m_Replicators)
            {
                replicator.EditorLink = m_EditorLinkReference;
            }
        }

        void DisableEditorLink()
        {
            ClusterDebug.Log("[Cluster Replication] Disabling editor link");
            m_EditorLinkReference?.Dispose();
            m_EditorLinkReference = null;

            foreach (var (_, replicator) in m_Replicators)
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
            foreach (var (_, replicator) in m_Replicators)
            {
                replicator.OnEnable(m_Mode);
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
            foreach (var (_, replicator) in m_Replicators)
            {
                replicator.OnPreFrame();
            }
        }

        void DisableReplicators()
        {
            m_EditorLinkReference?.Dispose();
            foreach (var (_, replicator) in m_Replicators)
            {
                replicator.OnDisable();
            }
        }

        void UpdateReplicators()
        {
            foreach (var (_, replicator) in m_Replicators)
            {
                replicator.Update();
            }
        }

        static IReplicator MakeReplicator(ReplicationTarget target) =>
            s_SpecializedReplicators.TryGetValue(target.Component.GetType(),
                out var create)
                ? create(target)
                : new GenericPropertyReplicator(target);

        public void AddTarget(Component component, string property = null)
        {
            var target = new ReplicationTarget {Component = component, Property = property};
            m_Replicators.Add(target, MakeReplicator(target));
        }

        public void RemoveTarget(ReplicationTarget target) => m_Replicators.Remove(target);

        public void OnBeforeSerialize()
        {
            m_ReplicationTargets = new List<ReplicationTarget>();
            foreach (var (target, replicator) in m_Replicators)
            {
                target.IsValid = replicator.IsValid;
                m_ReplicationTargets.Add(target);
            }
        }

        public void OnAfterDeserialize()
        {
            m_Replicators = m_ReplicationTargets.ToDictionary(target => target, MakeReplicator);
        }
    }
}
