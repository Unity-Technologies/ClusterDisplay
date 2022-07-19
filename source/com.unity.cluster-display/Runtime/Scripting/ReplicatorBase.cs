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
        /// <summary>
        /// <see cref="OnPreFrame"/> is called during WaitForLastPresentationAndUpdateTime, at the beginning of the
        /// update loop.
        /// </summary>
        void OnPreFrame();

        void Initialize(ReplicatorMode mode);

        /// <summary>
        /// <see cref="Update"/> is called during PostLateUpdate.
        /// </summary>
        ///
        void Update();
        bool IsValid { get; }
        bool IsInitialized { get; }
        Guid Guid { get; }
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
        public bool IsInitialized { get; private set; }
        public Guid Guid { get; }

        IDisposable m_EventSubscriber;

        /// <summary>
        /// The latest unprocessed message received from the Editor Link. Applies to emitter only.
        /// </summary>
        TContents m_LatestLinkMessage;

        /// <summary>
        /// Indicates that we should replicate (broadcast to the cluster) <see cref="m_LatestLinkMessage"/> instead of
        /// whatever value this property currently has. Applies to emitter only.
        /// </summary>
        bool m_HasLinkMessage;

        /// <summary>
        /// The latest unprocessed messaged received from <see cref="EventBus"/> (i.e. cluster data from the emitter).
        /// If we are the emitter, this is a loopback message.
        /// </summary>
        TContents m_LatestReplicatorMessage;

        /// <summary>
        /// Indicates that we should apply the data in <see cref="m_LatestReplicatorMessage"/> to the target property
        /// (replacing its current value).
        /// </summary>
        bool m_HasReplicatorMessage;

        /// <summary>
        /// Indicates whether the target property has changed value since the previous frame.
        /// </summary>
        bool m_PropertyChanged;

        EditorLink m_EditorLink;
        TContents m_StateCapture;
        ReplicatorMode m_Mode;

        internal EventBus<ReplicationMessage<TContents>> EventBus { get; private set; }

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

        public void Initialize(ReplicatorMode mode)
        {
            m_Mode = mode;
            InitEventBus();
            m_StateCapture = GetCurrentState();
            IsInitialized = true;

            ClusterDebug.Log($"[Replicator] Initialized in {m_Mode} mode");
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <summary>
        /// Reads the current value of the target property and encode it in a <typeparamref name="TContents"/> structure.
        /// </summary>
        /// <returns>
        /// The value of the target property has a <typeparamref name="TContents"/> structure.
        /// </returns>
        protected abstract TContents GetCurrentState();

        /// <summary>
        /// Read the contents of <paramref name="message"/> and set the value of the target property accordingly.
        /// </summary>
        /// <param name="message">Replication message received from the cluster.</param>
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

        public Guid Guid
        {
            get => m_Guid;
            set => m_Guid.FromGuid(value);
        }

        public bool IsValid
        {
            get => m_IsValid;
            internal set => m_IsValid = value;
        }
    }
}
