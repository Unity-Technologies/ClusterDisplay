using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    [Serializable]
    public struct ExtendedNodeInfo : IEquatable<ExtendedNodeInfo>
    {
        public readonly string Name;

        public NodeInfo Info { get; }

        public IPAddress Address { get; }

        public int Port { get; }

        public DateTime LastResponse { get; set; }

        public int Id => Address.GetHashCode();

        public ExtendedNodeInfo(NodeInfo nodeInfo, MessageHeader header)
        {
            Info = nodeInfo;
            Address = header.Address;
            Name = header.HostName;
            Port = header.Port;
            LastResponse = DateTime.Now;
        }

        public bool Equals(ExtendedNodeInfo other)
        {
            return Info.Equals(other.Info) && Equals(Address, other.Address);
        }

        public override bool Equals(object obj)
        {
            return obj is ExtendedNodeInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Info.GetHashCode() * 397) ^ (Address != null ? Address.GetHashCode() : 0);
            }
        }

        public static bool operator ==(ExtendedNodeInfo left, ExtendedNodeInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ExtendedNodeInfo left, ExtendedNodeInfo right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class Server : IDisposable
    {
        UdpClient m_UdpClient = new(port: 0);
        Dictionary<int, ExtendedNodeInfo> m_ActiveNodes = new();

        static readonly IPEndPoint k_DiscoveryEndPoint = new(IPAddress.Broadcast, Constants.DiscoveryPort);

        IEnumerable<ExtendedNodeInfo> Nodes => m_ActiveNodes.Values;

        public event Action<ExtendedNodeInfo> NodeAdded;

        public event Action<ExtendedNodeInfo> NodeRemoved;

        public event Action<ExtendedNodeInfo> NodeUpdated;

        static readonly string k_MachineName = Environment.MachineName;
        const int k_MaxResponseTime = 15;
        IPEndPoint m_LocalEndPoint;

        CancellationTokenSource m_CancellationTokenSource;

        public async Task Run()
        {
            var localPort = ((IPEndPoint) m_UdpClient.Client.LocalEndPoint).Port;
            var localAddress = NetworkUtils.GetLocalIPAddress();
            m_LocalEndPoint = new IPEndPoint(localAddress, localPort);

            m_CancellationTokenSource?.Dispose();
            m_CancellationTokenSource = new CancellationTokenSource();
            var discoveryTask = DoDiscovery(m_CancellationTokenSource.Token);
            var pruneTask = PruneNodes(m_CancellationTokenSource.Token);
            var listenTask = ListenForNodeResponses(m_CancellationTokenSource.Token);

            await Task.WhenAny(discoveryTask, pruneTask, listenTask);
        }

        public void Dispose()
        {
            if (m_CancellationTokenSource != null)
            {
                m_CancellationTokenSource.Cancel();
                m_CancellationTokenSource.Dispose();
            }

            m_UdpClient.Dispose();
        }

        async Task PruneNodes(CancellationToken token)
        {
            while (true)
            {
                await Task.Delay(5000, token);
                var now = DateTime.Now;
                var deadNodes = Nodes.Where(node => (now - node.LastResponse).TotalSeconds > k_MaxResponseTime).ToList();
                foreach (var node in deadNodes)
                {
                    OnNodeRemoved(node);
                    m_ActiveNodes.Remove(node.Id);
                }
            }
        }

        async Task DoDiscovery(CancellationToken token)
        {
            var dgram = new byte[Constants.BufferSize];
            var size = dgram.WriteMessage(k_MachineName, m_LocalEndPoint, new ServerInfo());

            while (true)
            {
                await m_UdpClient.SendAsync(dgram, size, k_DiscoveryEndPoint).WithCancellation(token);
                await Task.Delay(5000, token);
            }
        }

        async Task ListenForNodeResponses(CancellationToken token)
        {
            while (true)
            {
                var result = await m_UdpClient.ReceiveAsync().WithCancellation(token);
                if (result.Buffer.MatchMessage<NodeInfo>() is var (header, nodeInfo))
                {
                    var newItem = new ExtendedNodeInfo(nodeInfo, header)
                    {
                        LastResponse = DateTime.Now
                    };
                    if (!m_ActiveNodes.TryGetValue(newItem.Id, out var node))
                    {
                        m_ActiveNodes.Add(newItem.Id, newItem);
                        OnNodeAdded(newItem);
                    }
                    else if (node != newItem)
                    {
                        OnNodeUpdated(newItem);
                    }

                    m_ActiveNodes[newItem.Id] = newItem;
                }
            }
        }

        async Task WaitForStatus(int nodeId, NodeStatus targetStatus)
        {
            var tcs = new TaskCompletionSource<bool>();
            var handler = new Action<ExtendedNodeInfo>(nodeInfo =>
            {
                if (nodeId == nodeInfo.Id && targetStatus == nodeInfo.Info.Status)
                {
                    tcs.SetResult(true);
                }
            });

            NodeUpdated += handler;
            await tcs.Task;
            NodeUpdated -= handler;
        }

        async Task SyncProjectNodes(IReadOnlyList<(ExtendedNodeInfo, LaunchInfo)> launchData, CancellationToken token)
        {
            var dgram = new byte[Constants.BufferSize];
            List<Task> allSyncTasks = new List<Task>();
            foreach (var (node, launchInfo) in launchData)
            {
                var size = dgram.WriteMessage(k_MachineName,
                    m_LocalEndPoint,
                    new ProjectSyncInfo(launchInfo.ProjectDir));
                var remoteEndPoint = new IPEndPoint(node.Address, node.Port);
                await m_UdpClient.SendAsync(dgram, size, remoteEndPoint);
                await WaitForStatus(node.Id, NodeStatus.SyncFiles).WithCancellation(token);
                allSyncTasks.Add(WaitForStatus(node.Id, NodeStatus.Ready));
            }

            await Task.WhenAll(allSyncTasks);
        }

        public async Task Launch(IEnumerable<(ExtendedNodeInfo, LaunchInfo)> launchData, CancellationToken token)
        {
            var dgram = new byte[Constants.BufferSize];

            var data = launchData.ToList();

            await SyncProjectNodes(data, token);

            foreach (var (node, launchInfo) in data)
            {
                var size = dgram.WriteMessage(k_MachineName,
                    m_LocalEndPoint,
                    new LaunchInfo(launchInfo.ProjectDir,
                        launchInfo.NodeID,
                        launchInfo.NumRepeaters));
                var remoteEndPoint = new IPEndPoint(node.Address, node.Port);
                await m_UdpClient.SendAsync(dgram, size, remoteEndPoint);
            }
        }

        public async void StopAll()
        {
            var dgram = new byte[Constants.BufferSize];

            foreach (var node in Nodes.ToList())
            {
                var size = dgram.WriteMessage(k_MachineName, m_LocalEndPoint, new KillInfo());
                var remoteEndPoint = new IPEndPoint(node.Address, node.Port);
                await m_UdpClient.SendAsync(dgram, size, remoteEndPoint);
            }
        }

        void OnNodeAdded(ExtendedNodeInfo extendedNode)
        {
            NodeAdded?.Invoke(extendedNode);
        }

        void OnNodeRemoved(ExtendedNodeInfo extendedNode)
        {
            NodeRemoved?.Invoke(extendedNode);
        }

        void OnNodeUpdated(ExtendedNodeInfo obj)
        {
            // Debug.Log($"Node changed {obj.Address}[{obj.Info.Status}]");
            NodeUpdated?.Invoke(obj);
        }
    }
}
