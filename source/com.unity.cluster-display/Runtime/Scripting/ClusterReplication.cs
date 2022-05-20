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
#endif
    public class ClusterReplication : MonoBehaviour
    {
        static Dictionary<Type, Func<ReplicationTarget,IReplicator>> s_SpecializedReplicators = new();

        struct ReplicationUpdate { }

        [SerializeField]
        List<ReplicationTarget> m_ReplicationTargets = new();

        List<IReplicator> m_Replicators;

        static ClusterReplication()
        {
            RegisterSpecializedReplicator<Transform>(target =>
                new TransformReplicator(target.Guid, target.Target as Transform));
        }

        internal static void RegisterSpecializedReplicator<T>(Func<ReplicationTarget, IReplicator> createFunc) =>
            s_SpecializedReplicators.Add(typeof(T), createFunc);

        internal static bool HasSpecializedReplicator(Type t) => s_SpecializedReplicators.TryGetValue(t, out _);

        public void AddTarget(Component component, string property)
        {
            m_ReplicationTargets.Add(new ReplicationTarget
            {
                Target = component,
                Property = property
            });
        }

        void OnEnable()
        {
            m_Replicators ??= m_ReplicationTargets.Select(MakeReplicator).ToList();
            foreach (var replicator in m_Replicators)
            {
                replicator.OnEnable();
            }
            PlayerLoopExtensions.RegisterUpdate<PostLateUpdate.DirectorLateUpdate, ReplicationUpdate>(UpdateReplicators);
        }

        void OnDisable()
        {
            foreach (var replicator in m_Replicators)
            {
                replicator.OnDisable();
            }
            PlayerLoopExtensions.DeregisterUpdate<ReplicationUpdate>(UpdateReplicators);
        }

        void UpdateReplicators()
        {
            foreach (var replicator in m_Replicators)
            {
                replicator.Update();
            }
        }

        static IReplicator MakeReplicator(ReplicationTarget target) =>
            s_SpecializedReplicators.TryGetValue(target.Target.GetType(),
                out var create)
                ? create(target)
                : new GenericPropertyReplicator(target);
    }
}
