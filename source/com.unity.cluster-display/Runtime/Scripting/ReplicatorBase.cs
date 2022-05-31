using System;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay.Scripting
{
    interface IReplicator : IDisposable
    {
        void OnEnable();
        void OnDisable();
        void Update();
        bool IsValid { get; }
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

    abstract class ReplicatorBase<TContents> : IReplicator where TContents : unmanaged, IEquatable<TContents>
    {
        public enum Mode
        {
            Editor,
            Emitter,
            Repeater
        }

        public Mode OperatingMode { get; private set; }

        internal Guid Guid { get; }

        IDisposable m_EventSubscriber;

        TContents m_LatestLiveLinkMessage;
        bool m_HasLiveLinkMessage;

        TContents m_LatestReplicatorMessage;
        bool m_HasReplicatorMessage;
        bool m_PropertyChanged;

        EditorLiveLink m_LiveLink;
        TContents m_StateCapture;

        EventBus<ReplicationMessage<TContents>> EventBus { get; set; }

        protected ReplicatorBase(Guid guid)
        {
            Guid = guid;
        }

        void ReceiveLiveLinkMessage(ReplicationMessage<TContents> message)
        {
            Debug.Assert(message.Guid == Guid);
            ClusterDebug.Log($"[Replicator] Received Live Link message {message.Contents}");

            m_LatestLiveLinkMessage = message.Contents;
            m_HasLiveLinkMessage = true;
        }

        public void OnEnable()
        {
            EventBus = ServiceLocator.TryGet(out IClusterSyncState clusterSyncState)
                ? new EventBus<ReplicationMessage<TContents>>(clusterSyncState)
                : new EventBus<ReplicationMessage<TContents>>();

            OperatingMode = Mode.Repeater;
            if (clusterSyncState is {NodeRole: NodeRole.Emitter})
            {
                OperatingMode = Mode.Emitter;
            }

    #if UNITY_EDITOR
            OperatingMode = Mode.Editor;
    #endif

            m_StateCapture = GetCurrentState();
            m_EventSubscriber = EventBus.Subscribe(msg =>
            {
                if (msg.Guid == Guid)
                {
                    m_LatestReplicatorMessage = msg.Contents;
                    m_HasReplicatorMessage = true;
                    ClusterDebug.Log($"[Replicator] received eventbus message: {msg.Contents}");
                }
            });

            if (OperatingMode is Mode.Editor or Mode.Emitter)
            {
                ClusterDebug.Log("[Replicator] Enabling Live Link");
                if (!ServiceLocator.TryGet(out m_LiveLink))
                {
                    m_LiveLink = new EditorLiveLink(clusterSyncState);
                    ServiceLocator.Provide(m_LiveLink);
                }

                m_LiveLink.ConnectReceiver<TContents>(Guid, ReceiveLiveLinkMessage);
                ClusterSyncLooper.onInstanceDoPreFrame += RestoreState;
            }
        }

        void RestoreState()
        {
            // During the sync point (the beginning of the game loop),
            // restore the "original" state of the property. If this is the emitter,
            // the replicated state (what we presented to the screen)
            // is actually a frame behind. After performing the restore,
            // the state of the property would appear "normal" (not delayed) to
            // other scripts on Update and LateUpdate of the current frame.
            if (OperatingMode is Mode.Emitter && m_PropertyChanged)
            {
                ApplyMessage(m_StateCapture);
                m_PropertyChanged = false;
            }
        }

        public void OnDisable()
        {
            Dispose();
        }

        public void Update()
        {
            var newState = GetCurrentState();

            // Editor: just transmit the current state to the live link.
            // No replication logic.
            if (OperatingMode is Mode.Editor && m_LiveLink is {IsTransmitter: true} liveLink)
            {
                if (!newState.Equals(m_StateCapture))
                {
                    ClusterDebug.Log($"[Replicator] State changed - publishing Live Link {newState}");
                    liveLink.Publish(new ReplicationMessage<TContents>(Guid, newState));
                }

                m_StateCapture = newState;

                return;
            }

            // Emitter is responsible for coordinating the replication
            if (OperatingMode is Mode.Emitter)
            {
                // Emitter should retransmit the live link messages as replication messages
                if (m_HasLiveLinkMessage)
                {
                    newState = m_LatestLiveLinkMessage;
                    m_HasLiveLinkMessage = false;
                }

                // Transmit data to replicate (if property has changed)
                if (!newState.Equals(m_StateCapture))
                {
                    // Publishing data on the event bus means that all nodes (including this one)
                    // will see this message at the NEXT sync point.
                    ClusterDebug.Log($"[Replicator] Publish message {newState}");
                    EventBus.Publish(new ReplicationMessage<TContents>(Guid, newState));
                    m_PropertyChanged = true;
                }

                m_StateCapture = newState;
            }

            if (m_HasReplicatorMessage)
            {
                ClusterDebug.Log($"[Replicator] Applying message: {m_LatestReplicatorMessage}");
                ApplyMessage(m_LatestReplicatorMessage);
                m_HasReplicatorMessage = false;
            }
        }

        public abstract bool IsValid { get; }

        protected abstract TContents GetCurrentState();

        protected abstract void ApplyMessage(in TContents message);

        public void Dispose()
        {
            m_EventSubscriber?.Dispose();
            EventBus?.Dispose();
            m_LiveLink?.DisconnectReceiver(Guid);
            ClusterSyncLooper.onInstanceDoPreFrame -= RestoreState;
        }
    }

    [Serializable]
    public class ReplicationTarget
    {
        [SerializeField]
        SerializableGuid m_Guid = new();

        [SerializeField]
        Component m_Component;

        [SerializeField]
        string m_Property;


        [SerializeField]
        bool m_IsValid;

        public Component Component
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

        public bool IsValid
        {
            get => m_IsValid;
            set => m_IsValid = value;
        }
    }
}
