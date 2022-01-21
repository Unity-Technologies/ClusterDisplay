using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        UdpClient m_UdpClient;
        BlockingCollection<NodeInfo> m_OutgoingMessages = new(k_MessageQueueSize);

        NodeStatus m_LatestStatus;

        CancellationTokenSource m_SubProcessCancellation;
        CancellationTokenSource m_TaskCancellation;

        Task m_ListenTask;
        Task m_SubProcessTask;
        Task m_HeartbeatTask;

        IPEndPoint m_ServerEndPoint;
        IPEndPoint m_LocalEndPoint;
        DateTime m_LastServerResponse = DateTime.Now;

        public ClusterListener(int port = Constants.DiscoveryPort)
        {
            m_UdpClient = new UdpClient(port);
            
            const int sioUdpConnreset = -1744830452;
            byte[] inValue = {0};
            byte[] outValue = {0};
            m_UdpClient.Client.IOControl(sioUdpConnreset, inValue, outValue);
        }

        public async Task Run()
        {
            Console.WriteLine("Cluster listener started");
            m_TaskCancellation = new CancellationTokenSource();
            var cancellationToken = m_TaskCancellation.Token;

            m_HeartbeatTask = DoHeartbeat(15000, cancellationToken);
            m_ListenTask = Listen(cancellationToken);

            await Task.WhenAll(m_HeartbeatTask, m_ListenTask);
        }

        async Task Listen(CancellationToken token)
        {
            while (true)
            {
                try
                {
                    var receiveResult = await m_UdpClient.ReceiveAsync().WithCancellation(token);
                    if (receiveResult.Buffer.MatchMessage<ServerInfo>() is var (header, _))
                    {
                        // Console.WriteLine($"Discovery request from {header.HostName}");
                        Debug.Assert(m_UdpClient.Client.LocalEndPoint != null);

                        m_LocalEndPoint ??= new IPEndPoint(
                            NetworkUtils.QueryRoutingInterface(header.EndPoint),
                            ((IPEndPoint) m_UdpClient.Client.LocalEndPoint).Port);

                        m_ServerEndPoint = header.EndPoint;
                        m_LastServerResponse = DateTime.Now;
                        ReportNodeStatus(m_LatestStatus, token);
                    }
                    else if (receiveResult.Buffer.MatchMessage<LaunchInfo>() is var (_, launchInfo))
                    {
                        Console.WriteLine("Launch requested");
                        await KillRunningProcess();
                        m_SubProcessCancellation = new CancellationTokenSource();
                        m_SubProcessTask = MonitorLaunch(launchInfo, m_SubProcessCancellation.Token);
                    }
                    else if (receiveResult.Buffer.MatchMessage<ProjectSyncInfo>() is var (_, syncInfo))
                    {
                        Console.WriteLine("Project sync requested");
                        await KillRunningProcess();
                        m_SubProcessCancellation = new CancellationTokenSource();
                        m_SubProcessTask = MonitorSync(syncInfo, m_SubProcessCancellation.Token);
                    }
                    else if (receiveResult.Buffer.MatchMessage<KillInfo>() is var (_, _))
                    {
                        await KillRunningProcess();
                        ReportNodeStatus(NodeStatus.Ready, token);
                    }
                    else
                    {
                        Console.WriteLine("Unsupported message type received");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        async Task KillRunningProcess()
        {
            m_SubProcessCancellation?.Cancel();
            if (m_SubProcessTask != null)
            {
                try
                {
                    await m_SubProcessTask;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                Console.WriteLine("Subprocess killed");
            }
        }

        async Task MonitorSync(ProjectSyncInfo syncInfo, CancellationToken token)
        {
            ReportNodeStatus(NodeStatus.SyncFiles, token);
            await Launcher.SyncProjectDir(syncInfo.SharedProjectDir, token);
            Console.WriteLine("Sync complete");
            ReportNodeStatus(NodeStatus.Ready, token);
        }

        async Task MonitorLaunch(LaunchInfo launchInfo, CancellationToken token)
        {
            ReportNodeStatus(NodeStatus.Running, token);
            await Launcher.Launch(launchInfo, token);
            ReportNodeStatus(NodeStatus.Ready, token);
        }

        void ReportNodeStatus(NodeStatus status, CancellationToken token)
        {
            m_LatestStatus = status;
            m_OutgoingMessages.TryAdd(new NodeInfo(status), Timeout.Infinite, token);
        }

        async Task PumpMessages(int timeoutMilliseconds, CancellationToken token)
        {
            while (await m_OutgoingMessages.TakeAsync(timeoutMilliseconds, token) is {} nodeInfo
                && m_ServerEndPoint != null)
            {
                var size = m_SendBuffer.WriteMessage(k_MachineName, m_LocalEndPoint, nodeInfo);
                Console.WriteLine($"Sending status {nodeInfo.Status} to {m_ServerEndPoint}");
                var bytesSent = await m_UdpClient.SendAsync(m_SendBuffer, size, m_ServerEndPoint).WithCancellation(token);
                if (bytesSent < size)
                {
                    Console.WriteLine($"Sent {bytesSent}; expected {size}");
                }
            }
        }

        async Task DoHeartbeat(int intervalMilliseconds, CancellationToken token)
        {
            while (true)
            {
                await PumpMessages(intervalMilliseconds, token);
                if (m_ServerEndPoint != null && (DateTime.Now - m_LastServerResponse).TotalMilliseconds > intervalMilliseconds * 2)
                {
                    Console.WriteLine("Server lost");
                    m_ServerEndPoint = null;
                }
            }
        }

        public async void Dispose()
        {
            m_TaskCancellation.Cancel();
            try
            {
                await Task.WhenAll(KillRunningProcess(), m_HeartbeatTask, m_ListenTask);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Tasks cancelled");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            m_UdpClient.Dispose();
        }
    }
}
