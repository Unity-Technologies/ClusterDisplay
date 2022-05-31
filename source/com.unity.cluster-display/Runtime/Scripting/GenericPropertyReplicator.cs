using System;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay.Scripting
{
    class GenericPropertyReplicator : IReplicator
    {
        IReplicator m_Impl;

        public GenericPropertyReplicator(ReplicationTarget target)
        {
            if (string.IsNullOrEmpty(target.Property))
            {
                return;
            }

            var propertyInfo = target.Component.GetType().GetProperty(target.Property);
            if (propertyInfo == null)
            {
                Debug.LogError($"{target.Component} does not contain property {target.Property}");
                return;
            }

            try
            {
                var implTypeGeneric = typeof(GenericPropertyReplicatorImpl<>);
                var implTypeSpecific = implTypeGeneric.MakeGenericType(propertyInfo.PropertyType);
                m_Impl = Activator.CreateInstance(implTypeSpecific, target) as IReplicator;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void OnEnable() => m_Impl?.OnEnable();

        public void OnDisable() => m_Impl?.OnDisable();

        public void Update() => m_Impl?.Update();
        public bool IsValid => m_Impl?.IsValid ?? false;

        public void Dispose()
        {
            m_Impl?.Dispose();
        }
    }

    class GenericPropertyReplicatorImpl<T> : ReplicatorBase<T> where T : unmanaged, IEquatable<T>
    {
        readonly PropertyInfo m_PropertyInfo;
        readonly object m_Component;

        public GenericPropertyReplicatorImpl(ReplicationTarget target)
            : base(target.Guid)
        {
            m_Component = target.Component;
            m_PropertyInfo = m_Component.GetType().GetProperty(target.Property);
            ClusterDebug.Log($"Replicator created for property {m_PropertyInfo}");
        }

        public override bool IsValid => true;
        protected override T GetCurrentState() => (T)m_PropertyInfo.GetValue(m_Component);

        protected override void ApplyMessage(in T message)
        {
#if UNITY_EDITOR
            return;
#endif
            m_PropertyInfo.SetValue(m_Component, message);
        }
    }
}
