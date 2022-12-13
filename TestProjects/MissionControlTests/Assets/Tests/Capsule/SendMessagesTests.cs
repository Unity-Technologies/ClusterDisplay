using System;
using System.Collections;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    public class SendMessagesTests
    {
        [SetUp]
        public void SetUp()
        {
            m_ProcessingLoop = new();
            m_RunningProcessingLoop = m_ProcessingLoop.Start(k_TestPort);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return StopCapsule();
        }

        [UnityTest]
        public IEnumerator OneMessage()
        {
            using TcpClient testClient = new("127.0.0.1", k_TestPort);
            using var testCapcomStream = testClient.GetStream();

            ConnectionInit initStruct = new() {MessageFlow = MessageDirection.CapsuleToCapcom};
            initStruct.Send(testCapcomStream);
            yield return ConsumeInitialStatusMessage(testCapcomStream);

            yield return SendSendCapsuleStatus(testCapcomStream);

            // Need to manually stop the capsule to avoid error message.  By design as capcom should always outlive the
            // capsule.
            yield return StopCapsule();
        }

        [UnityTest]
        public IEnumerator TwoCapcomConnections()
        {
            // First connection
            using TcpClient testClient1 = new("127.0.0.1", k_TestPort);
            using var testCapcomStream1 = testClient1.GetStream();

            ConnectionInit initStruct = new() {MessageFlow = MessageDirection.CapsuleToCapcom};
            initStruct.Send(testCapcomStream1);
            yield return ConsumeInitialStatusMessage(testCapcomStream1);

            // Second connection
            using TcpClient testClient2 = new("127.0.0.1", k_TestPort);
            using var testCapcomStream2 = testClient2.GetStream();

            initStruct.Send(testCapcomStream2);

            // Try to write something on that second connection, it should fail when the capsule refuses it and close
            // the stream and the connection.
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                try
                {
                    testCapcomStream2.WriteByte(42);
                    testCapcomStream2.Flush();
                }
                catch (Exception)
                {
                    break;
                }
                yield return null;
            }
            LogAssert.Expect(LogType.Warning, "Refused message receiver as we already have one.");

            // Test the first connection is still good
            yield return SendSendCapsuleStatus(testCapcomStream1);

            // Need to manually stop the capsule to avoid error message.  By design as capcom should always outlive the
            // capsule.
            yield return StopCapsule();
        }

        IEnumerator StopCapsule()
        {
            if (m_RunningProcessingLoop != default)
            {
                m_ProcessingLoop.Stop();
                m_ProcessingLoop = null;
                yield return m_RunningProcessingLoop.AsIEnumerator();
                m_RunningProcessingLoop = default;
            }
        }

        static IEnumerator ConsumeInitialStatusMessage(NetworkStream networkStream)
        {
            // Read the message that capcom should receive
            byte[] buffer = new byte[Marshal.SizeOf<Guid>()];
            Guid capsuleStatusMessageId = Guid.Empty;
            var getMessageTask = Task.Run(async () =>
            {
                var retId = await networkStream.ReadStructAsync<Guid>(buffer);
                if (!retId.HasValue)
                {
                    return;
                }
                capsuleStatusMessageId = retId.Value;

                await networkStream.ReadStructAsync<CapsuleStatusMessage>(buffer);
            });
            EnumeratorTimeout readTimeout = new(TimeSpan.FromSeconds(5));
            yield return getMessageTask.AsIEnumerator(readTimeout);
            Assert.That(readTimeout.TimedOut, Is.False);

            // Validate the received message
            Assert.That(capsuleStatusMessageId, Is.EqualTo(MessagesId.CapsuleStatus));

            // Send a response
            CapsuleStatusResponse response = new();
            EnumeratorTimeout writeTimeout = new(TimeSpan.FromSeconds(5));
            yield return networkStream.WriteStructAsync(response, buffer).AsIEnumerator(writeTimeout);
            Assert.That(writeTimeout.TimedOut, Is.False);
        }

        IEnumerator SendSendCapsuleStatus(NetworkStream networkStream)
        {
            // Send the message
            var sendCapsuleStatus = SendCapsuleStatus.New();
            sendCapsuleStatus.NodeRole = NodeRole.Repeater;
            sendCapsuleStatus.NodeId = 28;
            sendCapsuleStatus.RenderNodeId = 42;
            m_ProcessingLoop.QueueSendMessage(sendCapsuleStatus);

            // Read the message that capcom should receive
            byte[] buffer = new byte[Marshal.SizeOf<Guid>()];
            Guid capsuleStatusMessageId = Guid.Empty;
            CapsuleStatusMessage capsuleStatusMessage = new();
            var getMessageTask = Task.Run(async () =>
            {
                var retId = await networkStream.ReadStructAsync<Guid>(buffer);
                if (!retId.HasValue)
                {
                    return;
                }
                capsuleStatusMessageId = retId.Value;

                var retMsg = await networkStream.ReadStructAsync<CapsuleStatusMessage>(buffer);
                if (!retMsg.HasValue)
                {
                    return;
                }
                capsuleStatusMessage = retMsg.Value;
            });
            EnumeratorTimeout readTimeout = new(TimeSpan.FromSeconds(5));
            yield return getMessageTask.AsIEnumerator(readTimeout);
            Assert.That(readTimeout.TimedOut, Is.False);

            // Validate the received message
            Assert.That(capsuleStatusMessageId, Is.EqualTo(MessagesId.CapsuleStatus));
            Assert.That(capsuleStatusMessage.NodeRole, Is.EqualTo((byte)NodeRole.Repeater));
            Assert.That(capsuleStatusMessage.NodeId, Is.EqualTo(28));
            Assert.That(capsuleStatusMessage.RenderNodeId, Is.EqualTo(42));

            // Send a response
            CapsuleStatusResponse response = new();
            EnumeratorTimeout writeTimeout = new(TimeSpan.FromSeconds(5));
            yield return networkStream.WriteStructAsync(response, buffer).AsIEnumerator(writeTimeout);
            Assert.That(writeTimeout.TimedOut, Is.False);
        }

        const int k_TestPort = Helpers.ListenPort;
        ProcessingLoop m_ProcessingLoop;
        Task m_RunningProcessingLoop;
    }
}
