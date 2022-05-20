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
            var propertyInfo = target.Target.GetType().GetProperty(target.Property);
            if (propertyInfo == null)
            {
                Debug.LogError($"{target.Target} does not contain property {target.Property}");
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
                throw;
            }
        }

        public void OnEnable() => m_Impl?.OnEnable();

        public void OnDisable() => m_Impl?.OnDisable();

        public void Update() => m_Impl?.Update();
    }

    class GenericPropertyReplicatorImpl<T> : ReplicatorBase<T> where T : unmanaged
    {
        PropertyInfo m_PropertyInfo;
        object m_Target;

        public GenericPropertyReplicatorImpl(ReplicationTarget target)
            : base(target.Guid)
        {
            m_Target = target.Target;
            m_PropertyInfo = m_Target.GetType().GetProperty(target.Property);
        }

        protected override T GetCurrentState() => (T)m_PropertyInfo.GetValue(m_Target);

        protected override void ApplyMessage(in T message)
        {
#if UNITY_EDITOR
            return;
#endif
            m_PropertyInfo.SetValue(m_Target, message);
        }
    }
}
