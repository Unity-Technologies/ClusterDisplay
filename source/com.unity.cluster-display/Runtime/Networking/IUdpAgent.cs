using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Unity.Collections;
using Utils;
using Debug = UnityEngine.Debug;
using MessagePreprocessor = System.Func<Unity.ClusterDisplay.ReceivedMessageBase, Unity.ClusterDisplay.PreProcessResult>;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Ways for a <see cref="IReceivedMessageData"/> to deal with data.
    /// </summary>
    enum ReceivedMessageDataFormat
    {
        ManagedArray,
        NativeArray
    }

    /// <summary>
    /// Interface for data associated to <see cref="ReceivedMessageBase"/>.
    /// </summary>
    interface IReceivedMessageData
    {
        /// <summary>
        /// Most efficient format for the <see cref="IReceivedMessageData"/> to return the data it contains.
        /// </summary>
        ReceivedMessageDataFormat PreferredFormat { get; }

        /// <summary>
        /// Returns the length of the data.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Returns the data as a managed array.
        /// </summary>
        /// <param name="array">Array containing the data.  Content of the array shall not be modified.</param>
        /// <param name="dataStart">First byte in array that contains the data.  Data before that index is undefined.
        /// </param>
        /// <param name="dataLength">Length of the data (starting at <paramref name="dataStart"/>). </param>
        /// <remarks>Some implementation of the method might have to copy the data (to a managed array) but result
        /// will be cashed for cases where the method would be called multiple times.</remarks>
        void AsManagedArray(out byte[] array, out int dataStart, out int dataLength);

        /// <summary>
        /// Returns the data as a native array.
        /// </summary>
        /// <returns>The native array.</returns>
        /// <remarks>Some implementation of the method might have to copy the data (to a NativeArray) but result will be
        /// cashed for cases where the method would be called multiple times.</remarks>
        NativeArray<byte> AsNativeArray();

        /// <summary>
        /// Indicate that the "owner" of the <see cref="IReceivedMessageData"/> is done of it and it can be potentially
        /// reused for other received messages.
        /// </summary>
        void Release();
    }

    /// <summary>
    /// Base class for the different ReceivedMessage specializations
    /// </summary>
    abstract class ReceivedMessageBase: IDisposable
    {
        /// <summary>
        /// Type of <see cref="ReceivedMessage{M}"/>.
        /// </summary>
        public MessageType Type { get; private set; }

        /// <summary>
        /// IDispose implementation that return the ReceivedMessage to its pool to avoid allocation next time we will
        /// need a message of the same type.
        /// </summary>
        public void Dispose()
        {
            ReturnToPool();
        }

        /// <summary>
        /// Extra data (bytes) that were present after the <see cref="ReceivedMessage{TM}.Payload"/> on the network.
        /// </summary>
        public IReceivedMessageData ExtraData => m_ExtraData;

        /// <summary>
        /// Sets the <see cref="ExtraData"/> associated with the <see cref="IReceivedMessageData"/>.
        /// </summary>
        /// <param name="extraData">The <see cref="IReceivedMessageData"/> to initialize the
        /// <see cref="ReceivedMessageBase"/> with.</param>
        /// <remarks>The <see cref="ReceivedMessageBase"/> will take "ownership" of the extra data and call the
        /// <see cref="IReceivedMessageData.Release"/> method on it when the <see cref="ReceivedMessageBase"/> is
        /// disposed of.</remarks>
        internal void AdoptExtraData(IReceivedMessageData extraData)
        {
            m_ExtraData?.Release();
            m_ExtraData = extraData;
        }

        /// <summary>
        /// Returns the current <see cref="ExtraData"/> of this <see cref="ReceivedMessageBase"/> and remove it from this
        /// <see cref="ReceivedMessageBase"/> so that the <see cref="IReceivedMessageData.Release"/> method does not get
        /// called by this <see cref="ReceivedMessageBase"/>.
        /// </summary>
        /// <returns><see cref="ExtraData"/> value before calling this method.</returns>
        internal IReceivedMessageData DetachExtraData()
        {
            var ret = m_ExtraData;
            m_ExtraData = null;
            return ret;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">Type of <see cref="ReceivedMessage{M}"/>.</param>
        protected ReceivedMessageBase(MessageType type)
        {
            Type = type;
        }

        /// <summary>
        /// Method to be called when done of the <see cref="ReceivedMessageBase"/> to return it to the pool it belongs to.
        /// </summary>
        protected virtual void ReturnToPool()
        {
            m_ExtraData?.Release();
            m_ExtraData = null;
        }

        /// <summary>
        /// Used to detect double pooling (calling <see cref="IDisposable.Dispose"/> twice).
        /// </summary>
        protected bool IsInPool { get; set; } = true; // Default to true since it should be created by the ObjectPool

        /// <summary>
        /// Extra data (bytes) that were present after the <see cref="ReceivedMessage{TM}.Payload"/> on the network.
        /// </summary>
        IReceivedMessageData m_ExtraData;
    }

    /// <summary>
    /// All the information about a received message
    /// </summary>
    /// <typeparam name="TM">Type of payload of this message</typeparam>
    class ReceivedMessage<TM>: ReceivedMessageBase where TM: unmanaged
    {
        /// <summary>
        /// Payload of the message
        /// </summary>
        public TM Payload { get; set; }

        /// <summary>
        /// Construct a new ReceivedMessage that still need to be filled with an actual content.
        /// </summary>
        /// <remarks>Private since this constructor is not designed to be called manually, instead call the
        /// <see cref="GetFromPool()"/> method combined with calling the <see cref="IDisposable.Dispose"/> method when
        /// done of it.</remarks>
        ReceivedMessage() : base(MessageTypeAttribute.GetTypeOf<TM>())
        {
        }

        /// <summary>
        /// Returns a <see cref="ReceivedMessage{M}"/> instance by reusing previous instances returned to a free pool
        /// when the <see cref="IDisposable.Dispose"/> method is called.
        /// </summary>
        /// <returns>The <see cref="ReceivedMessage{M}"/> instance.</returns>
        /// <remarks>Will automatically allocate a new one if there are no free instances in the pool.</remarks>
        public static ReceivedMessage<TM> GetFromPool()
        {
            var ret = s_InstancesPool.Get();
            Debug.Assert(ret.IsInPool);
            ret.IsInPool = false;
            return ret;
        }

        /// <summary>
        /// Returns a <see cref="ReceivedMessage{M}"/> instance by reusing previous instances returned to a free pool
        /// when the <see cref="IDisposable.Dispose"/> method is called and fill it from the given byte array.
        /// </summary>
        /// <param name="fillFrom">Bytes to fill the message from (length must match sizeof(M))</param>
        /// <returns>The <see cref="ReceivedMessage{M}"/> instance and the amount of data from
        /// <paramref name="fillFrom"/> that was not used to fill the <see cref="ReceivedMessage{M}"/>.</returns>
        /// <remarks>Will automatically allocate a new one if there are no free instances in the pool.</remarks>
        /// <exception cref="ArgumentException">If length of <paramref name="fillFrom"/> does not match length of
        /// <see cref="TM"/>.</exception>
        public static (ReceivedMessageBase message, int leftOver) GetFromPool(ReadOnlySpan<byte> fillFrom)
        {
            var ret = GetFromPool();
            ret.Payload = fillFrom.LoadStruct<TM>();
            return (ret, fillFrom.Length - Marshal.SizeOf<TM>());
        }

        /// <summary>
        /// Transfer the content from this <see cref="ReceivedMessage{TM}"/> to a new one.
        /// </summary>
        /// <returns>The new <see cref="ReceivedMessage{TM}"/> that contain what we used to contain.</returns>
        public ReceivedMessage<TM> TransferToNewInstance()
        {
            var newInstance = GetFromPool();
            newInstance.Payload = Payload;
            AdoptExtraData(DetachExtraData());
            return newInstance;
        }

        protected override void ReturnToPool()
        {
            // Remarks: We might be tempted to use ObjectPool.actionOnRelease to perform the work done in
            // base.ReturnToPool, but that would mean that it would be done while s_instancesPool is internally locked.
            // We can easily do it outside the lock, so let's reduce contention (and it is not too bad since returning
            // to the pool is centralized through this method as this is the only way for specializing classes to do it
            // (s_InstancesPool is private)).
            base.ReturnToPool();
            if (!IsInPool)
            {
                IsInPool = true;
                s_InstancesPool.Release(this);
            }
            else
            {
                Debug.LogWarning($"Dispose called twice on {nameof(ReceivedMessage<TM>)}<{typeof(TM).Name}>");
            }
        }

        /// <summary>
        /// Pool that allow avoiding constant allocation (and garbage collection) of <see cref="ReceivedMessage{TM}"/>.
        /// </summary>
        static ConcurrentObjectPool<ReceivedMessage<TM>> s_InstancesPool = new(() => new ReceivedMessage<TM>());
    }

    /// <summary>
    /// Return value for <see cref="MessagePreprocessor"/>.
    /// </summary>
    struct PreProcessResult
    {
        /// <summary>
        /// Should the caller of the preprocess callback <see cref="IDisposable.Dispose"/> the received message because
        /// it is not needed anymore?
        /// </summary>
        public bool DisposePreProcessedMessage { get; private set; }
        /// <summary>
        /// <see cref="ReceivedMessageBase"/> that is to be passed to the next pre-process or added to the received
        /// message queue when all pre-process have been executed.
        /// </summary>
        public ReceivedMessageBase Result { get; private set; }

        /// <summary>
        /// Value to be returned by a pre-process to indicate that the received message should continue flowing through
        /// the pre-process chain.
        /// </summary>
        public static PreProcessResult PassThrough()
        {
            return new PreProcessResult(){DisposePreProcessedMessage = false, Result = null};
        }

        /// <summary>
        /// Value to be returned by a pre-process to indicate that processing of the <see cref="ReceivedMessageBase"/>
        /// should be stopped (not passed to subsequent pre-process nor added to the received message queue).
        /// </summary>
        public static PreProcessResult Stop()
        {
            return new PreProcessResult(){DisposePreProcessedMessage = true, Result = null};
        }

        /// <summary>
        /// Value to be returned by a pre-process to indicate that the pre-processed <see cref="ReceivedMessageBase"/>
        /// should be disposed of and replaces by a new one.
        /// </summary>
        public static PreProcessResult ReplaceMessage(ReceivedMessageBase newMessage)
        {
            return new PreProcessResult(){DisposePreProcessedMessage = true, Result = newMessage};
        }
    }

    /// <summary>
    /// Suggested priority for <see cref="ReceivedMessageBase"/> pre-processing.
    /// </summary>
    public static class UdpAgentPreProcessPriorityTable
    {
        // To be used for pre-processing that want to sniff received messages as soon as they are received without
        // modifying them in any way.
        public const int MessageSniffing = int.MaxValue;

        // Repeaters receive ton of FrameData fragments, so put them first in the list
        public const int FrameDataProcessing = 1000;
        // We shouldn't receive a lot, but emitter want to be able to react to retransmit request as fast as possible.
        public const int RetransmitFrameDataProcessing = 900;

        // Processing of RepeaterWaitingToStartFrame is important but not as critical as we should normally be using
        // hardware synchronization as opposed to network based synchronization...
        public const int RepeaterWaitingToStartFrame = 500;

        // Emitter placeholder should ideally "never runs" (since used in the fail over) and when it runs it does not
        // deal with that many messages that need to be processed that fast (but I guess that they should run a little
        // bit faster than the initial handshake).
        public const int EmitterPlaceholder = 300;

        // Processing of registering node is far from being as critical as the rest...
        public const int RegisteringWithEmitter = 100;
    }

    /// <summary>
    /// Interface exposing public members of <see cref="UdpAgent"/>.
    /// </summary>
    /// <remarks>Mainly done to make unit testing of other classes using <see cref="UdpAgent"/> easier and to allow
    /// better testing.</remarks>
    interface IUdpAgent
    {
        /// <summary>
        /// Address of the adapter from which we are sending / receiving messages.
        /// </summary>
        IPAddress AdapterAddress { get; }

        /// <summary>
        /// Maximum combined size for <c>Marshal.SizeOf(typeof(TM)) + additionalData.Length</c> when sending a message
        /// with the SendMessage methods.
        /// </summary>
        /// <remarks>Can be used from any thread.</remarks>
        int MaximumMessageSize { get; }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="messageType">Message type corresponding to <see cref="TM"/>.</param>
        /// <param name="message">The message to send.</param>
        /// <typeparam name="TM">Message type corresponding to messageType.</typeparam>
        /// <remarks><see cref="messageType"/> could in theory be automatically deduced, however caller will always
        /// have it ready at hand which is faster than us trying to reflection or other mechanisms to find it.</remarks>
        /// <remarks>This is a blocking call (not asynchronous) as the calling code cannot / does not need to proceed
        /// until the message is sent.</remarks>
        /// <remarks>Can be used from any thread.</remarks>
        /// <exception cref="ArgumentException">If Marshal.SizeOf(typeof(TM)) > MaximumMessageSize</exception>
        void SendMessage<TM>(MessageType messageType, TM message) where TM : unmanaged;

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="messageType">Message type corresponding to <see cref="TM"/>.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="additionalData">Additional data to be appended to the data to send immediately after
        /// <see cref="message"/></param>
        /// <typeparam name="TM">Message type corresponding to messageType.</typeparam>
        /// <remarks><paramref name="messageType"/> could in theory be automatically deduced, however caller will always
        /// have it ready at hand which is faster than us trying to reflection or other mechanisms to find it.</remarks>
        /// <remarks>This is a blocking call (not asynchronous) as the calling code cannot / does not need to proceed
        /// until the message is sent.</remarks>
        /// <remarks>Can be used from any thread.</remarks>
        /// <exception cref="ArgumentException">If Marshal.SizeOf(typeof(TM)) + additionalData.Length >
        /// MaximumMessageSize</exception>
        void SendMessage<TM>(MessageType messageType, TM message, NativeArray<byte>.ReadOnly additionalData)
            where TM : unmanaged;

        /// <summary>
        /// Gets the next received message.
        /// </summary>
        /// <returns>Next received message.</returns>
        /// <remarks>The call will block if no message are current waiting to be consumed.</remarks>
        /// <remarks>The caller of this method shall call the <see cref="IDisposable.Dispose"/> method of the object
        /// when done of it to return it to a free pool so that it can be re-used next time a message of the same type
        /// is received.</remarks>
        /// <remarks>Can be used from any thread.</remarks>
        ReceivedMessageBase ConsumeNextReceivedMessage();

        /// <summary>
        /// Tries to get the next received message.
        /// </summary>
        /// <returns>Next received message or null if no received message are waiting in the queue.</returns>
        /// <remarks>The caller of this method shall call the <see cref="IDisposable.Dispose"/> method of the object
        /// when done of it to return it to a free pool so that it can be re-used next time a message of the same type
        /// is received.</remarks>
        /// <remarks>Can be used from any thread.</remarks>
        ReceivedMessageBase TryConsumeNextReceivedMessage();

        /// <summary>
        /// Tries to get the next received message and wait the specified time period.
        /// </summary>
        /// <param name="timeout">Maximum amount of time to wait for a received message.</param>
        /// <returns>Next received message or null if no received message are waiting in the queue.</returns>
        /// <remarks>The caller of this method shall call the <see cref="IDisposable.Dispose"/> method of the object
        /// when done of it to return it to a free pool so that it can be re-used next time a message of the same type
        /// is received.</remarks>
        /// <remarks>Can be used from any thread.</remarks>
        ReceivedMessageBase TryConsumeNextReceivedMessage(TimeSpan timeout);

        /// <summary>
        /// Number of received <see cref="ReceivedMessageBase"/> waiting to be consumed.
        /// </summary>
        /// <remarks>Can be used from any thread.</remarks>
        int ReceivedMessagesCount { get; }

        /// <summary>
        /// Message types that the <see cref="UdpAgent"/> has been configured to receive.
        /// </summary>
        /// <remarks>Can be used from any thread and does not change during execution.</remarks>
        MessageType[] ReceivedMessageTypes { get; }

        /// <summary>
        /// Adds a <see cref="MessagePreprocessor"/> to the list of processing to be applied to
        /// <see cref="ReceivedMessageBase"/> before being added to queue of received messages.
        /// </summary>
        /// <param name="priority">Priority of the pre-processing being added wrt the other pre-process.  Higher
        /// priority pre-processing will be performed first.  It is strongly recommended to regroup all priorities in
        /// <see cref="UdpAgentPreProcessPriorityTable"/>.</param>
        /// <param name="preProcessor">Function to be executed to pre-process the message.</param>
        /// <remarks>Can be used from any thread.</remarks>
        void AddPreProcess(int priority, MessagePreprocessor preProcessor);

        /// <summary>
        /// Removes a <see cref="MessagePreprocessor"/> from the list of processing to be applied to
        /// <see cref="ReceivedMessageBase"/> before being added to queue of received messages.
        /// </summary>
        /// <param name="preProcessor">Function previously added to the list by calling <see cref="AddPreProcess"/> to
        /// be removed from the list.</param>
        /// <remarks>Can be used from any thread.</remarks>
        void RemovePreProcess(MessagePreprocessor preProcessor);

        /// <summary>
        /// Various statistics about networking.
        /// </summary>
        NetworkStatistics Stats { get; }

        /// <summary>
        /// Clone this UDP agent into a new one with the same settings but that will handle a different set of received
        /// messages.
        /// </summary>
        /// <param name="receivedMessageTypes">New received messages supported.</param>
        IUdpAgent Clone(MessageType[] receivedMessageTypes);
    }

    static class UdpAgentExtensions
    {
        /// <summary>
        /// Consume messages until the specified predicate is met.
        /// </summary>
        /// <param name="extendedAgent">The <see cref="IUdpAgent"/> to extend with this method.</param>
        /// <param name="timeout">For how long to try to get a message.</param>
        /// <param name="predicate">Predicate function to refuse / accept received messages.</param>
        /// <typeparam name="T">Type of message to get.</typeparam>
        [CanBeNull]
        public static ReceivedMessage<T> ConsumeMessagesUntil<T>(this IUdpAgent extendedAgent, TimeSpan timeout,
            Func<ReceivedMessage<T>, bool> predicate) where T: unmanaged
        {
            long deadline = StopwatchUtils.TimestampIn(timeout);
            do
            {
                var consumedMessage = extendedAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(deadline));
                if (consumedMessage is ReceivedMessage<T> consumedOfType)
                {
                    if (predicate(consumedOfType))
                    {
                        return consumedOfType;
                    }
                    consumedMessage.Dispose();
                }
            } while (Stopwatch.GetTimestamp() < deadline);

            return null;
        }
    }
}
