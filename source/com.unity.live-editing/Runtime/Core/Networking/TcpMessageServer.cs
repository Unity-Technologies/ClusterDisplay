using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.LiveEditing.LowLevel.Networking
{
    /// <summary>
    /// A server (hub) for maintaining a many-to-many messaging network over TCP.
    /// </summary>
    /// <remarks>
    /// <p>Each <see cref="TcpMessageClient"/> that connects to the server is able to send
    /// and receive messages from every other connected <see cref="TcpMessageClient"/>.
    /// Every message received by the server is relayed to all clients (excluding the
    /// originator), emulating a "broadcast". </p>
    /// <p>This class cannot be used directly to send messages. Thus, a messaging network requires at least
    /// 2 instances of <see cref="TcpMessageClient"/> in addition to an instance of <see cref="TcpMessageServer"/>.</p>
    /// <p>TODO: Do we want to allow this class to also behave as a "client"?</p>
    /// </remarks>
    /// <seealso cref="TcpMessageClient"/>
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

        /// <summary>
        /// Returns whether we have any connection errors.
        /// </summary>
        /// <remarks>
        /// TODO: Report status in a more granular way.
        /// </remarks>
        public bool HasErrors
        {
            get
            {
                lock (m_Connections)
                {
                    return m_Connections.Any(connection => connection.CurrentException != null);
                }
            }
        }

        const int k_MaxPendingConnections = 20;

        /// <summary>
        /// The time in milliseconds after which a connection should be closed if the client cannot be reached.
        /// </summary>
        const int k_Timeout = 10 * 1000;

        /// <summary>
        /// The time between attempts to create the listener socket if it fails to start.
        /// </summary>
        static readonly TimeSpan k_RetrySocketPeriod = TimeSpan.FromSeconds(0.5);

        readonly object m_ConnectionsLock = new();

        readonly List<DataChannel> m_Connections = new();

        readonly Task m_ServerTask;
        readonly CancellationTokenSource m_CancellationTokenSource = new();
        readonly ILooper m_Looper;

        /// <summary>
        /// Creates a new <see cref="TcpMessageServer"/> instance.
        /// </summary>
        /// <param name="port">The port to listen for connections on.</param>
        /// <param name="looper">The looper determines how the server performs internal updates (e.g. maintaining
        /// connection statuses).</param>
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
                    var channel = new DataChannel(clientSocket, token, EnqueueForBroadcast) { Name = "ServerConnection" };
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
                    socket.SetKeepAlive(true, k_Timeout, 1000);
                    yield return socket;
                }
            }

            listener.Dispose();
        }

        void EnqueueForBroadcast(in PacketHeader header, ReadOnlySpan<byte> payload)
        {
            Profiler.BeginSample(nameof(EnqueueForBroadcast));
            lock (m_ConnectionsLock)
            {
                foreach (var conn in m_Connections)
                {
                    if (conn.Id != header.ChannelId)
                    {
                        conn.EnqueueSend(in header, payload);
                    }
                }
            }

            Profiler.EndSample();
        }

        void UpdateConnections()
        {
            Profiler.BeginSample(nameof(UpdateConnections));
            lock (m_ConnectionsLock)
            {
                for (var index = m_Connections.Count - 1; index >= 0; index--)
                {
                    var conn = m_Connections[index];
                    if (conn.IsStopped)
                    {
                        Debug.Log($"Lost connection with client channel {conn.Id}: {conn.CurrentException}");
                        conn.Dispose();
                        m_Connections.RemoveAt(index);
                    }
                }
            }

            Profiler.EndSample();
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
        /// <exception cref="TimeoutException">Timed out waiting for the server to stop.</exception>
        public void Stop(TimeSpan timeout)
        {
            m_CancellationTokenSource.Cancel();

            try
            {
                m_ServerTask.Wait(timeout);
            }
            catch (AggregateException e)
            {
                Debug.Log(e);
            }
            finally
            {
                foreach (var connection in m_Connections)
                {
                    connection.StartShutdown();
                }

                m_Looper.Update -= UpdateConnections;
            }
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
