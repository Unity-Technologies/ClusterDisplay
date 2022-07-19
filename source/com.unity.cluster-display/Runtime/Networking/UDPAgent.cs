using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using Utils;
using Debug = UnityEngine.Debug;
using MessagePreprocessor = System.Func<Unity.ClusterDisplay.ReceivedMessageBase, Unity.ClusterDisplay.PreProcessResult>;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Configuration parameters for <see cref="UdpAgent"/>.
    /// </summary>
    struct UdpAgentConfig
    {
        /// <summary>
        /// Multicast IP address to which every multicast messages are being sent.
        /// </summary>
        public IPAddress MulticastIp;

        /// <summary>
        /// The port to which we are sending messages and on which we are receiving messages (same for both).
        /// </summary>
        public int Port;

        /// <summary>
        /// Network adapter name. If null or empty, the adapter will be selected automagically.
        /// </summary>
        public string AdapterName;

        /// <summary>
        /// Types of message we support receiving (other type of messages are simply discarded as soon as received).
        /// </summary>
        /// <remarks>The whole receiving thread logic will not be present if null or empty.</remarks>
        public MessageType[] ReceivedMessagesType;
    }

    /// <summary>
    /// Class taking care of the network access for Cluster Display.
    /// </summary>
    /// <remarks>This class does not handle any loss detection or retransmission logic, this is to be done by the user
    /// of this class.</remarks>
    class UdpAgent: IDisposable, IUdpAgent
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">Networking configuration</param>
        /// <exception cref="IOException">When no available network interface can be found</exception>
        public UdpAgent(UdpAgentConfig config)
        {
            var (selectedNic, selectedNicAddress) = SelectNetworkInterface(config.AdapterName);
            if (selectedNic == null)
            {
                throw new IOException("There are no available network interfaces that support Cluster Display");
            }
            Assert.IsNotNull(selectedNicAddress);
            AdapterAddress = selectedNicAddress.Address;

            try
            {
                m_Mtu = selectedNic.GetIPProperties().GetIPv4Properties().Mtu;
            }
            catch (Exception)
            {
                // For some reason GetIPv4Properties throw with a NullReferenceException when running on Yamato, let's
                // assume a default MTU size if anything goes wrong.
                m_Mtu = 1400;
            }
            MaximumMessageSize = m_Mtu - 1 /*for the Message type*/;

            // Create the UdpClient
            m_UdpClient = new();
            // Set buffers to ushort.MaxValue.  Maybe we can have better values than that, but old code was doing it and
            // so far tests shows it gives good results, so let's keep it this way.
            m_UdpClient.Client.SendBufferSize = ushort.MaxValue;
            m_UdpClient.Client.ReceiveBufferSize = ushort.MaxValue;
            // Ask to send the multicast traffic using the specified adapter
            m_UdpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
                AdapterAddress.GetAddressBytes());
            // There should be only one hop between the emitter and repeater
            // Food for thought: Should we make this configurable to work on slightly more complex network infrastructures?
            m_UdpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            // Allow multiple ClusterDisplay applications to bind on the same address and port. Useful for when running
            // multiple nodes locally and unit testing.
            // Food for thought: Does it have a performance cost? Do we want to have it configurable or disabled in some
            //                   cases?
            m_UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            // Bind to receive from the selected adapter on the same port than the port we are sending to (everyone will
            // use the same port).
            m_UdpClient.Client.Bind(new IPEndPoint(AdapterAddress, config.Port));
            // Join the multicast group
            m_UdpClient.JoinMulticastGroup(config.MulticastIp);
            // This is normally true by default but this is required to keep things simple (unit tests working, multiple
            // instances on the same computer, ...).  So let's get sure it is on.
            m_UdpClient.MulticastLoopback = true;

            // Other member variables used to send datagrams
            m_MulticastTxEndpoint = new IPEndPoint(config.MulticastIp, config.Port);
            m_SendTempBuffers = new(() => new byte[m_Mtu]);

            // Prepare reception of messages
            if (SetupReceiveMessageFactory(config.ReceivedMessagesType))
            {
                m_ReceivedMessageDataPool = new(() => new ManagedReceivedMessageData(this, m_Mtu));
                m_ReceiveThread = new Thread(ProcessIncomingDatagrams);
                m_ReceiveThread.Name = $"ClusterDisplay UDP Reception {AdapterAddress}:{config.Port}";
                m_ReceiveThread.Start();
            }
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            m_ReceiveThreadShouldStop = true;
            m_UdpClient?.Dispose(); // Interrupts any currently going on call to Receive in m_ReceiveThread
            m_ReceiveThread?.Join();
        }

        public IPAddress AdapterAddress { get; private set; }

        public int MaximumMessageSize { get; private set; }

        public void SendMessage<TM>(MessageType messageType, TM message) where TM: unmanaged
        {
            SendMessage(messageType, message, s_EmptyNativeArray.AsReadOnly());
        }

        public void SendMessage<TM>(MessageType messageType, TM message, NativeArray<byte>.ReadOnly additionalData) where TM: unmanaged
        {
            // Remarks, Assert below check for profiler otherwise GetTypeOf generate tons of GC Alloc that makes
            // profiling unnecessarily alarming.
            Assert.IsTrue(Profiler.enabled || MessageTypeAttribute.GetTypeOf<TM>() == messageType);
            if (Marshal.SizeOf(typeof(TM)) + sizeof(MessageType) + additionalData.Length > m_Mtu)
            {
                throw new ArgumentException("Marshal.SizeOf(typeof(TM)) + additionalData.Length > MaximumMessageSize.");
            }

            byte[] sendTempBuffer = m_SendTempBuffers.Get();
            try
            {
                sendTempBuffer[0] = (byte)messageType;
                int toSendSize = 1;
                toSendSize += message.StoreInBuffer(sendTempBuffer, 1);
                if (additionalData.Length > 0)
                {
                    NativeArray<byte>.Copy(additionalData, 0, sendTempBuffer, toSendSize, additionalData.Length);
                    toSendSize += additionalData.Length;
                }

                // Remark: Sad but the following call perform some heap allocation (+/- 104 bytes per send).  AFAIK there
                // is no way to avoid it.  Probably related to this suggested improvement to .Net:
                // https://github.com/dotnet/runtime/issues/30797
                m_UdpClient.Send(sendTempBuffer, toSendSize, m_MulticastTxEndpoint);

                Stats.MessageSent(messageType);
                LogMessage(message, true, additionalData.Length);
            }
            finally
            {
                m_SendTempBuffers.Release(sendTempBuffer);
            }
        }

        public ReceivedMessageBase ConsumeNextReceivedMessage()
        {
            return m_ReceivedMessages.Dequeue();
        }

        public ReceivedMessageBase TryConsumeNextReceivedMessage()
        {
            return m_ReceivedMessages.TryDequeue(out var ret) ? ret : null;
        }

        public ReceivedMessageBase TryConsumeNextReceivedMessage(TimeSpan timeout)
        {
            return m_ReceivedMessages.TryDequeue(out var ret, timeout) ? ret : null;
        }

        public int ReceivedMessagesCount => m_ReceivedMessages.Count;

        public MessageType[] ReceivedMessageTypes { get; private set; }

        public void AddPreProcess(int priority, MessagePreprocessor preProcessor)
        {
            lock (m_MessagePreprocessors)
            {
                m_MessagePreprocessors.Add((priority, preProcessor));
                UpdateSortedMessagePreprocessors();
            }
        }

        public void RemovePreProcess(MessagePreprocessor preProcessor)
        {
            lock (m_MessagePreprocessors)
            {
                var index = m_MessagePreprocessors.FindIndex(tuple => tuple.Item2 == preProcessor);
                if (index >= 0)
                {
                    m_MessagePreprocessors.RemoveAt(index);
                    UpdateSortedMessagePreprocessors();
                }
            }
        }

        public NetworkStatistics Stats { get; } = new NetworkStatistics();

        /// <summary>
        /// Returns the network adapter to use for data transmission and reception.
        /// </summary>
        /// <param name="adapterName">Provided network adapter name.</param>
        /// <returns>The selected network interface and the IP address identifying it.</returns>
        static (NetworkInterface, UnicastIPAddressInformation) SelectNetworkInterface(string adapterName)
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            NetworkInterface firstUpNic = null;
            UnicastIPAddressInformation firstUpNicIPAddress = null;

            foreach (var nic in nics)
            {
                ClusterDebug.Log($"Polling interface: \"{nic.Name}\".");

                bool isExplicitNic = !string.IsNullOrEmpty(adapterName) && nic.Name == adapterName;
                bool isUp = nic.OperationalStatus == OperationalStatus.Up;

                if (!isUp)
                {
                    if (isExplicitNic)
                    {
                        ClusterDebug.LogError(
                            $"Unable to use explicit interface: \"{nic.Name}\", the interface is down. Attempting to use the next available interface.");
                    }

                    continue;
                }

                var ipProperties = nic.GetIPProperties();
                if (ipProperties == null || !ipProperties.MulticastAddresses.Any())
                {
                    continue;
                }

                if (!nic.SupportsMulticast)
                {
                    continue;
                }

                UnicastIPAddressInformation nicIPAddress = null;
                foreach (var ip in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    nicIPAddress = ip;
                    break;
                }

                if (nicIPAddress == null)
                {
                    continue;
                }

                if (IPAddress.IsLoopback(nicIPAddress.Address))
                {
                    // Skip loopback adapter as they cause all sort of problems with multicast...
                    continue;
                }

                firstUpNic ??= nic;
                firstUpNicIPAddress ??= nicIPAddress;
                if (!isExplicitNic)
                {
                    continue;
                }

                ClusterDebug.Log($"Selecting explicit interface: \"{nic.Name} with ip {nicIPAddress.Address}\".");
                return (nic, nicIPAddress);
            }

            // If we reach this point then there was no explicit nic selected, use the first up nic as the automatic one
            if (firstUpNic == null)
            {
                ClusterDebug.LogError($"There are NO available interfaces to bind cluster display to.");
                return (null, null);
            }

            ClusterDebug.Log($"No explicit interface defined, defaulting to interface: \"{firstUpNic.Name}\".");
            return (firstUpNic, firstUpNicIPAddress);
        }

        /// <summary>
        /// Setup the factory responsible for creating the different <see cref="ReceivedMessageBase"/> based on the
        /// <see cref="MessageType"/> of the received messages.
        /// </summary>
        /// <param name="receivedMessagesType">Type of <see cref="ReceivedMessageBase"/> that we should properly
        /// reception and make available to caller of the <see cref="ConsumeNextReceivedMessage()"/> method.</param>
        /// <returns>Do we have any MessageType to receive?</returns>
        bool SetupReceiveMessageFactory(MessageType[] receivedMessagesType)
        {
            if (receivedMessagesType == null)
            {
                return false;
            }

            var getFromPoolArgTypes = new[] {typeof(ReadOnlySpan<byte>)};
            var messageTypes =
                Enum.GetValues(typeof(MessageType)).Cast<MessageType>().OrderBy( mt => (int)mt );
            m_FactoryByMessageType = new MessageFactory[(int)messageTypes.Last() + 1];
            HashSet<MessageType> receivedMessageTypes = new();
            foreach ((var messageType, var messageTypeAttribute) in MessageTypeAttribute.AllTypes)
            {
                var messageTypeEnum = messageTypeAttribute.Type;
                if (receivedMessagesType.Contains(messageTypeEnum) && !receivedMessageTypes.Contains(messageTypeEnum))
                {
                    var genericInstanceType = typeof(ReceivedMessage<>).MakeGenericType(messageType);
                    var getFromPoolMethodInfo = genericInstanceType.GetMethod("GetFromPool", getFromPoolArgTypes);
                    if (getFromPoolMethodInfo != null)
                    {
                        m_FactoryByMessageType[(int)messageTypeEnum] =
                            (MessageFactory)Delegate.CreateDelegate(typeof(MessageFactory), getFromPoolMethodInfo);
                        receivedMessageTypes.Add(messageTypeEnum);
                    }
                }
            }

            if (receivedMessageTypes.Count > 0)
            {
                ReceivedMessageTypes = receivedMessageTypes.OrderBy(mt => (int)mt).ToArray();
                return true;
            }
            else
            {
                m_FactoryByMessageType = null;
                return false;
            }
        }

        /// <summary>
        /// Datagram reception thread function (looping until UdpAgentConfig is disposed of).
        /// </summary>
        void ProcessIncomingDatagrams()
        {
            ManagedReceivedMessageData receiveReceivedMessageData = m_ReceivedMessageDataPool.Get();

            while (!m_ReceiveThreadShouldStop)
            {
                // Receive the next datagram
                int receivedLength;
                using (s_MarkerReceive.Auto())
                {
                    try
                    {
                        receivedLength = m_UdpClient.Client.Receive(receiveReceivedMessageData.RawArray);
                        if (receivedLength < 1)
                        {
                            // Empty message, strange, let's just skip it...
                            continue;
                        }
                    }
                    catch (SocketException e)
                    {
                        if (e.SocketErrorCode == SocketError.Interrupted && m_ReceiveThreadShouldStop)
                        {
                            // We are shutting down, this is perfectly normal, just continue
                            continue;
                        }
                        else
                        {
                            Debug.LogError($"Socket.Receive error: {e}");
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Socket.Receive error: {e}");
                        continue;
                    }
                }

                ReceivedMessageBase receivedMessage;
                using (s_MarkerCreateMessage.Auto())
                {
                    // Get the factory for it
                    var messageTypeByte = receiveReceivedMessageData.RawArray[0];
                    var receivedFactory = messageTypeByte < m_FactoryByMessageType.Length ? m_FactoryByMessageType[messageTypeByte] : null;
                    if (receivedFactory == null)
                    {
                        // This is perfectly normal if reception of that message type is not enabled, just skip it and say
                        // nothing...
                        continue;
                    }

                    Stats.MessageReceived((MessageType)messageTypeByte);

                    // Create the message
                    try
                    {
                        int leftOver;
                        (receivedMessage, leftOver) =
                            receivedFactory(new ReadOnlySpan<byte>(receiveReceivedMessageData.RawArray, 1, receivedLength - 1));
                        if (receivedMessage == null)
                        {
                            // A "normal failure" in the factory?  Let's continue...
                            continue;
                        }

                        if (leftOver > 0)
                        {
                            // Instead of copying data around, move receiveReceivedMessageData to the receivedMessage and
                            // get a new receiveReceivedMessageData.
                            receiveReceivedMessageData.SetRange(receivedLength - leftOver, leftOver);
                            receivedMessage.AdoptExtraData(receiveReceivedMessageData);

                            receiveReceivedMessageData = m_ReceivedMessageDataPool.Get();
                        }

                        LogReceivedMessage(receivedMessage);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to parse message of type {messageTypeByte}: {e}");
                        continue;
                    }
                }

                // Preprocess the message
                using (s_MarkerPreprocessMessage.Auto())
                {
                    using (var arrayLock = m_SortedMessagePreprocessors.Lock())
                    {
                        foreach (var preprocessor in arrayLock.GetArray())
                        {
                            try
                            {
                                var ret = preprocessor(receivedMessage);
                                if (ret.DisposePreProcessedMessage)
                                {
                                    Debug.Assert(!ReferenceEquals(ret.Result, receivedMessage),
                                        "Cannot dispose of a ReceivedMessage AND ask to continue processing it...");
                                    receivedMessage.Dispose();
                                    receivedMessage = ret.Result;
                                }
                                else if (ret.Result != null)
                                {
                                    receivedMessage = ret.Result;
                                }

                                if (receivedMessage == null)
                                {
                                    break;
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogError($"Unexpected exception pre-processing received messages: {e}");
                            }
                        }
                    }

                    if (receivedMessage == null)
                    {
                        // Looks like a pre-process discarded the message, let's move on to the next one
                        continue;
                    }
                }

                // At last we are ready to queue the message
                using (s_MarkerQueueMessage.Auto())
                {
                    m_ReceivedMessages.Enqueue(receivedMessage);
                }
            }

            receiveReceivedMessageData?.Release();
        }

        /// <summary>
        /// Returns a <see cref="ManagedReceivedMessageData"/> to the pool.
        /// </summary>
        /// <param name="toReturn">To return to the pool.</param>
        void ReturnManagedReceivedMessageData(ManagedReceivedMessageData toReturn)
        {
            m_ReceivedMessageDataPool.Release(toReturn);
        }

        /// <summary>
        /// Update sorted array of <see cref="MessagePreprocessor"/> to the given list.
        /// </summary>
        void UpdateSortedMessagePreprocessors()
        {
            lock (m_MessagePreprocessors)
            {
                using var lockedArray = m_SortedMessagePreprocessors.Lock();
                lockedArray.SetArray(m_MessagePreprocessors.OrderBy(p => p.Item1).
                    Select(p => p.Item2).ToArray());
            }
        }

        /// <summary>
        /// <see cref="IReceivedMessageData"/> that is used to receive all the packets and to be transferred as a
        /// <see cref="ReceivedMessageBase.ExtraData"/> if there is some additional data after the
        /// <see cref="ReceivedMessage{TM}.Payload"/>.
        /// </summary>
        class ManagedReceivedMessageData: IReceivedMessageData
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="owner">Owning <see cref="UdpAgent"/> that contains the pool to which we should be
            /// returned to.</param>
            /// <param name="size">Size in bytes of the byte[] contained in this class.</param>
            public ManagedReceivedMessageData(UdpAgent owner, int size)
            {
                m_Owner = owner;
                m_Bytes = new byte[size];
            }

            /// <summary>
            /// Raw access to the byte array contained in this ManagedReceivedMessageData
            /// </summary>
            public byte[] RawArray => m_Bytes;

            /// <summary>
            /// Sets the range of the managed memory that forms the area of the byte[] to be considered by methods of
            /// <see cref="IReceivedMessageData"/>.
            /// </summary>
            /// <param name="dataStart">First byte in array that contains the data.</param>
            /// <param name="dataLength">Length of the data (starting at <paramref name="dataStart"/>).</param>
            public void SetRange(int dataStart, int dataLength)
            {
                if (dataStart + dataLength > m_Bytes.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(dataLength),
                        "dataStart + dataLength > size of the ManagedReceivedMessageData.");
                }
                m_DataStart = dataStart;
                m_DataLength = dataLength;
                if (m_NativeArray.IsCreated)
                {
                    m_NativeArray.Dispose();
                }
            }

            public ReceivedMessageDataFormat PreferredFormat => ReceivedMessageDataFormat.ManagedArray;

            public int Length => m_DataLength;

            public void AsManagedArray(out byte[] array, out int dataStart, out int dataLength)
            {
                array = m_Bytes;
                dataStart = m_DataStart;
                dataLength = m_DataLength;
            }

            public NativeArray<byte> AsNativeArray()
            {
                if (!m_NativeArray.IsCreated)
                {
                    m_NativeArray = new NativeArray<byte>(m_Bytes, Allocator.Temp).GetSubArray(m_DataStart, m_DataLength);
                }
                return m_NativeArray;
            }

            public void Release()
            {
                m_DataStart = 0;
                m_DataLength = 0;
                if (m_NativeArray.IsCreated)
                {
                    m_NativeArray.Dispose();
                }
                m_Owner.ReturnManagedReceivedMessageData(this);
            }

            /// <summary>
            /// Owning <see cref="UdpAgent"/> that contains the pool to which we should be returned to.
            /// </summary>
            UdpAgent m_Owner;
            /// <summary>
            /// Managed array of bytes in which the data is stored.
            /// </summary>
            byte[] m_Bytes;
            /// <summary>
            /// First byte in array that contains the data considered by methods of <see cref="IReceivedMessageData"/>.
            /// </summary>
            int m_DataStart;
            /// <summary>
            /// Length of the data (starting at <see cref="m_DataStart"/>) considered by methods of
            /// <see cref="IReceivedMessageData"/>.
            /// </summary>
            int m_DataLength;
            /// <summary>
            /// Cache for calls to AsNativeArray.
            /// </summary>
            NativeArray<byte> m_NativeArray;
        }

        /// <summary>
        /// Main UdpClient used for reception of data and sending to the Multicast endpoint (config's MulticastIp:Port).
        /// </summary>
        UdpClient m_UdpClient;
        /// <summary>
        /// MTU of the adapter used to communicate.
        /// </summary>
        int m_Mtu;
        /// <summary>
        /// <see cref="IPEndPoint"/> to which send all the multicast traffic.
        /// </summary>
        IPEndPoint m_MulticastTxEndpoint;
        /// <summary>
        /// Buffers to be used by the send message methods.
        /// </summary>
        /// <remarks>Would be nice to use <c>Span&lt;byte&gt;</c> from stackalloc in the methods that send messages, however
        /// we can't as the .net version we are targeting does not yet support sending from a <c>Span&lt;byte&gt;</c>, only
        /// from a <c>byte[]</c>.</remarks>
        ConcurrentObjectPool<byte[]> m_SendTempBuffers;
        /// <summary>
        /// Object used to stop all the asynchronous work going on when we finish.
        /// </summary>
        volatile bool m_ReceiveThreadShouldStop;
        /// <summary>
        /// Thread responsible for receiving data.
        /// </summary>
        /// <remarks>We need to process receptions of data on a dedicated thread.  This is necessary as handling
        /// retransmission of lost FrameData datagrams has to be done ASAP and cannot wait for some game loop code to
        /// ask for messages before returning missing FrameData datagrams.</remarks>
        Thread m_ReceiveThread;
        /// <summary>
        /// Queue of <see cref="ReceivedMessageBase"/> waiting to be consumed by our user.
        /// </summary>
        BlockingQueue<ReceivedMessageBase> m_ReceivedMessages = new();
        /// <summary>
        /// Non sorted list of <see cref="MessagePreprocessor"/> with their priority.
        /// </summary>
        /// <remarks>Must be locked before using it.</remarks>
        List<(int, MessagePreprocessor)> m_MessagePreprocessors = new();
        /// <summary>
        /// Array of <see cref="MessagePreprocessor"/> sorted by priority that can quickly accessed from any thread.
        /// </summary>
        ArrayWithSpinLock<MessagePreprocessor> m_SortedMessagePreprocessors = new();
        /// <summary>
        /// Custom delegate to create a <see cref="ReceivedMessage{M}"/> from a byte array (length must match sizeof(M)).
        /// </summary>
        /// <remarks>Cannot use <see cref="Func{ReadOnlySpan, ReceivedMessageBase}"/> as it does not allow using
        /// ReadOnlySpan as a generic argument.</remarks>
        delegate (ReceivedMessageBase message, int leftOver) MessageFactory(ReadOnlySpan<byte> bytes);
        /// <summary>
        /// <see cref="ReceivedMessage{M}"/> factory function indexed on <see cref="MessageType"/>.
        /// </summary>
        MessageFactory[] m_FactoryByMessageType;
        /// <summary>
        /// Pool of <see cref="ManagedReceivedMessageData"/> that allow avoiding constant allocation (and garbage collection) of
        /// byte[] used to receive the datagrams and to contain the received data.
        /// </summary>
        ConcurrentObjectPool<ManagedReceivedMessageData> m_ReceivedMessageDataPool;

        /// <summary>
        /// Empty <see cref="NativeArray{T}"/> that should never be modified.  Use to unify cases with and without
        /// additional data.
        /// </summary>
        /// <remarks>Cannot be made readonly because <see cref="NativeArray{T}.AsReadOnly"/> is not considered "pure"
        /// and do internal modifications to s_EmptyNativeArray.</remarks>
        static NativeArray<byte> s_EmptyNativeArray = new(0, Allocator.Persistent);
        /// <summary>
        /// <see cref="ProfilerMarker"/> used to identify the time spent doing actual network reception.
        /// </summary>
        static ProfilerMarker s_MarkerReceive = new ProfilerMarker("Receive");
        /// <summary>
        /// <see cref="ProfilerMarker"/> used to identify the time spent creating the <see cref="ReceivedMessage{TM}"/>
        /// from the received buffer.
        /// </summary>
        static ProfilerMarker s_MarkerCreateMessage = new ProfilerMarker("CreateMessage");
        /// <summary>
        /// <see cref="ProfilerMarker"/> used to identify the time spent pre-processing the <see cref="ReceivedMessage{TM}"/>.
        /// </summary>
        static ProfilerMarker s_MarkerPreprocessMessage = new ProfilerMarker("PreprocessMessage");
        /// <summary>
        /// <see cref="ProfilerMarker"/> used to identify the time spent adding <see cref="ReceivedMessage{TM}"/> to the
        /// received queue.
        /// </summary>
        static ProfilerMarker s_MarkerQueueMessage = new ProfilerMarker("QueueMessage");

        [Conditional("CLUSTER_DISPLAY_NETWORK_LOG")]
        static void LogReceivedMessage(ReceivedMessageBase receivedMessage)
        {
            int extraDataLength = receivedMessage.ExtraData?.Length ?? 0;
            switch (receivedMessage.Type)
            {
            case MessageType.RegisteringWithEmitter:
                LogMessage(((ReceivedMessage<RegisteringWithEmitter>)receivedMessage).Payload, false, extraDataLength);
                break;
            case MessageType.RepeaterRegistered:
                LogMessage(((ReceivedMessage<RepeaterRegistered>)receivedMessage).Payload, false, extraDataLength);
                break;
            case MessageType.FrameData:
                LogMessage(((ReceivedMessage<FrameData>)receivedMessage).Payload, false, extraDataLength);
                break;
            case MessageType.RetransmitFrameData:
                LogMessage(((ReceivedMessage<RetransmitFrameData>)receivedMessage).Payload, false, extraDataLength);
                break;
            case MessageType.RepeaterWaitingToStartFrame:
                LogMessage(((ReceivedMessage<RepeaterWaitingToStartFrame>)receivedMessage).Payload, false, extraDataLength);
                break;
            case MessageType.EmitterWaitingToStartFrame:
                LogMessage(((ReceivedMessage<EmitterWaitingToStartFrame>)receivedMessage).Payload, false, extraDataLength);
                break;
            }
        }

        [Conditional("CLUSTER_DISPLAY_NETWORK_LOG")]
        static void LogMessage(object message, bool send, int extraDataLength)
        {
            var stringBuilder = new StringBuilder();
            var messageTime = (double)(Stopwatch.GetTimestamp() - k_StartTime) / Stopwatch.Frequency;
            stringBuilder.AppendFormat("{0:0.0000}", messageTime);
            stringBuilder.Append(send ? ", Send " : ", Recv ");
            switch (message)
            {
            case RegisteringWithEmitter registering:
                {
                    var bytes = BitConverter.GetBytes(registering.IPAddressBytes);
                    stringBuilder.AppendFormat("RegisteringWithEmitter     : NodeId = {0}, IPAddress = {1}.{2}.{3}.{4}",
                        registering.NodeId, bytes[0], bytes[1], bytes[2], bytes[3] );
                }
                break;
            case RepeaterRegistered registered:
                {
                    var bytes = BitConverter.GetBytes(registered.IPAddressBytes);
                    stringBuilder.AppendFormat("RepeaterRegistered         : NodeId = {0}, IPAddress = " +
                        "{1}.{2}.{3}.{4}, Accepted = {5}",
                        registered.NodeId, bytes[0], bytes[1], bytes[2], bytes[3], registered.Accepted);
                }
                break;
            case FrameData frameData:
                stringBuilder.AppendFormat("FrameData                  : FrameIndex = {0}, DataLength = {1}, " +
                    "DatagramIndex = {2}, DatagramDataOffset = {3}, ExtraDataLength = {4}",
                    frameData.FrameIndex, frameData.DataLength, frameData.DatagramIndex, frameData.DatagramDataOffset,
                    extraDataLength);
                break;
            case RetransmitFrameData retransmit:
                stringBuilder.AppendFormat("RetransmitFrameData        : FrameIndex = {0}, " +
                    "DatagramIndexIndexStart = {1}, DatagramIndexIndexEnd = {2}",
                    retransmit.FrameIndex, retransmit.DatagramIndexIndexStart, retransmit.DatagramIndexIndexEnd);
                break;
            case RepeaterWaitingToStartFrame repeaterWaiting:
                stringBuilder.AppendFormat("RepeaterWaitingToStartFrame: FrameIndex = {0}, NodeId = {1}, " +
                    "WillUseNetworkSyncOnNextFrame = {2}",
                    repeaterWaiting.FrameIndex, repeaterWaiting.NodeId, repeaterWaiting.WillUseNetworkSyncOnNextFrame);
                break;
            case EmitterWaitingToStartFrame emitterWaiting:
                var waitingOn = new NodeIdBitVectorReadOnly(emitterWaiting.WaitingOn0, emitterWaiting.WaitingOn1,
                    emitterWaiting.WaitingOn2, emitterWaiting.WaitingOn3);
                stringBuilder.AppendFormat("EmitterWaitingToStartFrame : FrameIndex = {0}, " +
                    "WaitingNodesBitField = {1}",
                    emitterWaiting.FrameIndex, waitingOn);
                break;
            }

            lock (s_LogThreadLock)
            {
                if (s_LogThread == null)
                {
                    s_LogThread = new Thread(WriteMessageLogThreadFunc);
                    s_LogThread.Start();
                }
            }
            s_LogQueue.Add(stringBuilder.ToString());
        }

        static void WriteMessageLogThreadFunc()
        {
            var logFilePath = Path.Combine(Environment.GetEnvironmentVariable("AppData"), "..", "LocalLow",
                k_CompanyName, k_ProductName, "Network.log");
            using StreamWriter file = new(logFilePath);
            for (;;)
            {
                var line = s_LogQueue.Take();
                file.WriteLine(line);
            }
        }

        static readonly long k_StartTime = Stopwatch.GetTimestamp();
        static readonly string k_CompanyName = Application.companyName; // Done here since it can only be called from
        static readonly string k_ProductName = Application.productName; // the main thread.
        static object s_LogThreadLock = new ();
        static BlockingCollection<string> s_LogQueue = new(new ConcurrentQueue<string>());
        static Thread s_LogThread;
    }
}
