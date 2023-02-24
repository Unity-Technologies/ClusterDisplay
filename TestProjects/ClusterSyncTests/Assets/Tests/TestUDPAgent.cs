using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using JetBrains.Annotations;
using Unity.Collections;
using Utils;
using Debug = UnityEngine.Debug;
using MessagePreprocessor = System.Func<Unity.ClusterDisplay.ReceivedMessageBase, Unity.ClusterDisplay.PreProcessResult>;

namespace Unity.ClusterDisplay.Tests
{
    /// <summary>
    /// Represent a fictive network to which multiple TestUdpAgent are connected.
    /// </summary>
    class TestUdpAgentNetwork
    {
        public List<TestUdpAgent> Agents { get; } = new();
    }

    /// <summary>
    /// Dummy <see cref="IUdpAgent"/> that does not really transmit and where everything sent is automatically received.
    /// </summary>
    class TestUdpAgent: IUdpAgent
    {
        public TestUdpAgent(TestUdpAgentNetwork network, MessageType[] receivedMessageTypes)
        {
            m_Network = network;
            network.Agents.Add(this);
            ReceivedMessageTypes = receivedMessageTypes;
            AdapterAddress = IPAddress.Parse("1.2.3." + network.Agents.Count);
        }

        public IPAddress AdapterAddress { get; }

        public int MaximumMessageSize { get; } = 1257; // Some random number, a little bit smaller than what we can expect to have

        public void SendMessage<TM>(MessageType messageType, TM message) where TM : unmanaged
        {
            SendMessage(messageType, message, s_EmptyNativeArray.AsReadOnly());
        }

        public void SendMessage<TM>(MessageType messageType, TM message, NativeArray<byte>.ReadOnly additionalData) where TM : unmanaged
        {
            foreach (var agent in m_Network.Agents)
            {
                if (agent.ReceivedMessageTypes.Contains(messageType))
                {
                    var receivedMessage = ReceivedMessage<TM>.GetFromPool();
                    receivedMessage.Payload = message;
                    if (additionalData.Length > 0)
                    {
                        receivedMessage.AdoptExtraData(new TestUdpAgentNativeExtraData(additionalData));
                    }
                    agent.QueueReceivedMessage(receivedMessage);
                }
            }
        }

        public ReceivedMessageBase ConsumeNextReceivedMessage()
        {
            return m_ReceivedMessages.Take();
        }

        public ReceivedMessageBase TryConsumeNextReceivedMessage()
        {
            return m_ReceivedMessages.TryTake(out var ret) ? ret : null;
        }

        public ReceivedMessageBase TryConsumeNextReceivedMessage(TimeSpan timeout)
        {
            return m_ReceivedMessages.TryTake(out var ret, timeout) ? ret : null;
        }

        public int ReceivedMessagesCount => m_ReceivedMessages.Count;

        public MessageType[] ReceivedMessageTypes { get; private set; }

        public ulong ReceivedMessagesSoFar { get; private set; }

        public void AddPreProcess(int priority, MessagePreprocessor preProcessor)
        {
            lock (m_MessagePreprocessors)
            {
                m_MessagePreprocessors.Add((priority, preProcessor));
                var sorted = m_MessagePreprocessors.OrderBy(p => p.Item1).ToArray();
                m_MessagePreprocessors.Clear();
                m_MessagePreprocessors.AddRange(sorted);
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
                    // No need to sort, removing does not change the order...
                }
            }
        }

        public NetworkStatistics Stats { get; } = new NetworkStatistics();

        public IUdpAgent Clone(MessageType[] receivedMessageTypes)
        {
            return new TestUdpAgent(m_Network, receivedMessageTypes);
        }

        void QueueReceivedMessage(ReceivedMessageBase receivedMessage)
        {
            lock (m_MessagePreprocessors)
            {
                foreach (var (_, preprocessor) in m_MessagePreprocessors)
                {
                    try
                    {
                        var ret = preprocessor(receivedMessage);
                        if (ret.DisposePreProcessedMessage && receivedMessage != null)
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
            if (receivedMessage != null)
            {
                ++ReceivedMessagesSoFar;
                m_ReceivedMessages.Add(receivedMessage);
            }
        }

        TestUdpAgentNetwork m_Network;
        List<(int, MessagePreprocessor)> m_MessagePreprocessors = new();
        // Remark: Does not really need to be blocking but allow implementation of the interface to be simpler and this
        // is only a unit test class, so ok if not 100% optimal...
        BlockingCollection<ReceivedMessageBase> m_ReceivedMessages = new (new ConcurrentQueue<ReceivedMessageBase>());
        static NativeArray<byte> s_EmptyNativeArray = new(0, Allocator.Persistent);
    }

    class TestUdpAgentNativeExtraData: IReceivedMessageData
    {
        public TestUdpAgentNativeExtraData(NativeArray<byte>.ReadOnly nativeReadOnly)
        {
            m_NativeExtraData = new NativeArray<byte>(nativeReadOnly.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte>.Copy(nativeReadOnly, m_NativeExtraData);
        }

        public int Length => m_NativeExtraData.Length;

        public ReceivedMessageDataFormat PreferredFormat => ReceivedMessageDataFormat.NativeArray;

        public void AsManagedArray(out byte[] array, out int dataStart, out int dataLength)
        {
            m_ManagedExtraData ??= m_NativeExtraData.ToArray();

            array = m_ManagedExtraData;
            dataStart = 0;
            dataLength = m_ManagedExtraData.Length;
        }

        public NativeArray<byte> AsNativeArray()
        {
            return m_NativeExtraData;
        }

        public void Release()
        {
            m_NativeExtraData.Dispose();
        }

        NativeArray<byte> m_NativeExtraData;
        byte[] m_ManagedExtraData;
    }
}
