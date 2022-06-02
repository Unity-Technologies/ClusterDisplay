﻿using System;
using System.Net;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Pool;

using MessagePreprocessor = System.Func<Unity.ClusterDisplay.ReceivedMessageBase, Unity.ClusterDisplay.ReceivedMessageBase>;
using ReturnExtraDataCallback = System.Action<byte[]>;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Ways for a <see cref="IReceivedMessageExtraData"/> to deal with data.
    /// </summary>
    enum ExtraDataFormat
    {
        ManagedArray,
        NativeArray
    }

    /// <summary>
    /// Interface for extra data associated to <see cref="ReceivedMessageBase"/>.
    /// </summary>
    interface IReceivedMessageExtraData
    {
        /// <summary>
        /// Most efficient format for the <see cref="IReceivedMessageExtraData"/> to return the data it contains.
        /// </summary>
        ExtraDataFormat PreferredFormat { get; }

        /// <summary>
        /// Returns the length of the data in the extra data.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Returns the extra data as a managed array.
        /// </summary>
        /// <param name="array">Array containing the extra data.  Content of the array shall not be modified.</param>
        /// <param name="extraDataStart">First byte in array that contains the extra data.  Data before that index is
        /// undefined.</param>
        /// <param name="extraDataLength">Length of the extra data (starting at <see cref="extraDataStart"/>).</param>
        /// <remarks>Some implementation of the method might have to copy the data (to a managed array) but result
        /// will be cashed for cases where the method would be called multiple times.</remarks>
        void AsManagedArray(out byte[] array, out int extraDataStart, out int extraDataLength);

        /// <summary>
        /// Returns the extra data as a native array.
        /// </summary>
        /// <returns>The native array.</returns>
        /// <remarks>Some implementation of the method might have to copy the data (to a NativeArray) but result will be
        /// cashed for cases where the method would be called multiple times.</remarks>
        NativeArray<byte> AsNativeArray();
    }

    /// <summary>
    /// Helpers for <see cref="IReceivedMessageExtraData"/>.
    /// </summary>
    static class ReceivedMessageExtraDataExtensions
    {
        /// <summary>
        /// Wrapper around <see cref="IReceivedMessageExtraData.AsManagedArray"/>.
        /// </summary>
        /// <param name="extraData">The <see cref="IReceivedMessageExtraData"/>.</param>
        /// <returns>A <see cref="Span{T}"/>.</returns>
        public static Span<byte> ToSpan(this IReceivedMessageExtraData extraData)
        {
            extraData.AsManagedArray(out var array, out var dataStart, out var dataLength);
            return new Span<byte>(array, dataStart, dataLength);
        }
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
        public IReceivedMessageExtraData ExtraData { get => m_ExtraData; }

        /// <summary>
        /// Initialize the ReceivedMessageBase with <see cref="IReceivedMessageExtraData"/>.
        /// </summary>
        /// <param name="extraData">The <see cref="IReceivedMessageExtraData"/> to initialize the
        /// <see cref="ReceivedMessageBase"/> with.</param>
        /// <remarks>The <see cref="ReceivedMessageBase"/> will take "ownership" of the extra data and dispose of it
        /// when the <see cref="ReceivedMessageBase"/> is disposed of.</remarks>
        internal void InitializeWithExtraData(IReceivedMessageExtraData extraData)
        {
            (m_ExtraData as IDisposable)?.Dispose();
            m_ExtraData = extraData;
        }

        /// <summary>
        /// Returns the current <see cref="ExtraData"/> of this <see cref="ReceivedMessageBase"/> and remove it from this
        /// <see cref="ReceivedMessageBase"/> so that it does not get disposed when <see cref="IDisposable.Dispose"/> is
        /// called on this object.
        /// </summary>
        /// <returns></returns>
        internal IReceivedMessageExtraData DetachExtraData()
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
        /// Method to be called when done of the ReceivedMessageBase to return it to the pool it belongs to.
        /// </summary>
        protected virtual void ReturnToPool()
        {
            var disposableExtraData = m_ExtraData as IDisposable;
            if (disposableExtraData != null)
            {
                disposableExtraData.Dispose();
                m_ExtraData = null;
            }
        }

        /// <summary>
        /// Used to detect double pooling (calling <see cref="IDisposable.Dispose"/> twice).
        /// </summary>
        protected bool IsInPool { get; set; } = true; // Default to true since it should be created by the ObjectPool

        /// <summary>
        /// Extra data (bytes) that were present after the <see cref="ReceivedMessage{TM}.Payload"/> on the network.
        /// </summary>
        IReceivedMessageExtraData m_ExtraData;
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
            lock (s_InstancesPool)
            {
                var ret = s_InstancesPool.Get();
                Debug.Assert(ret.IsInPool);
                ret.IsInPool = false;
                return ret;
            }
        }

        /// <summary>
        /// Returns a <see cref="ReceivedMessage{M}"/> instance by reusing previous instances returned to a free pool
        /// when the <see cref="IDisposable.Dispose"/> method is called and fill it from the given byte array.
        /// </summary>
        /// <param name="fillFrom">Bytes to fill the message from (length must match sizeof(M))</param>
        /// <returns>The <see cref="ReceivedMessage{M}"/> instance and the amount of data from <see cref="fillFrom"/>
        /// that was not used to fill the <see cref="ReceivedMessage{M}"/>.</returns>
        /// <remarks>Will automatically allocate a new one if there are no free instances in the pool.</remarks>
        /// <exception cref="ArgumentException">If length of <see cref="fillFrom"/> does not match length of
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
            InitializeWithExtraData(DetachExtraData());
            return newInstance;
        }

        protected override void ReturnToPool()
        {
            base.ReturnToPool();
            lock (s_InstancesPool)
            {
                if (!IsInPool)
                {
                    s_InstancesPool.Release(this);
                    IsInPool = true;
                }
                else
                {
                    Debug.LogWarning($"Dispose called twice on {nameof(ReceivedMessage<TM>)}<{typeof(TM).Name}>");
                }
            }
        }

        /// <summary>
        /// Pool that allow avoiding constant allocation (and garbage collection) of <see cref="ReceivedMessage{TM}"/>.
        /// </summary>
        static ObjectPool<ReceivedMessage<TM>> s_InstancesPool = new(() => new ReceivedMessage<TM>());
    }

    /// <summary>
    /// Interface exposing public members of <see cref="UDPAgent"/>.
    /// </summary>
    /// <remarks>Mainly done to make unit testing of other classes using <see cref="UDPAgent"/> easier and to allow
    /// better testing.</remarks>
    interface IUDPAgent
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
        /// <remarks><see cref="messageType"/> could in theory be automatically deduced, however caller will always
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
        /// Message types that the <see cref="UDPAgent"/> has been configured to receive.
        /// </summary>
        /// <remarks>Can be used from any thread and does not change during execution.</remarks>
        MessageType[] ReceivedMessageTypes { get; }

        /// <summary>
        /// Callbacks that are called as we received new messages.
        /// </summary>
        /// <remarks>They can modify the message, create a new one (and return the new one) or even stop reception of
        /// the message (by calling <see cref="IDisposable.Dispose"/> and returning null).</remarks>
        /// <remarks>Processing of received messages should be kept to a minimum as any message taking longer than it
        /// should to preprocess will stall all other incoming messages.</remarks>
        /// <remarks>Can be used from any thread.</remarks>
        event MessagePreprocessor OnMessagePreProcess;

        /// <summary>
        /// Various statistics about networking.
        /// </summary>
        NetworkStatistics Stats { get; }
    }
}
