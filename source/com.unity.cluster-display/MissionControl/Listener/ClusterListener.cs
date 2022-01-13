using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    sealed class ClusterListener : IDisposable
    {
        readonly byte[] m_SendBuffer = new byte[Constants.BufferSize];
        static readonly string k_MachineName = Environment.MachineName;
        const int k_MessageQueueSize = 16;

        IPAddress m_LocalAddress;
        UdpClient m_UdpClient;
        ConcurrentQueue<NodeInfo> m_OutgoingMessages = new();
        CancellationTokenSource m_LaunchCancellation = new();
        Task m_LaunchTask;

        public ClusterListener(int port = Constants.DiscoveryPort)
        {
            m_UdpClient = new UdpClient(port);
        }

        public async Task Listen(CancellationToken token)
        {
            Console.WriteLine("Cluster listener started");
            try
            {
                ReportNodeStatus(NodeStatus.Ready);
                while (true)
                {
                    var receiveResult = await m_UdpClient.ReceiveAsync().WithCancellation(token);
                    if (receiveResult.Buffer.MatchMessage<ServerInfo>() is var (header, _))
                    {
                        // Console.WriteLine($"Discovery request from {header.HostName}");
                        var serverEndPoint = header.EndPoint;
                        m_LocalAddress ??= NetworkUtils.QueryRoutingInterface(header.EndPoint);

                        await AnnounceToServer(serverEndPoint, token);
                    }
                    else if (receiveResult.Buffer.MatchMessage<LaunchInfo>() is var (_, launchInfo))
                    {
                        Console.WriteLine("Launch requested");
                        await KillRunningProcess();
                        m_LaunchCancellation = new CancellationTokenSource();
                        m_LaunchTask = MonitorLaunch(launchInfo, m_LaunchCancellation.Token);
                    }
                    else if (receiveResult.Buffer.MatchMessage<KillInfo>() is var (_, _))
                    {
                        await KillRunningProcess();
                    }
                    else
                    {
                        Console.WriteLine("Unsupported message type received: ");
                    }
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
            finally
            {
                m_LaunchCancellation.Cancel();
            }
        }

        async Task KillRunningProcess()
        {
            Console.WriteLine("Stopping the app");
            m_LaunchCancellation.Cancel();
            if (m_LaunchTask != null)
            {
                try
                {
                    await m_LaunchTask;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        async Task MonitorLaunch(LaunchInfo launchInfo, CancellationToken token)
        {
            Console.WriteLine($"Launching {launchInfo.ProjectDir}");
            try
            {
                await foreach (var status in Launcher.Launch(launchInfo, token))
                {
                    ReportNodeStatus(status);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("The launch task was cancelled.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
            ReportNodeStatus(NodeStatus.Ready);
        }

        void ReportNodeStatus(NodeStatus status)
        {
            m_OutgoingMessages.Enqueue(new NodeInfo(status));
            while (m_OutgoingMessages.Count > k_MessageQueueSize)
            {
                m_OutgoingMessages.TryDequeue(out _);
            }
        }

        async Task AnnounceToServer(IPEndPoint serverEndPoint, CancellationToken token)
        {
            // Console.WriteLine($"Responding to server: {serverEndPoint}");
            var ipEndPoint = m_UdpClient.Client.LocalEndPoint as IPEndPoint;

            Debug.Assert(ipEndPoint != null, nameof(ipEndPoint) + " != null");

            var localPort = ipEndPoint.Port;
            var localEndPoint = new IPEndPoint(m_LocalAddress, localPort);

            while (m_OutgoingMessages.Count > 1)
            {
                if (m_OutgoingMessages.TryDequeue(out var nodeInfo))
                {
                    var size = m_SendBuffer.WriteMessage(k_MachineName, localEndPoint, nodeInfo);
                    await m_UdpClient.SendAsync(m_SendBuffer, size, serverEndPoint).WithCancellation(token);
                }
            }

            if (m_OutgoingMessages.TryPeek(out var latestNodeInfo))
            {
                var size = m_SendBuffer.WriteMessage(k_MachineName, localEndPoint, latestNodeInfo);
                await m_UdpClient.SendAsync(m_SendBuffer, size, serverEndPoint).WithCancellation(token);
            }
        }

        public void Dispose()
        {
            m_UdpClient.Dispose();
        }
    }
}
