using System.Collections;
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
        // A Test behaves as an ordinary method
        [Test]
        public void NetworkingTestsSimplePasses()
        {
            // Use the Assert class to test conditions
        }

        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator NetworkingTestsWithEnumeratorPasses()
        {
            var port = GetFreeTcpPort();
            using var looper = new PlayerUpdateLooper();

            using var server = new TcpMessageHub(port, looper);
            var serverEndPoint = new IPEndPoint(IPAddress.Loopback, port);

            using var client1 = new TcpMessageClient(serverEndPoint, looper);
            using var client2 = new TcpMessageClient(serverEndPoint, looper);

            client1.DataReceived += bytes =>
            {
                Debug.Log($"client1 received {System.Text.Encoding.UTF8.GetString(bytes)}");
            };

            client2.DataReceived += bytes =>
            {
                Debug.Log($"client2 received {System.Text.Encoding.UTF8.GetString(bytes)}");
            };

            yield return new WaitForSeconds(1);
            Assert.That(server.ClientCount, Is.EqualTo(2));
            client1.Send(System.Text.Encoding.UTF8.GetBytes("Hello"));
            client2.Send(System.Text.Encoding.UTF8.GetBytes("World"));

            // Use the Assert class to test conditions.
            // Use yield to skip a frame.
            yield return new WaitForSeconds(1);
            // client1.Dispose();

            yield return new WaitForSeconds(1);
            client2.Send(System.Text.Encoding.UTF8.GetBytes("Foo"));

            yield return new WaitForSeconds(1);
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
