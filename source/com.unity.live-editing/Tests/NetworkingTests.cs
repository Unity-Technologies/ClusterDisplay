using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.LiveEditing.LowLevel;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.LiveEditing.LowLevel.Networking;

namespace Unity.LiveEditing.Tests
{
    public class NetworkingTests
    {
        [UnityTest]
        public IEnumerator TestClientSendAndReceive()
        {
            var port = GetFreeTcpPort();

            IEnumerator WaitNumFrames(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                }
            }

            using var perFrame = new CoroutineLooper();
            using var every5Frames = new CoroutineLooper(WaitNumFrames(5));

            // Create the server (hub). Update every 5 frames.
            using var server = new TcpMessageServer(port, every5Frames);
            var serverEndPoint = new IPEndPoint(IPAddress.Loopback, port);

            // Create some clients
            var connectionTimeout = TimeSpan.FromSeconds(1);
            using var client1 = new TcpMessageClient(perFrame);
            using var client2 = new TcpMessageClient(perFrame);
            using var client3 = new TcpMessageClient(perFrame);

            var cts = new CancellationTokenSource();
            var connectTask = Task.WhenAll(
                client1.JoinMessageServerAsync(serverEndPoint, connectionTimeout, cts.Token),
                client2.JoinMessageServerAsync(serverEndPoint, connectionTimeout, cts.Token),
                client3.JoinMessageServerAsync(serverEndPoint, connectionTimeout, cts.Token));

            while (!connectTask.IsCompleted)
            {
                yield return null;
            }

            // We know that when the JoinMessageServerAsync tasks complete, it means the server has accepted
            // the connections (because it does an id-assignment handshake).

            // TEST: Verify that server connects all the clients.
            Assert.That(connectTask.IsFaulted, Is.Not.True);
            Assert.That(server.ClientCount, Is.EqualTo(3));

            // Create some string data
            var sendMessages1 = new List<string> { "Tis but a scratch!", "I fart in your general direction" };
            var sendMessages2 = new List<string> { "She turned me into a newt", "Well I got better" };
            var sendMessages3 = new List<string> { "Your mother was a hamster", "Your father smelled of elderberries" };

            // Setup to capture any received data.
            var receivedMessages1 = new List<string>();
            var receivedMessages2 = new List<string>();
            var receivedMessages3 = new List<string>();

            client1.DataReceived += bytes =>
            {
                receivedMessages1.Add(System.Text.Encoding.UTF8.GetString(bytes));
            };

            client2.DataReceived += bytes =>
            {
                receivedMessages2.Add(System.Text.Encoding.UTF8.GetString(bytes));
            };

            client3.DataReceived += bytes =>
            {
                receivedMessages3.Add(System.Text.Encoding.UTF8.GetString(bytes));
            };

            // Send some messages from each client
            client1.Send(System.Text.Encoding.UTF8.GetBytes(sendMessages1[0]));
            client2.Send(System.Text.Encoding.UTF8.GetBytes(sendMessages2[0]));
            client3.Send(System.Text.Encoding.UTF8.GetBytes(sendMessages3[0]));

            yield return null;

            client1.Send(System.Text.Encoding.UTF8.GetBytes(sendMessages1[1]));
            client2.Send(System.Text.Encoding.UTF8.GetBytes(sendMessages2[1]));
            client3.Send(System.Text.Encoding.UTF8.GetBytes(sendMessages3[1]));

            // We don't know how long messages take to reach their destinations, so wait
            // a reasonable amount of time.
            yield return new WaitForSecondsRealtime(1f);

            // TEST: Each client should receive all messages except the ones sent from themselves.
            Assert.That(receivedMessages1, Is.EquivalentTo(sendMessages2.Concat(sendMessages3)));
            Assert.That(receivedMessages2, Is.EquivalentTo(sendMessages1.Concat(sendMessages3)));
            Assert.That(receivedMessages3, Is.EquivalentTo(sendMessages1.Concat(sendMessages2)));
        }

        [UnityTest]
        public IEnumerator TestDisconnect()
        {
            var port = GetFreeTcpPort();
            using var perFrame = new CoroutineLooper();

            using var server = new TcpMessageServer(port, perFrame);
            var serverEndPoint = new IPEndPoint(IPAddress.Loopback, port);

            // Create some clients
            var connectionTimeout = TimeSpan.FromSeconds(1);
            using var client1 = new TcpMessageClient(perFrame);
            using var client2 = new TcpMessageClient(perFrame);

            // TEST: Verify that server connects all the clients.
            var cts = new CancellationTokenSource();
            var connectTask = Task.WhenAll(
                client1.JoinMessageServerAsync(serverEndPoint, connectionTimeout, cts.Token),
                client2.JoinMessageServerAsync(serverEndPoint, connectionTimeout, cts.Token));

            while (!connectTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(server.ClientCount, Is.EqualTo(2));

            // TEST: Disconnect a client and verify that the server updates its connections.
            client1.Stop(TimeSpan.FromSeconds(1));

            // We don't know how long it takes for the shutdown to propagate back to the server.
            var quickWait = new WaitForSecondsRealtime(0.1f);
            int retries = 0;
            while (server.ClientCount >= 3 && retries < 60)
            {
                retries++;
                yield return quickWait;
            }

            // TEST: Verify that we disconnected cleanly
            Assert.That(client1.CurrentException, Is.Null);
            Assert.That(server.HasErrors, Is.False);

            Assert.That(server.ClientCount, Is.EqualTo(2));

            // TEST: We can still dispose a disconnected client
            client1.Dispose();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(client1.CurrentException, Is.Null);
            Assert.That(server.HasErrors, Is.False);

            // TEST: Stop the Server. Clients should get disconnected.
            server.Stop(TimeSpan.FromSeconds(1));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(client2.IsConnected, Is.False);
            Assert.That(client2.CurrentException, Is.Null);
        }


        [UnityTest]
        public IEnumerator TestDispose()
        {
            var port = GetFreeTcpPort();
            using var perFrame = new CoroutineLooper();

            using var server = new TcpMessageServer(port, perFrame);
            var serverEndPoint = new IPEndPoint(IPAddress.Loopback, port);

            // Create some clients
            var connectionTimeout = TimeSpan.FromSeconds(1);
            using var client1 = new TcpMessageClient(perFrame);
            using var client2 = new TcpMessageClient(perFrame);

            // TEST: Verify that server connects all the clients.
            var cts = new CancellationTokenSource();
            var connectTask = Task.WhenAll(
                client1.JoinMessageServerAsync(serverEndPoint, connectionTimeout, cts.Token),
                client2.JoinMessageServerAsync(serverEndPoint, connectionTimeout, cts.Token));

            while (!connectTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(server.ClientCount, Is.EqualTo(2));

            // TEST: Improperly dispose a client and verify that the server updates its connections.
            client1.Dispose();

            // We don't know how long it takes for the shutdown to propagate back to the server.
            var quickWait = new WaitForSecondsRealtime(0.1f);
            int retries = 0;
            while (server.ClientCount >= 3 && retries < 60)
            {
                retries++;
                yield return quickWait;
            }

            Assert.That(server.ClientCount, Is.EqualTo(2));

            // TEST: Improperly dispose the Server. Clients should get disconnected.
            server.Dispose();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(client2.IsConnected, Is.False);
        }

        static int GetFreeTcpPort()
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
