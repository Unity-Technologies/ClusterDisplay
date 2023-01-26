using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static System.Threading.Timeout;

namespace Unity.LiveEditing.LowLevel.Networking
{
    /// <summary>
    /// Packet information for internal use.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    readonly struct PacketHeader
    {
        // TODO: add extra metadata here
        public readonly int PayLoadSize;

        public PacketHeader(int payLoadSize)
        {
            PayLoadSize = payLoadSize;
        }
    }

    readonly struct QueuedPacket : IDisposable
    {
        public readonly PacketHeader Header;
        public readonly byte[] PayloadData;

        public QueuedPacket(PacketHeader header, ReadOnlySpan<byte> data)
        {
            Header = header;
            PayloadData = ArrayPool<byte>.Shared.Rent(header.PayLoadSize);
            data[..header.PayLoadSize].CopyTo(PayloadData);
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(PayloadData);
        }
    }

    static class PacketTransport
    {
        static readonly int k_HeaderSize = Marshal.SizeOf<PacketHeader>();

        public static PacketHeader ReadPacket(this Socket socket, Span<byte> payloadDataOut)
        {
            var bytesRead = 0;
            Span<byte> headerDataBuffer = stackalloc byte[k_HeaderSize];
            while (bytesRead < k_HeaderSize)
            {
                bytesRead += socket.Receive(headerDataBuffer[bytesRead..k_HeaderSize]);
            }

            var header = MemoryMarshal.Read<PacketHeader>(headerDataBuffer);
            Debug.Log($"Received packet {header.PayLoadSize}");
            bytesRead = 0;
            while (bytesRead < header.PayLoadSize)
            {
                bytesRead += socket.Receive(payloadDataOut[bytesRead..header.PayLoadSize]);
            }

            return header;
        }
    }

    class LiveEditTcpServer : IDisposable
    {
        public TimeSpan Timeout { get; } = TimeSpan.FromSeconds(0.5);

        public int Port { get; }

        public int ClientCount
        {
            get
            {
                lock (m_ConnectionLock)
                {
                    return m_Connections.Count;
                }
            }
        }

        const int k_MaxPendingConnections = 20;
        const int k_ReceiveBufferSize = 1024 * 64; // 64k should be enough for everybody ;)
        const int k_SendQueueMaxLength = 32;

        /// <summary>
        /// The time between attempts to create the listener socket if it fails to start.
        /// </summary>
        static readonly TimeSpan k_RetrySocketPeriod = TimeSpan.FromSeconds(0.5);

        readonly object m_ConnectionLock = new();

        readonly List<ClientConnection> m_Connections = new();

        struct ClientConnection : IDisposable
        {
            public Socket Socket;
            public Task ReceiveTask;
            public Task SendTask;
            public BlockingCollection<QueuedPacket> OutgoingPackets;
            public CancellationTokenSource CancellationTokenSource;

            public void Dispose()
            {
                CancellationTokenSource?.Cancel();
                try
                {
                    Task.WaitAll(new[] { ReceiveTask, SendTask }, 500);
                }
                catch (AggregateException e)
                {
                    Debug.Log(e);
                }

                Socket?.Close();
                if (OutgoingPackets != null)
                {
                    while (OutgoingPackets.TryTake(out var packet))
                    {
                        packet.Dispose();
                    }
                }

                Debug.Log("Connection closed");
            }
        }

        readonly Task m_ServerTask;
        readonly CancellationTokenSource m_CancellationTokenSource = new();
        readonly ILooper m_Looper;

        public LiveEditTcpServer(int port, ILooper looper)
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
                lock (m_ConnectionLock)
                {
                    var clientConnection = new ClientConnection()
                    {
                        Socket = clientSocket,
                        ReceiveTask = ReceiveFromClientAsync(clientSocket,
                            token),
                        OutgoingPackets = new(k_SendQueueMaxLength),
                        CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token)
                    };
                    clientConnection.SendTask = SendPacketsInQueueAsync(clientConnection.Socket, clientConnection.OutgoingPackets, token);
                    m_Connections.Add(clientConnection);
                }
            }
        }

        Task ReceiveFromClientAsync(Socket socket, CancellationToken token)
        {
            return Task.Run(() =>
            {
                Span<byte> receiveBuffer = stackalloc byte[k_ReceiveBufferSize];
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var header = socket.ReadPacket(receiveBuffer);
                        Debug.Log("Server received packet");
                        EnqueueForBroadcast(header, receiveBuffer, socket, token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Don't need to bubble up cancellations
                    }
                }
            }, token).ContinueWith(_ =>
            {
                Debug.Log("ReceiveFromClientAsync complete!");
            }, token);
        }

        static void SendPacket(Socket socket, in QueuedPacket packet)
        {
            socket.Send(MemoryExtensions.CreateReadOnlySpan(in packet.Header).AsBytes());
            socket.Send(packet.PayloadData, packet.Header.PayLoadSize, SocketFlags.None);
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

        static Task SendPacketsInQueueAsync(Socket destination, BlockingCollection<QueuedPacket> queue, CancellationToken token)
        {
            return Task.Run(() =>
            {
                try
                {
                    Debug.Log($"Server waiting for packet {queue.GetHashCode()}");
                    while (queue.TryTake(out var packet, Infinite, token))
                    {
                        Debug.Log("Server sending out packet");
                        SendPacket(destination, in packet);
                        packet.Dispose();
                    }
                    Debug.LogError("Whoops. Shouldn't happen.");
                }
                catch (OperationCanceledException)
                {
                    // Don't bubble up cancellations
                }
            }, token).ContinueWith(_ =>
            {
                Debug.Log("SendPacketsInQueueAsync complete");
            });
        }

        void EnqueueForBroadcast(in PacketHeader header, ReadOnlySpan<byte> payload, Socket sender, CancellationToken token)
        {
            lock (m_ConnectionLock)
            {
                foreach (var conn in m_Connections)
                {
                    if (conn.Socket != sender)
                    {
                        Debug.Log($"Server enqueuing packet to send {conn.OutgoingPackets.GetHashCode()}");
                        conn.OutgoingPackets.Add(new QueuedPacket(header, payload), token);
                    }
                }
            }
        }

        void UpdateConnections()
        {
            lock (m_ConnectionLock)
            {
                for (var index = m_Connections.Count - 1; index >= 0; index--)
                {
                    var conn = m_Connections[index];
                    if (conn.ReceiveTask.IsCompleted || conn.SendTask.IsCompleted)
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

        void Stop()
        {
            m_CancellationTokenSource.Cancel();
            m_ServerTask.Wait(Timeout);
            m_Looper.Update -= UpdateConnections;
        }

        public void Dispose()
        {
            Stop();
            lock (m_Connections)
            {
                foreach (var connection in m_Connections)
                {
                    connection.Dispose();
                }
            }
            m_CancellationTokenSource?.Dispose();
        }
    }

    class LiveEditTcpClient : IDisposable
    {
        public TimeSpan ConnectionAttemptTimeout { get; set; } = TimeSpan.FromSeconds(2);
        public event Action<byte[]> DataReceived;

        const int k_ReceiveBufferSize = 1024 * 64;
        readonly CancellationTokenSource m_CancellationTokenSource = new();
        readonly Task m_ClientTask;
        readonly ILooper m_Looper;
        readonly BlockingCollection<byte[]> m_ReceiveQueue = new();
        Socket m_Socket;

        public LiveEditTcpClient(IPEndPoint server, ILooper looper)
        {
            m_Looper = looper;
            m_Looper.Update += NotifyListeners;
            m_ClientTask = RunClientAsync(server, ConnectionAttemptTimeout, m_CancellationTokenSource.Token);
        }

        public void Send(ReadOnlySpan<byte> buffer)
        {
            if (m_Socket is { } socket)
            {
                var header = new PacketHeader(buffer.Length);
                socket.Send(MemoryExtensions.CreateReadOnlySpan(header).AsBytes());
                socket.Send(buffer);
            }
        }

        public async Task SendAsync(Memory<byte> buffer, CancellationToken token)
        {
            if (m_Socket is { } socket)
            {
                var header = new PacketHeader(buffer.Length);
                var tempBuffer = ArrayPool<byte>.Shared.Rent(Marshal.SizeOf<PacketHeader>());
                MemoryMarshal.Write(tempBuffer, ref header);
                try
                {
                    await socket.SendAsync(tempBuffer, SocketFlags.None, token);
                    await socket.SendAsync(buffer, SocketFlags.None, token);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tempBuffer);
                }
            }
        }

        Task ReceiveAsync(CancellationToken token)
        {
            return Task.Run(() =>
            {
                Debug.Assert(m_Socket != null);
                Span<byte> receiveBuffer = stackalloc byte[k_ReceiveBufferSize];
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var header = m_Socket.ReadPacket(receiveBuffer);
                        // Debug.Log($"Client received packet {header.PayLoadSize}");
                        m_ReceiveQueue.Add(receiveBuffer[..header.PayLoadSize].ToArray(), token);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }, token);
        }

        async Task RunClientAsync(IPEndPoint serverEndPoint, TimeSpan timeout, CancellationToken token)
        {
            m_Socket = await ConnectToServerAsync(serverEndPoint, timeout, token);
            await ReceiveAsync(token);
        }

        static async Task<Socket> ConnectToServerAsync(IPEndPoint serverEndPoint, TimeSpan timeout, CancellationToken token)
        {
            while (true)
            {
                try
                {
                    var socket = CreateSocket(IPAddress.Any);
                    await socket.ConnectAsync(serverEndPoint).WithCancellation(token).WithTimeout(timeout);
                    Debug.Log($"Client connected to {serverEndPoint}");
                    return socket;
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }

                token.ThrowIfCancellationRequested();
                await Task.Delay(timeout, token);
            }
        }

        void NotifyListeners()
        {
            while (m_ReceiveQueue.TryTake(out var packetData))
            {
                DataReceived?.Invoke(packetData);
            }
        }

        static Socket CreateSocket(IPAddress localInterface)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Disable Nagle's Algorithm. This helps to reduce latency when fewer, smaller message are being sent.
            socket.NoDelay = true;

            // By default tcp sockets will persist after being closed in order to ensure all
            // data has been send and received successfully, but this will block the port for a while.
            // We need to disable this behaviour so the socket closes immediately.
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, false);
            socket.LingerState = new LingerOption(true, 0);
            socket.Bind(new IPEndPoint(localInterface, 0));

            return socket;
        }

        void Stop()
        {
            m_Socket?.Close();
            m_CancellationTokenSource.Cancel();
            try
            {
                m_ClientTask.Wait(ConnectionAttemptTimeout);
            }
            catch (AggregateException e)
            {
                Debug.Log(e);
            }

            m_Looper.Update -= NotifyListeners;
        }

        public void Dispose()
        {
            Stop();
            m_Socket?.Dispose();
            m_CancellationTokenSource?.Dispose();
        }
    }
}
