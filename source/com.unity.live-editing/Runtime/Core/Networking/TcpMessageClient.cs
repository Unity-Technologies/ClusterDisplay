using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.LiveEditing.LowLevel.Networking
{
    class TcpMessageClient : IDisposable
    {
        public TimeSpan ConnectionAttemptTimeout { get; set; } = TimeSpan.FromSeconds(2);
        public int Id => m_Connection?.Id ?? -1;

        public delegate void DataReceivedHandler(ReadOnlySpan<byte> data);

        public event DataReceivedHandler DataReceived;
        public Exception CurrentException => m_Connection?.CurrentException;

        readonly CancellationTokenSource m_CancellationTokenSource = new();
        readonly Task m_ClientTask;
        readonly ILooper m_Looper;
        readonly BlockingCollection<(int size, byte[] data)> m_ReceiveQueue = new();
        DataChannel m_Connection;

        public TcpMessageClient(IPEndPoint server, ILooper looper)
        {
            m_Looper = looper;
            m_Looper.Update += NotifyListeners;
            m_ClientTask = ConnectToHubTask(server, ConnectionAttemptTimeout, m_CancellationTokenSource.Token);
        }

        public void Send(ReadOnlySpan<byte> buffer)
        {
            if (m_Connection is { } connection)
            {
                var header = PacketHeader.CreateForBinaryBlob(m_Connection.Id, buffer.Length);
                connection.EnqueueSend(header, buffer);
            }
        }

        async Task ConnectToHubTask(IPEndPoint serverEndPoint, TimeSpan timeout, CancellationToken token)
        {
            var socket = await ConnectToServerAsync(serverEndPoint, timeout, token);
            m_Connection = new DataChannel(socket, token, ReceivedHandler);
            await m_Connection.WaitForIdAssignment();
        }

        void ReceivedHandler(in PacketHeader header, ReadOnlySpan<byte> payload)
        {
            var queueBuffer = ArrayPool<byte>.Shared.Rent(header.PayLoadSize);
            payload.CopyTo(queueBuffer);
            m_ReceiveQueue.Add((header.PayLoadSize, queueBuffer), m_CancellationTokenSource.Token);
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
                DataReceived?.Invoke(packetData.data[..packetData.size]);
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
        /// Attempt to stop the client gracefully
        /// </summary>
        /// <exception cref="TimeoutException"></exception>
        public void Stop(TimeSpan timeout)
        {
            m_CancellationTokenSource.Cancel();
            try
            {
                m_ClientTask.Wait(timeout);
            }
            catch (AggregateException e)
            {
                Debug.Log(e);
            }
            finally
            {
                m_Looper.Update -= NotifyListeners;
            }
        }

        public void Dispose()
        {
            m_Connection?.Dispose();
            m_CancellationTokenSource?.Dispose();
        }
    }
}
