using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.LiveEditing.LowLevel.Networking
{
    public class Profiling : MonoBehaviour
    {
        void Start()
        {
            StartCoroutine(TestClientSendAndReceive());
        }

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
            using var client1 = new TcpMessageClient(serverEndPoint, perFrame, connectionTimeout);
            using var client2 = new TcpMessageClient(serverEndPoint, perFrame, connectionTimeout);
            using var client3 = new TcpMessageClient(serverEndPoint, perFrame, connectionTimeout);

            // TEST: Verify that server connects all the clients.
            int retries = 0;
            while (server.ClientCount < 3 && retries < 30)
            {
                retries++;
                yield return null;
            }

            // Assert.That(server.ClientCount, Is.EqualTo(3));

            // Create some string data
            var sendMessages1 = new List<string> { "Tis but a scratch!", "I fart in your general direction" };
            var sendMessages2 = new List<string> { "She turned me into a newt", "Well I got better" };
            var sendMessages3 = new List<string> { "Your mother was a hamster", "Your father smelled of elderberries" };
            var sendBytes1 = sendMessages1.Select(Encoding.UTF8.GetBytes).ToArray();
            var sendBytes2 = sendMessages2.Select(Encoding.UTF8.GetBytes).ToArray();
            var sendBytes3 = sendMessages3.Select(Encoding.UTF8.GetBytes).ToArray();

            // Setup to capture any received data.
            var receivedMessages1 = new List<string>();
            var receivedMessages2 = new List<string>();
            var receivedMessages3 = new List<string>();

            client1.DataReceived += bytes =>
            {
                // receivedMessages1.Add(System.Text.Encoding.UTF8.GetString(bytes));
            };

            client2.DataReceived += bytes =>
            {
                // receivedMessages2.Add(System.Text.Encoding.UTF8.GetString(bytes));
            };

            client3.DataReceived += bytes =>
            {
                // receivedMessages3.Add(System.Text.Encoding.UTF8.GetString(bytes));
            };

            var wait = new WaitForSecondsRealtime(0.1f);

            // Send some messages from each client
            for (int i = 0; i < 30; i++)
            {
                client1.Send(sendBytes1[0]);
                client2.Send(sendBytes2[0]);
                client3.Send(sendBytes3[0]);

                yield return null;

                client1.Send(sendBytes1[1]);
                client2.Send(sendBytes2[1]);
                client3.Send(sendBytes3[1]);

                yield return wait;
            }

            // TEST: Each client should receive all messages except the ones sent from themselves.
            // Assert.That(receivedMessages1, Is.EquivalentTo(sendMessages2.Concat(sendMessages3)));
            // Assert.That(receivedMessages2, Is.EquivalentTo(sendMessages1.Concat(sendMessages3)));
            // Assert.That(receivedMessages3, Is.EquivalentTo(sendMessages1.Concat(sendMessages2)));

            // TEST: Kill one of the clients and verify that the server updates its connections.
            client1.Stop(TimeSpan.FromSeconds(1));
            client1.Dispose();

            retries = 0;
            while (server.ClientCount >= 3 && retries < 30)
            {
                retries++;
                yield return null;
            }

            EditorApplication.isPlaying = false;
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
