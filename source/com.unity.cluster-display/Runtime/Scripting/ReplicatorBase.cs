using System;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay.Scripting
{
    public enum ReplicatorMode
    {
        Disabled,
        Editor,
        Emitter,
        Repeater
    }

    interface IReplicator : IDisposable
    {
        void OnPreFrame();
        void OnEnable(ReplicatorMode mode);
        void OnDisable();
        void Update();
        bool IsValid { get; }
        EditorLink EditorLink { get; set; }
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
        Guid Guid { get; }

        IDisposable m_EventSubscriber;

        TContents m_LatestLinkMessage;
        bool m_HasLinkMessage;

        TContents m_LatestReplicatorMessage;
        bool m_HasReplicatorMessage;
        bool m_PropertyChanged;

        EditorLink m_EditorLink;
        TContents m_StateCapture;
        ReplicatorMode m_Mode;

        EventBus<ReplicationMessage<TContents>> EventBus { get; set; }

        protected ReplicatorBase(Guid guid)
        {
            Guid = guid;
        }

        void ReceiveLiveLinkMessage(ReplicationMessage<TContents> message)
        {
            Debug.Assert(message.Guid == Guid);
            ClusterDebug.Log($"[Replicator] Received Live Link message {message.Contents}");

            m_LatestLinkMessage = message.Contents;
            m_HasLinkMessage = true;
        }

        void InitEventBus()
        {
            var eventBusFlags = m_Mode switch
            {
                ReplicatorMode.Emitter => EventBusFlags.Loopback | EventBusFlags.WriteToCluster,
                ReplicatorMode.Repeater => EventBusFlags.ReadFromCluster,
                _ => EventBusFlags.None,
            };

            EventBus = new EventBus<ReplicationMessage<TContents>>(eventBusFlags);

            m_EventSubscriber = EventBus.Subscribe(msg =>
            {
                if (msg.Guid == Guid)
                {
                    m_LatestReplicatorMessage = msg.Contents;
                    m_HasReplicatorMessage = true;
                    ClusterDebug.Log($"[Replicator] received eventbus message: {msg.Contents}");
                }
            });
        }

        public void OnEnable(ReplicatorMode mode)
        {
            m_Mode = mode;
            InitEventBus();
            m_StateCapture = GetCurrentState();

            ClusterDebug.Log($"[Replicator] Enabled in {m_Mode} mode");
        }

        public void OnPreFrame()
        {
            // During the sync point (the beginning of the game loop),
            // restore the "original" state of the property. If this is the emitter,
            // the replicated state (what we presented to the screen)
            // is actually a frame behind. After performing the restore,
            // the state of the property would appear "normal" (not delayed) to
            // other scripts on Update and LateUpdate of the current frame.
            if (m_Mode is ReplicatorMode.Emitter && m_PropertyChanged)
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
            if (m_Mode is ReplicatorMode.Editor && EditorLink is {} liveLink)
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
            if (m_Mode is ReplicatorMode.Emitter)
            {
                // Emitter should retransmit the live link messages as replication messages
                if (m_HasLinkMessage)
                {
                    newState = m_LatestLinkMessage;
                    m_HasLinkMessage = false;
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

        public EditorLink EditorLink
        {
            get => m_EditorLink;
            set
            {
                if (value != m_EditorLink)
                {
                    m_EditorLink?.DisconnectReceiver(Guid);
                }

                m_EditorLink = value;

                if (m_Mode is ReplicatorMode.Emitter)
                {
                    m_EditorLink?.ConnectReceiver<TContents>(Guid, ReceiveLiveLinkMessage);
                }
            }
        }

        protected abstract TContents GetCurrentState();

        protected abstract void ApplyMessage(in TContents message);

        public void Dispose()
        {
            m_EventSubscriber?.Dispose();
            EventBus?.Dispose();
            EditorLink = null;
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
            internal set => m_IsValid = value;
        }
    }
}
