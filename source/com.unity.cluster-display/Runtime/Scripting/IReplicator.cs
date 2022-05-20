using System;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay.Scripting
{
    interface IReplicator
    {
        void OnEnable();
        void OnDisable();
        void Update();
    }

    readonly struct ReplicationMessage<T> where T : unmanaged
    {
        public readonly Guid Guid;
        public readonly T Contents;

        public ReplicationMessage(Guid guid, in T contents)
        {
            Guid = guid;
            Contents = contents;
        }
    }

    abstract class ReplicatorBase<TContents> : IReplicator where TContents : unmanaged
    {
        EventBus<ReplicationMessage<TContents>> m_EventBus;
        IDisposable m_EventSubscriber;

        TContents m_LatestMessage;
        bool m_HasNewMessage;

        Guid Guid { get; }

        protected ReplicatorBase(Guid guid)
        {
            Guid = guid;
        }

        public void OnEnable()
        {
            m_EventBus = ServiceLocator.TryGet(out IClusterSyncState clusterSyncState)
                ? new EventBus<ReplicationMessage<TContents>>(clusterSyncState)
                : new EventBus<ReplicationMessage<TContents>>();

            m_EventSubscriber = m_EventBus.Subscribe(msg =>
            {
                if (msg.Guid == Guid)
                {
                    m_LatestMessage = msg.Contents;
                    m_HasNewMessage = true;
                }
            });
        }

        public void OnDisable()
        {
            m_EventBus?.Dispose();
            m_EventSubscriber?.Dispose();
        }

        public void Update()
        {
            var messageToPublish = GetCurrentState();
            if (m_HasNewMessage)
            {
                ApplyMessage(m_LatestMessage);
                m_HasNewMessage = false;
            }

            m_EventBus.Publish(new ReplicationMessage<TContents>(Guid, messageToPublish));
        }

        protected abstract TContents GetCurrentState();

        protected abstract void ApplyMessage(in TContents message);
    }

    [Serializable]
    class ReplicationTarget
    {
        [SerializeField]
        SerializableGuid m_Guid = new();

        [SerializeField]
        Component m_Component;

        [SerializeField]
        string m_Property;

        public Component Target
        {
            get => m_Component;
            set => m_Component = value;
        }

        public string Property
        {
            get => m_Property;
            set => m_Property = value;
        }

        public Guid Guid => m_Guid;


    }
}
