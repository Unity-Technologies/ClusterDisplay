using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.LiveEditing.LowLevel.Networking
{
    /// <summary>
    /// A hub for maintaining a many-to-many messaging network for TCP.
    /// </summary>
    class TcpMessageServer : IDisposable
    {
        public int Port { get; }

        public int ClientCount
        {
            get
            {
                lock (m_ConnectionsLock)
                {
                    return m_Connections.Count;
                }
            }
        }

        const int k_MaxPendingConnections = 20;

        /// <summary>
        /// The time between attempts to create the listener socket if it fails to start.
        /// </summary>
        static readonly TimeSpan k_RetrySocketPeriod = TimeSpan.FromSeconds(0.5);

        readonly object m_ConnectionsLock = new();

        readonly List<DataChannel> m_Connections = new();

        readonly Task m_ServerTask;
        readonly CancellationTokenSource m_CancellationTokenSource = new();
        readonly ILooper m_Looper;

        public TcpMessageServer(int port, ILooper looper)
        {
            Port = port;
            m_ServerTask = RunServerAsync(m_CancellationTokenSource.Token);
            m_Looper = looper;
            m_Looper.Update += UpdateConnections;
        }

        async Task RunServerAsync(CancellationToken token)
        {
            await foreach (var clientSocket in ListenForConnectionsAsync(Port, token))
            {
                lock (m_ConnectionsLock)
                {
                    var nextId = m_Connections.Count;
                    var channel = new DataChannel(clientSocket, token, EnqueueForBroadcast);
                    channel.EnqueueIdChange(nextId);
                    m_Connections.Add(channel);
                }
            }
        }

        static async IAsyncEnumerable<Socket> ListenForConnectionsAsync(int port, [EnumeratorCancellation] CancellationToken token)
        {
            var listener = default(Socket);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    listener = CreateSocket(port);
                    break;
                }
                catch (Exception e)
                {
                    Debug.Log($"Error creating listener socket. Retrying.\n{e}");
                    await Task.Delay(k_RetrySocketPeriod, token);
                }
            }

            Debug.Assert(listener != null);
            while (!token.IsCancellationRequested)
            {
                Debug.Log($"Listening for connections...");
                Socket socket = null;
                try
                {
                    socket = await listener.AcceptAsync().WithCancellation(token);
                }
                catch
                {
                    listener.Dispose();
                }

                if (socket != null)
                {
                    Debug.Log($"Connected to client {socket.RemoteEndPoint}");
                    yield return socket;
                }
            }

            listener.Dispose();
        }

        void EnqueueForBroadcast(in PacketHeader header, ReadOnlySpan<byte> payload)
        {
            lock (m_ConnectionsLock)
            {
                foreach (var conn in m_Connections)
                {
                    if (conn.Id != header.ChannelId)
                    {
                        Debug.Log($"Enqueue for broadcast [{header.PayLoadSize}]");
                        conn.EnqueueSend(in header, payload);
                    }
                }
            }
        }

        void UpdateConnections()
        {
            lock (m_ConnectionsLock)
            {
                for (var index = m_Connections.Count - 1; index >= 0; index--)
                {
                    var conn = m_Connections[index];
                    if (conn.IsStopped)
                    {
                        conn.Dispose();
                        m_Connections.RemoveAt(index);

                        Debug.Log($"Removing connection {index}");
                    }
                }
            }
        }

        static Socket CreateSocket(int port)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));

            socket.Listen(k_MaxPendingConnections);

            return socket;
        }

        /// <summary>
        /// Attempt to stop the server gracefully.
        /// </summary>
        void Stop(TimeSpan timeout)
        {
            m_CancellationTokenSource.Cancel();
            m_ServerTask.Wait(timeout);
        }

        public void Dispose()
        {
            lock (m_Connections)
            {
                foreach (var connection in m_Connections)
                {
                    connection.Dispose();
                }
            }
            m_CancellationTokenSource?.Dispose();
            m_Looper.Update -= UpdateConnections;
        }
    }
}
