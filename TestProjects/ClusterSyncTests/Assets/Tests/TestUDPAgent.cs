using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using MessagePreprocessor = System.Func<Unity.ClusterDisplay.ReceivedMessageBase, Unity.ClusterDisplay.ReceivedMessageBase>;

namespace Unity.ClusterDisplay.Tests
{
    /// <summary>
    /// Represent a fictive network to which multiple TestUDPAgent are connected.
    /// </summary>
    class TestUDPAgentNetwork
    {
        public List<TestUDPAgent> Agents { get; } = new();
    }

    /// <summary>
    /// Dummy <see cref="IUDPAgent"/> that does not really transmit and where everything sent is automatically received.
    /// </summary>
    class TestUDPAgent: IUDPAgent
    {
        public TestUDPAgent(TestUDPAgentNetwork network, MessageType[] receivedMessageTypes)
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
            foreach (var agent in m_Network.Agents)
            {
                if (agent.ReceivedMessageTypes.Contains(messageType))
                {
                    var receivedMessage = ReceivedMessage<TM>.GetFromPool();
                    receivedMessage.Payload = message;
                    agent.QueueReceivedMessage(receivedMessage);
                }
            }
        }

        public void SendMessage<TM>(MessageType messageType, TM message, NativeArray<byte>.ReadOnly additionalData) where TM : unmanaged
        {
            foreach (var agent in m_Network.Agents)
            {
                if (agent.ReceivedMessageTypes.Contains(messageType))
                {
                    var receivedMessage = ReceivedMessage<TM>.GetFromPool();
                    receivedMessage.Payload = message;
                    receivedMessage.InitializeWithExtraData(new TestUDPAgentNativeExtraData(additionalData));
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

        public event MessagePreprocessor OnMessagePreProcess
        {
            add => m_MessagePreprocessors.Add(value);
            remove => m_MessagePreprocessors.Remove(value);
        }
        List<MessagePreprocessor> m_MessagePreprocessors = new();

        public NetworkStatistics Stats { get; } = new NetworkStatistics();

        void QueueReceivedMessage(ReceivedMessageBase receivedMessage)
        {
            foreach (var preprocessor in m_MessagePreprocessors)
            {
                try
                {
                    receivedMessage = preprocessor(receivedMessage);
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
            if (receivedMessage != null)
            {
                ++ReceivedMessagesSoFar;
                m_ReceivedMessages.Add(receivedMessage);
            }
        }

        TestUDPAgentNetwork m_Network;
        // Remark: Does not really need to be blocking but allow implementation of the interface to be simpler and this
        // is only a unit test class, so ok if not 100% optimal...
        BlockingCollection<ReceivedMessageBase> m_ReceivedMessages = new (new ConcurrentQueue<ReceivedMessageBase>());
    }

    class TestUDPAgentNativeExtraData: IReceivedMessageExtraData, IDisposable
    {
        public TestUDPAgentNativeExtraData(NativeArray<byte>.ReadOnly nativeReadOnly)
        {
            m_NativeExtraData = new NativeArray<byte>(nativeReadOnly.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte>.Copy(nativeReadOnly, m_NativeExtraData);
        }

        public void Dispose()
        {
            m_NativeExtraData.Dispose();
        }

        public int Length => m_NativeExtraData.Length;

        public ExtraDataFormat PreferredFormat => ExtraDataFormat.NativeArray;

        public void AsManagedArray(out byte[] array, out int extraDataStart, out int extraDataLength)
        {
            if (m_ManagedExtraData == null)
            {
                m_ManagedExtraData = m_NativeExtraData.ToArray();
            }

            array = m_ManagedExtraData;
            extraDataStart = 0;
            extraDataLength = m_ManagedExtraData.Length;
        }

        public NativeArray<byte> AsNativeArray()
        {
            return m_NativeExtraData;
        }

        NativeArray<byte> m_NativeExtraData;
        byte[] m_ManagedExtraData;
    }
}
