using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    sealed class ClusterListener : IDisposable
    {
        readonly byte[] m_SendBuffer = new byte[Constants.BufferSize];
        static readonly string k_MachineName = Environment.MachineName;
        const int k_MessageQueueSize = 16;

        UdpClient m_UdpClient;

        NodeStatus m_LatestStatus;

        CancellationTokenSource m_SubProcessCancellation;
        CancellationTokenSource m_TaskCancellation;

        Task m_ListenTask;
        Task m_SubProcessTask;
        Task m_HeartbeatTask;
        Task m_PumpMessagesTask;

        IPEndPoint m_ServerEndPoint;
        IPEndPoint m_LocalEndPoint;
        DateTime m_LastHeardFromServer = DateTime.Now;

        Channel<NodeInfo> m_MessageChannel = Channel.CreateBounded<NodeInfo>(new BoundedChannelOptions(k_MessageQueueSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

        public ClusterListener(int port = Constants.DiscoveryPort)
        {
            m_UdpClient = new UdpClient(port);

            // Allow send to fail without closing the socket.
            const int sioUdpConnReset = -1744830452;
            byte[] inValue = {0};
            byte[] outValue = {0};
            m_UdpClient.Client.IOControl(sioUdpConnReset, inValue, outValue);
        }

        public async Task Run()
        {
            Console.WriteLine("Cluster listener started");
            m_TaskCancellation = new CancellationTokenSource();
            var cancellationToken = m_TaskCancellation.Token;

            m_HeartbeatTask = DoHeartbeat(15000, cancellationToken);
            m_ListenTask = Listen(cancellationToken);
            m_PumpMessagesTask = PumpMessages(cancellationToken);

            await Task.WhenAll(m_HeartbeatTask, m_ListenTask, m_PumpMessagesTask);
        }

        void HandleServerBroadcast(in MessageHeader header, in ServerInfo _)
        {
            // Console.WriteLine($"Discovery request from {header.HostName}");
            Debug.Assert(m_UdpClient.Client.LocalEndPoint != null);

            m_LocalEndPoint ??= new IPEndPoint(
                NetworkUtils.QueryRoutingInterface(header.EndPoint),
                ((IPEndPoint) m_UdpClient.Client.LocalEndPoint).Port);

            m_ServerEndPoint = header.EndPoint;
            m_LastHeardFromServer = DateTime.Now;
            ReportNodeStatus(m_LatestStatus);
        }

        async Task Listen(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var receiveResult = await m_UdpClient.ReceiveAsync().WithCancellation(token);
                if (!receiveResult.Buffer.MatchMessage<ServerInfo, LaunchInfo, DirectorySyncInfo, KillInfo>(
                    HandleServerBroadcast,
                    (in MessageHeader _, in LaunchInfo launchInfo) =>
                    {
                        m_SubProcessTask = RunPlayer(launchInfo);
                    },
                    (in MessageHeader _, in DirectorySyncInfo syncInfo) =>
                    {
                        m_SubProcessTask = SyncPlayerFiles(syncInfo);
                    },
                    (in MessageHeader _, in KillInfo _) =>
                    {
                        KillRunningProcess();
                        ReportNodeStatus(NodeStatus.Ready);
                    }))
                {
                    Console.WriteLine("Unsupported message type received");
                };
            }
        }

        void KillRunningProcess()
        {
            m_SubProcessCancellation?.Cancel();
            if (m_SubProcessTask is {Status: TaskStatus.Running})
            {
                try
                {
                    m_SubProcessTask.Wait(5000);
                    Console.WriteLine("Subprocess killed");
                }
                catch (AggregateException e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            m_SubProcessTask = null;
        }

        async Task<bool> RunAndReportErrors(Task task)
        {
            try
            {
                await task;
                return task.IsCompletedSuccessfully;
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.InnerExceptions)
                {
                    ReportNodeStatus(NodeStatus.Error, exception.Message);
                }
            }
            catch (Exception e)
            {
                ReportNodeStatus(NodeStatus.Error, e.Message);
            }

            return false;
        }

        async Task SyncPlayerFiles(DirectorySyncInfo syncInfo)
        {
            Console.WriteLine("Project sync requested");
            KillRunningProcess();
            m_SubProcessCancellation = new CancellationTokenSource();
            
            ReportNodeStatus(NodeStatus.SyncFiles);
            if (await RunAndReportErrors(Launcher.SyncProjectDir(syncInfo.RemoteDirectory, m_SubProcessCancellation.Token)))
            {
                Console.Write("Sync successful");
                ReportNodeStatus(NodeStatus.Ready);
            }
        }

        async Task RunPlayer(LaunchInfo launchInfo)
        {
            Console.WriteLine("Launch requested");
            
            KillRunningProcess();
            m_SubProcessCancellation = new CancellationTokenSource();
            
            ReportNodeStatus(NodeStatus.Running);
            await RunAndReportErrors(Launcher.Launch(launchInfo, m_SubProcessCancellation.Token));
            ReportNodeStatus(NodeStatus.Ready);
        }

        void ReportNodeStatus(NodeStatus status, string message = null)
        {
            m_LatestStatus = status;
            var canWrite = m_MessageChannel.Writer.TryWrite(new NodeInfo(status, message));
            Debug.Assert(canWrite);
        }

        async Task PumpMessages(CancellationToken token)
        {
            var reader = m_MessageChannel.Reader;
            while (await reader.WaitToReadAsync(token))
            {
                while (m_ServerEndPoint != null && reader.TryRead(out var nodeInfo))
                {
                    var size = m_SendBuffer.WriteMessage(k_MachineName, m_LocalEndPoint, nodeInfo);

                    // Console.WriteLine($"Sending status {nodeInfo.Status} to {m_ServerEndPoint}");
                    var bytesSent = await m_UdpClient.SendAsync(m_SendBuffer, size, m_ServerEndPoint).WithCancellation(token);
                    if (bytesSent < size)
                    {
                        Console.WriteLine($"Sent {bytesSent}; expected {size}");
                    }
                }
            }
        }

        async Task DoHeartbeat(int intervalMilliseconds, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (m_ServerEndPoint != null && (DateTime.Now - m_LastHeardFromServer).TotalMilliseconds > intervalMilliseconds * 2)
                {
                    Console.WriteLine("Server lost");
                    m_ServerEndPoint = null;
                }

                await Task.Delay(intervalMilliseconds, token);
            }
        }

        public async void Dispose()
        {
            m_MessageChannel.Writer.Complete();
            m_TaskCancellation.Cancel();
            try
            {
                KillRunningProcess();
                await Task.WhenAll(m_HeartbeatTask, m_ListenTask);
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
