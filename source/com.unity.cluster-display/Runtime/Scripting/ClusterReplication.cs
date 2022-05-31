using System;
using System.Collections.Generic;
using System.Linq;
using Unity.ClusterDisplay.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Unity.ClusterDisplay.Scripting
{

#if UNITY_EDITOR
    [InitializeOnLoad]
    [ExecuteAlways]
#endif
    [DisallowMultipleComponent]
    public class ClusterReplication : MonoBehaviour, ISerializationCallbackReceiver
    {
        static Dictionary<Type, Func<ReplicationTarget,IReplicator>> s_SpecializedReplicators = new();

        struct ReplicationUpdate { }

        /// <summary>
        /// For editor and serialization only. Do not use for business logic. Use
        /// <see cref="m_Replicators"/> instead.
        /// </summary>
        [SerializeField]
        List<ReplicationTarget> m_ReplicationTargets;

        Dictionary<ReplicationTarget, IReplicator> m_Replicators = new();

        static ClusterReplication()
        {
            RegisterSpecializedReplicator<Transform>(target =>
                new TransformReplicator(target.Guid, target.Component as Transform));
        }

        internal static void RegisterSpecializedReplicator<T>(Func<ReplicationTarget, IReplicator> createFunc) =>
            s_SpecializedReplicators.Add(typeof(T), createFunc);

        internal static bool HasSpecializedReplicator(Type t) => s_SpecializedReplicators.TryGetValue(t, out _);

        void OnEnable()
        {
            foreach (var (_, replicator) in m_Replicators)
            {
                replicator.OnEnable();
            }
            PlayerLoopExtensions.RegisterUpdate<PostLateUpdate.DirectorLateUpdate, ReplicationUpdate>(UpdateReplicators);
        }

        void OnDisable()
        {
            foreach (var (_, replicator) in m_Replicators)
            {
                replicator.OnDisable();
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
            var target = new ReplicationTarget
            {
                Component = component,
                Property = property
            };
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
