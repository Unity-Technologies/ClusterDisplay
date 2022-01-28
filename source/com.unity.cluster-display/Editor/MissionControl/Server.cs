using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEngine;
#endif

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
        public event Action<ExtendedNodeInfo> NodeAdded;
        public event Action<ExtendedNodeInfo> NodeRemoved;
        public event Action<ExtendedNodeInfo> NodeUpdated;

        const int k_MaxResponseTime = 15;
        static readonly IPEndPoint k_DiscoveryEndPoint = new(IPAddress.Broadcast, Constants.DiscoveryPort);
        static readonly string k_MachineName = Environment.MachineName;

        UdpClient m_UdpClient;
        IPEndPoint m_LocalEndPoint;
        Dictionary<int, ExtendedNodeInfo> m_ActiveNodes = new();

        IEnumerable<ExtendedNodeInfo> Nodes => m_ActiveNodes.Values;

        CancellationTokenSource m_CancellationTokenSource;

        public Server(int port = 0)
        {
            m_UdpClient = new UdpClient(port);
        }

        public async Task Run()
        {
            var localPort = ((IPEndPoint) m_UdpClient.Client.LocalEndPoint).Port;
            var localAddress = NetworkUtils.GetLocalIPAddress();
            m_LocalEndPoint = new IPEndPoint(localAddress, localPort);

            m_CancellationTokenSource?.Dispose();
            m_CancellationTokenSource = new CancellationTokenSource();
            var discoveryTask = DoDiscovery(m_CancellationTokenSource.Token);
            var pruneTask = PruneInactiveNodes(m_CancellationTokenSource.Token);
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

        async Task PruneInactiveNodes(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
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

            while (!token.IsCancellationRequested)
            {
                await m_UdpClient.SendAsync(dgram, size, k_DiscoveryEndPoint).WithCancellation(token);
                await Task.Delay(5000, token);
            }
        }

        async Task ListenForNodeResponses(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var result = await m_UdpClient.ReceiveAsync().WithCancellation(token);
                result.Buffer.MatchMessage<NodeInfo>(HandleNodeMessage);
            }
        }

        void HandleNodeMessage(in MessageHeader header, in NodeInfo nodeInfo)
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
                LogNodeError(ref newItem);
            }

            m_ActiveNodes[newItem.Id] = newItem;
        }

        static void LogNodeError(ref ExtendedNodeInfo nodeInfo)
        {
            if (nodeInfo.Info.Status == NodeStatus.Error)
            {
#if UNITY_EDITOR
                Debug.Log($"[{nodeInfo.Name}]: {nodeInfo.Info.LogMessage}");
#else
                Console.WriteLine($"[{nodeInfo.Name}]: {nodeInfo.Info.LogMessage}");
#endif
            }
        }

        async Task<NodeStatus> WaitForStatusChange(int nodeId, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<NodeStatus>();

            void UpdatedHandler(ExtendedNodeInfo nodeInfo)
            {
                if (nodeId == nodeInfo.Id)
                {
                    tcs.TrySetResult(nodeInfo.Info.Status);
                }
            }

            void RemovedHandler(ExtendedNodeInfo nodeInfo)
            {
                if (nodeId == nodeInfo.Id)
                {
                    tcs.TrySetResult(NodeStatus.Error);
                }
            }

            NodeUpdated += UpdatedHandler;
            NodeRemoved += RemovedHandler;

            var result = await tcs.Task.WithCancellation(token);

            NodeUpdated -= UpdatedHandler;
            NodeRemoved -= RemovedHandler;

            return result;
        }

        async Task<NodeStatus> SyncPlayerFiles(ExtendedNodeInfo nodeInfo, LaunchInfo launchInfo, CancellationToken token)
        {
            var dgram = new byte[Constants.BufferSize];
            var size = dgram.WriteMessage(k_MachineName,
                m_LocalEndPoint,
                new DirectorySyncInfo(launchInfo.PlayerDir));
            var remoteEndPoint = new IPEndPoint(nodeInfo.Address, nodeInfo.Port);
            
            // Instruct node to perform sync
            await m_UdpClient.SendAsync(dgram, size, remoteEndPoint);
            
            // Wait for sync operation to start before continuing
            if (await WaitForStatusChange(nodeInfo.Id, token) != NodeStatus.SyncFiles)
            {
                return NodeStatus.Error;
            }

            // await WaitForStatus(node.Id, NodeStatus.SyncFiles).WithCancellation(token);
            return await WaitForStatusChange(nodeInfo.Id, token);
        }

        async Task<bool> SyncPlayerFiles(IEnumerable<(ExtendedNodeInfo nodeInfo, LaunchInfo launchInfo)> launchData, CancellationToken token)
        {
            try
            {
                var allSyncTasks = launchData.Select(t => SyncPlayerFiles(t.nodeInfo, t.launchInfo, token));
                var results = await Task.WhenAll(allSyncTasks);
                return results.All(s => s == NodeStatus.Ready);
            }
            catch (TaskCanceledException)
            {
                // Don't need to do anything if the task was prematurely cancelled.
            }

            return false;
        }

        public async Task LaunchPlayer(ExtendedNodeInfo nodeInfo, LaunchInfo launchInfo, CancellationToken token)
        {
            var dgram = new byte[Constants.BufferSize];
            var size = dgram.WriteMessage(k_MachineName, m_LocalEndPoint, launchInfo);
            var remoteEndPoint = new IPEndPoint(nodeInfo.Address, nodeInfo.Port);
            await m_UdpClient.SendAsync(dgram, size, remoteEndPoint);
        }
        
        public async Task<bool> SyncAndLaunch(IEnumerable<(ExtendedNodeInfo, LaunchInfo)> launchData, CancellationToken token)
        {

            var data = launchData.ToList();

            // Wait for all nodes to finish syncing files
            if (!await SyncPlayerFiles(data, token))
            {
                throw new Exception("Sync failed");
            }
            foreach (var (node, launchInfo) in data)
            {
                await LaunchPlayer(node, launchInfo, token);
            }

            return true;
        }

        public async Task StopAll()
        {
            var dgram = new byte[Constants.BufferSize];

            foreach (var node in Nodes)
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
            NodeUpdated?.Invoke(obj);
        }
    }
}
