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

        /// <summary>
        /// For inspector and serialization only. Do not use for business logic. Use
        /// <see cref="m_Replicators"/> instead.
        /// </summary>
        [SerializeField]
        List<ReplicationTarget> m_ReplicationTargets;

        Dictionary<ReplicationTarget, IReplicator> m_Replicators = new();

        // For efficiency, all instances of ClusterReplication should share a single
        // EditorLink.
        static AutoReleaseReference<EditorLink> s_SharedEditorLink = new(CreateEditorLink);
        AutoReleaseReference<EditorLink>.SharedRef m_EditorLinkReference;

        ReplicatorMode m_Mode = ReplicatorMode.Disabled;

        static EditorLink CreateEditorLink()
        {
#if UNITY_EDITOR
            return new EditorLink(true);
#else
            return new EditorLink(false);
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

            EnableReplicators();
        }

        void EnableReplicators()
        {
            if (m_Mode is ReplicatorMode.Editor or ReplicatorMode.Emitter)
            {
                m_EditorLinkReference = s_SharedEditorLink.GetReference();
            }

            foreach (var (_, replicator) in m_Replicators)
            {
                replicator.OnEnable(m_Mode, m_EditorLinkReference);
                ClusterSyncLooper.onInstanceDoPreFrame += replicator.OnPreFrame;
            }

            PlayerLoopExtensions.RegisterUpdate<PostLateUpdate.DirectorLateUpdate, ReplicationUpdate>(UpdateReplicators);
        }

        void OnDisable()
        {
            m_EditorLinkReference?.Dispose();
            if (m_Mode is ReplicatorMode.Editor)
            {
                DisableReplicators();
            }
        }

        void DisableReplicators()
        {
            foreach (var (_, replicator) in m_Replicators)
            {
                replicator.OnDisable();
                ClusterSyncLooper.onInstanceDoPreFrame -= replicator.OnPreFrame;
            }

            PlayerLoopExtensions.DeregisterUpdate<ReplicationUpdate>(UpdateReplicators);
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
