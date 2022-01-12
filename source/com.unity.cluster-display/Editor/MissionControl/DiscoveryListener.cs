using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    sealed class DiscoveryListener : IDisposable
    {
        readonly byte[] m_SendBuffer = new byte[Constants.BufferSize];

        UdpClient m_UdpClient;

        public DiscoveryListener(int port = Constants.DefaultPort)
        {
            m_UdpClient = new UdpClient();
            m_UdpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        }

        public async Task Listen(CancellationToken token)
        {
            Console.WriteLine("Discovery listener started");
            try
            {
                while (true)
                {
                    var receiveResult = await m_UdpClient.ReceiveAsync().WithCancellation(token);
                    Console.WriteLine("Discovery request.");
                    await AnnounceToServer(receiveResult.RemoteEndPoint, token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Task cancelled");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        async Task AnnounceToServer(IPEndPoint serverEndPoint, CancellationToken token)
        {
            Console.WriteLine($"Responding to server: {serverEndPoint}");
            Array.Clear(m_SendBuffer, 0, Constants.BufferSize);
            NodeInfo nodeInfo = new NodeInfo();
            var size = m_SendBuffer.WriteStruct(ref nodeInfo);
            var remoteEndPoint = new IPEndPoint(serverEndPoint.Address, 11000);
            await m_UdpClient.SendAsync(m_SendBuffer, size, remoteEndPoint).WithCancellation(token);
        }

        public void Dispose()
        {
            m_UdpClient.Dispose();
        }
    }
}
