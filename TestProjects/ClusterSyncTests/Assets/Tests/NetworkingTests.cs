using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using static Unity.ClusterDisplay.Tests.NetworkingUtils;

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

        static readonly BitVector k_AllNodesMask = k_TestNodes.ToMask().SetBit(k_AgentNodeId);

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
                timeOut = TimeSpan.FromSeconds(k_TimeoutSeconds),
                adapterName = nic.Name
            };

            // Set up UDPAgent to broadcast/receive from nodes 1 and 2
            m_Agent = new UDPAgent(config)
            {
                AllNodesMask = k_AllNodesMask
            };

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
            m_Agent.Dispose();
            foreach (var client in m_TestClients)
            {
                client.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator TestReceiveMessageAsync()
        {
            yield return TestReceiveMessage().ToCoroutine(k_TimeoutSeconds);
        }

        [UnityTest]
        public IEnumerator TestPublishAsync()
        {
            yield return TestPublish().ToCoroutine(k_TimeoutSeconds);
        }

        [UnityTest]
        public IEnumerator TestResendAsync()
        {
            yield return TestResend().ToCoroutine(k_TimeoutSeconds);
        }

        [UnityTest]
        public IEnumerator TestNoAckAsync()
        {
            yield return TestNoAck().ToCoroutine(k_TimeoutSeconds);
        }

        async Task TestReceiveMessage()
        {
            var (_, testMessage) = GenerateTestMessage(k_TestNodes[0], new byte[] {0});
            var result = await m_TestClients[0].SendAsync(testMessage, testMessage.Length, k_AgentEndPoint);
            Assert.That(result, Is.EqualTo(testMessage.Length));

            // Do the receive and parse the results
            var (header, contents) = await m_Agent.ReceiveMessageAsync<RepeaterEnteredNextFrame>(k_TimeoutSeconds);
            Assert.That(header.MessageType, Is.EqualTo(EMessageType.EnterNextFrame));
            Assert.That(header.SequenceID, Is.EqualTo(10));
            Assert.That(contents.FrameNumber, Is.EqualTo(5));

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
                var (msgHeader, contents) = await client.ReceiveMessageAsync<RepeaterEnteredNextFrame>();
                Assert.That(msgHeader.MessageType, Is.EqualTo(EMessageType.EnterNextFrame));
                Assert.That(msgHeader.OriginID, Is.EqualTo(k_AgentNodeId));
                Assert.IsTrue(msgHeader.DestinationIDs[k_TestNodes[i]]);
                Assert.That(contents.FrameNumber, Is.EqualTo(5));
            }

            // Agent should now be waiting for ACK messages
            Assert.True(m_Agent.HasPendingAcks);

            // Send one ACK, agent should still be waiting
            var result = await m_TestClients[0].SendAck(k_AgentEndPoint, k_AgentNodeId, k_TestNodes[0]);
            Assert.That(result, Is.GreaterThan(0));
            var retries = 0;
            while (retries < 5)
            {
                Assert.True(m_Agent.HasPendingAcks);
                await Task.Delay(100);
                retries++;
            }

            // Send remaining ACK, agent should be done waiting
            result = await m_TestClients[1].SendAck(k_AgentEndPoint, k_AgentNodeId, k_TestNodes[1]);
            Assert.That(result, Is.GreaterThan(0));

            retries = 0;
            while (m_Agent.HasPendingAcks && retries < k_MaxRetries)
            {
                await Task.Delay(100);
                retries++;
            }

            Assert.False(m_Agent.HasPendingAcks);
        }

        async Task TestResend()
        {
            // Send a message to multiple nodes
            var (header, rawMsg) = GenerateTestMessage(k_AgentNodeId, k_TestNodes);
            Assert.True(m_Agent.PublishMessage(header, rawMsg));
            var client = m_TestClients[0];

            // Receive the initial message
            var rxData = await client.ReceiveMessageAsync<RepeaterEnteredNextFrame>();
            Assert.That(rxData.header.MessageType, Is.EqualTo(header.MessageType));
            Assert.That(rxData.contents.FrameNumber, Is.EqualTo(5));

            // Agent should now be waiting for ACK messages
            Assert.True(m_Agent.HasPendingAcks);

            // Ack with node 1
            await m_TestClients[1].SendAck(k_AgentEndPoint, k_AgentNodeId, k_TestNodes[1]);

            // Wait for agent to resend after 1 second
            try
            {
                var rxResent = await client.ReceiveMessageAsync<RepeaterEnteredNextFrame>(2000);
                Assert.IsTrue(rxResent.header.Flags.HasFlag(MessageHeader.EFlag.Resending));
                Assert.IsTrue(rxResent.header.DestinationIDs[k_TestNodes[0]]);
                Assert.IsFalse(rxResent.header.DestinationIDs[k_TestNodes[1]]);
                Assert.That(rxResent.contents, Is.EqualTo(rxData.contents));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for resend");
            }
        }

        async Task TestNoAck()
        {
            // Send a message to multiple nodes
            var (header, rawMsg) = GenerateTestMessage(k_AgentNodeId, k_TestNodes.Take(1));
            header.Flags |= MessageHeader.EFlag.DoesNotRequireAck;

            Assert.True(m_Agent.PublishMessage(header, rawMsg));
            var client = m_TestClients[0];

            // Receive the initial message
            await client.ReceiveMessageAsync<RepeaterEnteredNextFrame>();

            // Agent should NOT be waiting for ACKs
            Assert.IsFalse(m_Agent.HasPendingAcks);
        }
    }
}
