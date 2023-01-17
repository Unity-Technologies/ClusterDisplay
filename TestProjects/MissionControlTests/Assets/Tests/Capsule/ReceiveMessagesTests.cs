using System;
using System.Collections;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    public class ReceiveMessagesTests
    {
        struct TestHandlerMessage
        {
            public Guid Value;
        }

        struct TestHandlerResponse
        {
            public Guid Value1;
            public Guid Value2;
        }

        class TestHandlerResponseReceiver
        {
            public TestHandlerResponse Value;

            public async ValueTask ReceiveFromAsync(NetworkStream stream)
            {
                byte[] receiveBuffer = new byte[Marshal.SizeOf<TestHandlerResponse>()];
                var response = await stream.ReadStructAsync<TestHandlerResponse>(receiveBuffer).ConfigureAwait(false);
                Assert.That(response.HasValue, Is.True);
                Value = response!.Value;
            }
        }

        class TestHandler : IMessageHandler
        {
            public async ValueTask HandleMessage(NetworkStream networkStream)
            {
                var message = await networkStream.ReadStructAsync<TestHandlerMessage>(m_MessageBuffer).ConfigureAwait(false);
                if (message != null)
                {
                    Message = message.Value;
                }

                await networkStream.WriteStructAsync(Response, m_ResponseBuffer);
            }

            public TestHandlerMessage Message { get; private set; }
            public TestHandlerResponse Response { get; } = new() { Value1 = Guid.NewGuid(), Value2 = Guid.NewGuid() };

            byte[] m_MessageBuffer = new byte[Marshal.SizeOf<TestHandlerMessage>()];
            byte[] m_ResponseBuffer = new byte[Marshal.SizeOf<TestHandlerResponse>()];
        }

        [SetUp]
        public void SetUp()
        {
            m_ProcessingLoop = new();
            m_TestHandler = new();
            m_ProcessingLoop.AddMessageHandler(m_TestHandlerId, m_TestHandler);
            m_RunningProcessingLoop = m_ProcessingLoop.Start(k_TestPort);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (m_RunningProcessingLoop != default)
            {
                m_ProcessingLoop.Stop();
                m_ProcessingLoop = null;
                yield return m_RunningProcessingLoop.AsIEnumerator();
                m_RunningProcessingLoop = default;
            }
        }

        [UnityTest]
        public IEnumerator StartStop()
        {
            yield return Task.Delay(100).AsIEnumerator();
        }

        [UnityTest]
        public IEnumerator OneClient()
        {
            using TcpClient testClient = new("127.0.0.1", k_TestPort);
            using var testClientStream = testClient.GetStream();

            ConnectionInit initStruct = new() {MessageFlow = MessageDirection.CapcomToCapsule};
            testClientStream.WriteStruct(initStruct);

            TestHandlerMessage message = new() {Value = Guid.NewGuid()};
            yield return SendMessage(testClientStream, message);
            TestHandlerResponseReceiver responseReceiver = new();
            yield return responseReceiver.ReceiveFromAsync(testClientStream).AsIEnumerator();

            Assert.That(m_TestHandler.Message.Value, Is.EqualTo(message.Value));
            Assert.That(responseReceiver.Value.Value1, Is.EqualTo(m_TestHandler.Response.Value1));
            Assert.That(responseReceiver.Value.Value2, Is.EqualTo(m_TestHandler.Response.Value2));
        }

        [UnityTest]
        public IEnumerator TwoClients()
        {
            using TcpClient testClient1 = new("127.0.0.1", k_TestPort);
            using var testClient1Stream = testClient1.GetStream();
            using TcpClient testClient2 = new("127.0.0.1", k_TestPort);
            using var testClient2Stream = testClient2.GetStream();

            ConnectionInit initStruct = new() {MessageFlow = MessageDirection.CapcomToCapsule};
            testClient1Stream.WriteStruct(initStruct);
            testClient2Stream.WriteStruct(initStruct);

            TestHandlerMessage message1 = new() {Value = Guid.NewGuid()};
            yield return SendMessage(testClient1Stream, message1);
            TestHandlerResponseReceiver responseReceiver1 = new();
            yield return responseReceiver1.ReceiveFromAsync(testClient1Stream).AsIEnumerator();

            Assert.That(m_TestHandler.Message.Value, Is.EqualTo(message1.Value));
            Assert.That(responseReceiver1.Value.Value1, Is.EqualTo(m_TestHandler.Response.Value1));
            Assert.That(responseReceiver1.Value.Value2, Is.EqualTo(m_TestHandler.Response.Value2));

            TestHandlerMessage message2 = new() {Value = Guid.NewGuid()};
            yield return SendMessage(testClient2Stream, message2);
            TestHandlerResponseReceiver responseReceiver2 = new();
            yield return responseReceiver2.ReceiveFromAsync(testClient2Stream).AsIEnumerator();

            Assert.That(m_TestHandler.Message.Value, Is.EqualTo(message2.Value));
            Assert.That(responseReceiver2.Value.Value1, Is.EqualTo(m_TestHandler.Response.Value1));
            Assert.That(responseReceiver2.Value.Value2, Is.EqualTo(m_TestHandler.Response.Value2));
        }

        IEnumerator SendMessage(NetworkStream networkStream, TestHandlerMessage message)
        {
            byte[] guidBuffer = new byte[Marshal.SizeOf<Guid>()];
            yield return networkStream.WriteStructAsync(m_TestHandlerId, guidBuffer).AsIEnumerator();
            byte[] sendBuffer = new byte[Marshal.SizeOf<TestHandlerMessage>()];
            yield return networkStream.WriteStructAsync(message, sendBuffer).AsIEnumerator();
        }

        const int k_TestPort = Helpers.ListenPort;
        ProcessingLoop m_ProcessingLoop;
        readonly Guid m_TestHandlerId = Guid.NewGuid();
        TestHandler m_TestHandler;
        Task m_RunningProcessingLoop;
    }
}
