using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.LiveEditing.LowLevel.Networking
{
    /// <summary>
    /// Sends and receives messages to other <see cref="TcpMessageClient"/>s connected to the same
    /// <see cref="TcpMessageServer"/>.
    /// </summary>
    class TcpMessageClient : IDisposable
    {
        public delegate void DataReceivedHandler(ReadOnlySpan<byte> data);

        /// <summary>
        /// Event raised when new data has been received.
        /// </summary>
        /// <remarks>
        /// <p>As with all event handlers, it is good practice to return as quickly as possible in order to not block
        /// the looper thread (which could be the main game thread).
        /// For example, you can copy out the data for later processing.</p>
        /// <p>The context (i.e. thread) on which the handler is invoked depends on the <see cref="ILooper"/> used
        /// in the constructor</p>.
        /// </remarks>
        public event DataReceivedHandler DataReceived;

        public Exception CurrentException => m_Connection?.CurrentException;

        const int k_ReceiveQueueSize = 32;
        readonly CancellationTokenSource m_CancellationTokenSource = new();
        readonly ILooper m_Looper;
        readonly BlockingQueue<(int size, byte[] data)> m_ReceiveQueue = new(k_ReceiveQueueSize);
        DataChannel m_Connection;

        // TODO: Better API for client status
        internal bool IsConnected => !m_Connection.HasStopped && m_Connection.Id >= 0;

        /// <summary>
        /// Creates a new <see cref="TcpMessageClient"/>.
        /// </summary>
        /// <param name="looper">The looper that controls how <see cref="DataReceived"/> is raised.</param>
        /// <remarks>
        /// To send and receive with the <see cref="TcpClient"/>, you must call <see cref="JoinMessageServerAsync"/>
        /// to join a server.
        /// </remarks>
        public TcpMessageClient(ILooper looper)
        {
            m_Looper = looper;
            m_Looper.Update += NotifyListeners;
        }

        /// <summary>
        /// Join a <see cref="TcpMessageServer"/> (messaging group).
        /// </summary>
        /// <param name="serverEndPoint">The end point of the <see cref="TcpMessageServer"/>.</param>
        /// <param name="connectionTimeout">Timeout for making a connection to the server.</param>
        /// <param name="token">A cancellation token to observe while waiting for the connection to be established.</param>
        /// <returns>An asynchronous task that completes once the client connects to the server.</returns>
        /// <exception cref="TimeoutException">Timed out attempting to connect.</exception>
        /// <exception cref="TaskCanceledException">The operation was canceled.</exception>
        public async Task JoinMessageServerAsync(IPEndPoint serverEndPoint, TimeSpan connectionTimeout, CancellationToken token)
        {
            var socket = await ConnectToServerAsync(serverEndPoint, connectionTimeout, token);
            m_Connection = new DataChannel(socket, ReceivedHandler) { Name = "ClientChannel" };
            await m_Connection.WaitForIdAssignment();
        }

        /// <summary>
        /// Send a binary blob to the other clients.
        /// </summary>
        /// <param name="buffer">The buffer containing the data to be sent.</param>
        public void Send(ReadOnlySpan<byte> buffer)
        {
            Debug.Assert(IsConnected, "The client is not connected.");
            Profiler.BeginSample("TcpMessageClient.Send");
            if (m_Connection is { HasStopped: false } connection)
            {
                var header = PacketHeader.CreateForBinaryBlob(m_Connection.Id, buffer.Length);
                connection.EnqueueSend(header, buffer);
            }

            Profiler.EndSample();
        }

        /// <summary>
        /// Send blittable data to other clients.
        /// </summary>
        /// <param name="data">A blittable object.</param>
        /// <typeparam name="T">The type of the object to send.</typeparam>
        public void Send<T>(ref T data) where T : unmanaged
        {
            Debug.Assert(IsConnected, "The client is not connected.");
            Profiler.BeginSample("TcpMessageClient.Send");
            if (m_Connection is { } connection)
            {
                var dataAsBytes = MemoryMarshal.CreateReadOnlySpan(ref data, 1).AsBytes();
                var header = PacketHeader.CreateForBinaryBlob(m_Connection.Id, dataAsBytes.Length);
                connection.EnqueueSend(header, dataAsBytes);
            }

            Profiler.EndSample();
        }

        void ReceivedHandler(in PacketHeader header, ReadOnlySpan<byte> payload)
        {
            Profiler.BeginSample(nameof(ReceivedHandler));
            var queueBuffer = ArrayPool<byte>.Shared.Rent(header.PayLoadSize);
            payload.CopyTo(queueBuffer);
            m_ReceiveQueue.TryEnqueue((header.PayLoadSize, queueBuffer));
            Profiler.EndSample();
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
                catch (SocketException e)
                {
                    // Something went wrong when trying to create the socket or trying to connect.
                    // Log the error and try again.
                    Debug.LogError(e);
                }

                token.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(0.1f), token);
            }
        }

        void NotifyListeners()
        {
            while (m_ReceiveQueue.TryDequeue(out var packetData))
            {
                DataReceived?.Invoke(packetData.data.AsSpan()[..packetData.size]);
                ArrayPool<byte>.Shared.Return(packetData.data);
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

        /// <summary>
        /// Attempt to stop the client gracefully.
        /// </summary>
        /// <exception cref="TimeoutException">Timed out waiting for the client to stop.</exception>
        /// <remarks>It is safe to call this multiple times.</remarks>
        public void Stop(TimeSpan timeout)
        {
            m_CancellationTokenSource.Cancel();
            m_ReceiveQueue.CompleteAdding();
            m_Connection?.Shutdown();
            m_Looper.Update -= NotifyListeners;
        }

        public void Dispose()
        {
            m_Connection?.Dispose();
            m_CancellationTokenSource?.Dispose();
            m_Looper.Update -= NotifyListeners;
        }
    }
}
