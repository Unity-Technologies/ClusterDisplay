using System;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay.Scripting
{
    /// <summary>
    /// Script for replicating an arbitrary property on a component.
    /// </summary>
    /// <remarks>
    /// This class will also work for fields in addition to properties. However,
    /// we maintain the "property" terminology for convenience because public fields
    /// are discouraged and thus not as common in practice.
    /// </remarks>
    class GenericPropertyReplicator : IReplicator
    {
        IReplicator m_Impl;

        public GenericPropertyReplicator(ReplicationTarget target)
        {
            if (string.IsNullOrEmpty(target.Property))
            {
                return;
            }

            var memberAccessor = CreateMemberAccessor(target.Component, target.Property);
            if (memberAccessor == null)
            {
                return;
            }

            try
            {
                var implTypeGeneric = typeof(GenericPropertyReplicatorImpl<>);
                var implTypeSpecific = implTypeGeneric.MakeGenericType(memberAccessor.Type);
                m_Impl = Activator.CreateInstance(implTypeSpecific, target.Guid, memberAccessor) as IReplicator;
            }
            catch (Exception e)
            {
                Debug.LogError($"Cannot create replicator for {memberAccessor.Name}: {e.Message}");
            }
        }

        public void OnPreFrame() => m_Impl?.OnPreFrame();

        public void OnEnable(ReplicatorMode mode, EditorLink link = null) => m_Impl?.OnEnable(mode, link);

        public void OnDisable() => m_Impl?.OnDisable();

        public void Update() => m_Impl?.Update();
        public bool IsValid => m_Impl?.IsValid ?? false;

        public void Dispose()
        {
            m_Impl?.Dispose();
        }

        static IMemberAccessor CreateMemberAccessor(object obj, string memberName)
        {
            if (obj.GetType().GetProperty(memberName) is { } propertyInfo)
            {
                return new PropertyMember(obj, propertyInfo);
            }

            if (obj.GetType().GetField(memberName) is { } fieldInfo)
            {
                return new FieldMember(obj, fieldInfo);
            }

            Debug.LogError($"{obj.GetType()} does not contain property {memberName}");
            return null;
        }
    }

    interface IMemberAccessor
    {
        string Name { get; }
        Type Type { get; }

        object GetValue();
        void SetValue(object value);
    }

    class PropertyMember : IMemberAccessor
    {
        readonly PropertyInfo m_PropertyInfo;
        readonly object m_Object;

        public PropertyMember(object obj, PropertyInfo propertyInfo)
        {
            m_Object = obj;
            m_PropertyInfo = propertyInfo;
        }

        public string Name => m_PropertyInfo.Name;
        public Type Type => m_PropertyInfo.PropertyType;
        public object GetValue() => m_PropertyInfo.GetValue(m_Object);

        public void SetValue(object value) => m_PropertyInfo.SetValue(m_Object, value);
    }

    class FieldMember : IMemberAccessor
    {
        readonly FieldInfo m_FieldInfo;
        readonly object m_Object;

        public FieldMember(object obj, FieldInfo fieldInfo)
        {
            m_Object = obj;
            m_FieldInfo = fieldInfo;
        }

        public string Name => m_FieldInfo.Name;
        public Type Type => m_FieldInfo.FieldType;
        public object GetValue() => m_FieldInfo.GetValue(m_Object);

        public void SetValue(object value) => m_FieldInfo.SetValue(m_Object, value);
    }

    class GenericPropertyReplicatorImpl<T> : ReplicatorBase<T> where T : unmanaged, IEquatable<T>
    {
        readonly IMemberAccessor m_MemberAccessor;

        public GenericPropertyReplicatorImpl(Guid guid, IMemberAccessor memberAccessor)
            : base(guid)
        {
            m_MemberAccessor = memberAccessor;
            ClusterDebug.Log($"Replicator created for property {m_MemberAccessor.Name}");
        }

        public override bool IsValid => true;
        protected override T GetCurrentState() => (T)m_MemberAccessor.GetValue();

        protected override void ApplyMessage(in T message)
        {
#if UNITY_EDITOR
            return;
#endif
            m_MemberAccessor.SetValue(message);
        }
    }
}
