using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Unity.ClusterDisplay.Tests.NetworkingTestHelpers;

namespace Unity.ClusterDisplay.Tests
{
    public class NetworkingTests
    {
        UDPAgent m_Agent;
        const byte k_AgentNodeId = 0;

        const int k_RxPort = 12345;
        const int k_TxPort = 12346;
        const string k_MulticastAddress = "224.0.1.0";

        static readonly byte[] k_TestNodes = {1, 2};

        UdpClient[] m_TestClients;
        static readonly IPEndPoint k_AgentEndPoint = new(IPAddress.Parse(k_MulticastAddress), k_RxPort);
        const int k_TimeoutSeconds = 10;
        const int k_MaxRetries = 50;

        static readonly ulong k_AllNodesMask = k_TestNodes.ToMask() | k_AgentNodeId.ToMask();

        [SetUp]
        public void SetUp()
        {
            var nic = SelectNic();
            Assert.NotNull(nic);
            
            var config = new UDPAgent.Config
            {
                nodeId = k_AgentNodeId,
                ip = k_MulticastAddress,
                rxPort = k_RxPort,
                txPort = k_TxPort,
                timeOut = k_TimeoutSeconds,
                adapterName = nic.Name
            };
            m_Agent = new UDPAgent(config)
            {
                AllNodesMask = k_AllNodesMask
            };
            Assert.True(m_Agent.Initialize());

            var localAddress = nic.GetIPProperties().UnicastAddresses
                .Select(addr => addr.Address)
                .FirstOrDefault(addr => addr.AddressFamily == AddressFamily.InterNetwork);
            
            m_TestClients = new[]
            {
                CreateClient(k_MulticastAddress, k_TxPort, localAddress),
                CreateClient(k_MulticastAddress, k_TxPort, localAddress)
            };
        }

        [TearDown]
        public void TearDown()
        {
            m_Agent.Stop();
        }

        [UnityTest]
        public IEnumerator TestReceiveMessageAsync()
        {
            return TestAsyncTask(TestReceiveMessage(), k_TimeoutSeconds);
        }

        [UnityTest]
        public IEnumerator TestPublishAsync()
        {
            return TestAsyncTask(TestPublish(), k_TimeoutSeconds);
        }

        async Task TestReceiveMessage()
        {
            var (_, testMessage) = GenerateTestMessage(k_TestNodes[0], new byte[] {0});
            var result = await m_TestClients[0].SendAsync(testMessage, testMessage.Length, k_AgentEndPoint);
            Assert.That(result, Is.EqualTo(testMessage.Length));

            // Do the receive and parse the results
            var (header, contents) = await m_Agent.ReceiveMessage<RepeaterEnteredNextFrame>(k_TimeoutSeconds);
            Assert.That(header.MessageType, Is.EqualTo(EMessageType.EnterNextFrame));
            Assert.That(contents.FrameNumber, Is.EqualTo(1));

            // Check that we receive an ACK
            var ack = await m_TestClients[0].ReceiveAsync();
            Assert.That(ack.Buffer.LoadStruct<MessageHeader>().MessageType, Is.EqualTo(EMessageType.AckMsgRx));
        }

        async Task TestPublish()
        {
            // Send a message to multiple nodes
            var (header, rawMsg) = GenerateTestMessage(k_AgentNodeId, k_TestNodes);
            Assert.True(m_Agent.PublishMessage(header, rawMsg));

            // Make sure we received the messages
            for (var i = 0; i < m_TestClients.Length; i++)
            {
                var client = m_TestClients[i];
                var (msgHeader, contents) = await client.ReceiveMessage<RepeaterEnteredNextFrame>();
                Assert.That(msgHeader.MessageType, Is.EqualTo(EMessageType.EnterNextFrame));
                Assert.That(msgHeader.OriginID, Is.EqualTo(k_AgentNodeId));
                Assert.NotZero(msgHeader.DestinationIDs & k_TestNodes[i].ToMask());
                Assert.That(contents.FrameNumber, Is.EqualTo(1));
            }

            // Agent should now be waiting for ACK messages
            Assert.True(m_Agent.AcksPending);
            var ackBuffer = new byte[headerSize];

            async Task<int> SendAck(int index)
            {
                var ackHeader = new MessageHeader
                {
                    MessageType = EMessageType.AckMsgRx,
                    DestinationIDs = 1 << k_AgentNodeId,
                    OriginID = k_TestNodes[index]
                };
                ackHeader.StoreInBuffer(ackBuffer);
                return await m_TestClients[index].SendAsync(ackBuffer, ackBuffer.Length, k_AgentEndPoint);
            }

            // Send one ACK, agent should still be waiting
            var result = await SendAck(0);
            Assert.That(result, Is.GreaterThan(0));
            var retries = 0;
            while (retries < 5)
            {
                Assert.True(m_Agent.AcksPending);
                await Task.Delay(100);
                retries++;
            }

            // Send remaining ACK, agent should be done waiting
            result = await SendAck(1);
            Assert.That(result, Is.GreaterThan(0));

            retries = 0;
            while (m_Agent.AcksPending && retries < k_MaxRetries)
            {
                await Task.Delay(100);
                retries++;
            }

            Assert.False(m_Agent.AcksPending);
        }
    }

    static class NetworkingTestHelpers
    {
        public static readonly int headerSize = Marshal.SizeOf<MessageHeader>();

        public static ulong ToMask(this byte id) => 1UL << id;

        public static ulong ToMask(this IEnumerable<byte> ids) => ids.Aggregate(0UL, (mask, id) => mask | id.ToMask());

        public static async ValueTask<(MessageHeader header, T contents)> ReceiveMessage<T>(this UDPAgent agent, int timeout) where T : unmanaged
        {
            return await Task.Run(() =>
            {
                if (agent.RxWait.WaitOne(timeout * 1000) && agent.NextAvailableRxMsg(out var header, out var outBuffer))
                {
                    return (header, outBuffer.LoadStruct<T>(headerSize));
                }

                return default;
            });
        }

        public static async ValueTask<(MessageHeader header, T contents)> ReceiveMessage<T>(this UdpClient agent) where T : unmanaged
        {
            var result = await agent.ReceiveAsync();
            return (result.Buffer.LoadStruct<MessageHeader>(), result.Buffer.LoadStruct<T>(headerSize));
        }

        public static (MessageHeader header, byte[] rawMsg) GenerateTestMessage(byte originId, IEnumerable<byte> destinations)
        {
            // Generate and send message
            var header = new MessageHeader
            {
                MessageType = EMessageType.EnterNextFrame,
                DestinationIDs = destinations.ToMask(),
                OriginID = originId
            };
            var message = new RepeaterEnteredNextFrame
            {
                FrameNumber = 1
            };

            var bufferLen = headerSize + Marshal.SizeOf<RepeaterEnteredNextFrame>();
            var buffer = new byte[bufferLen];

            header.StoreInBuffer(buffer);
            message.StoreInBuffer(buffer, headerSize);

            return (header, buffer);
        }
        
        public static UdpClient CreateClient(string multicastAddress, int rxPort, IPAddress localAddress)
        {
            Debug.Log(localAddress);

            var client = new UdpClient();
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, localAddress.GetAddressBytes());
            
            client.Client.Bind(new IPEndPoint(IPAddress.Any, rxPort));
            client.JoinMulticastGroup(IPAddress.Parse(multicastAddress));
            return client;
        }

        public static IEnumerator TestAsyncTask(Task task, int timeoutSeconds)
        {
            var elapsed = 0f;
            while (!task.IsCompleted && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.True(task.IsCompleted);
            Assert.DoesNotThrow(task.Wait);
        }

        public static NetworkInterface SelectNic()
        {
            return NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(nic => nic.OperationalStatus == OperationalStatus.Up);
        }
    }
}
