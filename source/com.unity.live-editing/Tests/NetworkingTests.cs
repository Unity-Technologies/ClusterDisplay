using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using Unity.LiveEditing.LowLevel;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.LiveEditing.LowLevel.Networking;

namespace Unity.LiveEditing.Tests
{
    public class NetworkingTests
    {
        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator NetworkingTestsWithEnumeratorPasses()
        {
            var port = GetFreeTcpPort();
            using var looper = new PlayerUpdateLooper();

            using var server = new TcpMessageServer(port, looper);
            var serverEndPoint = new IPEndPoint(IPAddress.Loopback, port);

            using var client1 = new TcpMessageClient(serverEndPoint, looper);
            using var client2 = new TcpMessageClient(serverEndPoint, looper);

            var sendMessages1 = new List<string> { "Tis but a scratch!", "I fart in your general direction" };
            var sendMessages2 = new List<string> { "She turned me into a newt", "Well I got better" };

            var receivedMessages1 = new List<string>();
            var receivedMessages2 = new List<string>();
            client1.DataReceived += bytes =>
            {
                receivedMessages1.Add(System.Text.Encoding.UTF8.GetString(bytes));
            };

            client2.DataReceived += bytes =>
            {
                receivedMessages2.Add(System.Text.Encoding.UTF8.GetString(bytes));
            };

            yield return new WaitForSecondsRealtime(1f);
            Assert.That(server.ClientCount, Is.EqualTo(2));
            client1.Send(System.Text.Encoding.UTF8.GetBytes(sendMessages1[0]));
            client2.Send(System.Text.Encoding.UTF8.GetBytes(sendMessages2[0]));

            yield return null;

            client1.Send(System.Text.Encoding.UTF8.GetBytes(sendMessages1[1]));
            client2.Send(System.Text.Encoding.UTF8.GetBytes(sendMessages2[1]));

            // yield return null;
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(receivedMessages1, Is.EqualTo(sendMessages2));
            Assert.That(receivedMessages2, Is.EqualTo(sendMessages1));

            client1.Stop(TimeSpan.FromSeconds(1));
            client1.Dispose();
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(server.ClientCount, Is.EqualTo(1));
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
