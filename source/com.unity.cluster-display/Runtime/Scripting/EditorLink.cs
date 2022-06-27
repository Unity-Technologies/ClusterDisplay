using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.PlayerLoop;
using Unity.ClusterDisplay.Utils;

namespace Unity.ClusterDisplay.Scripting
{
    interface IMessageProcessor
    {
        void Dispatch(ReadOnlySpan<byte> data);
    }

    class MessageHandlerWrapper<T> : IMessageProcessor where T : unmanaged
    {
        Action<T> m_Handler;

        public MessageHandlerWrapper(Action<T> handler)
        {
            m_Handler = handler;
        }

        public void Dispatch(ReadOnlySpan<byte> data)
        {
            m_Handler(data.LoadStruct<T>());
        }
    }

    class EditorLink : IDisposable
    {
        EditorLinkConfig m_Config;
        UdpClient m_UdpClient;
        CancellationTokenSource m_CancellationTokenSource = new();
        Dictionary<Guid, IMessageProcessor> m_EventHandlers = new();

        readonly ConcurrentQueue<byte[]> m_ReceivedBytes = new();
        Task m_ReceiveTask;

        struct EditorLinkUpdate { }

        public bool IsTransmitter { get; }

        public EditorLink(EditorLinkConfig config, bool isTransmitter)
        {
            m_Config = config;
            IsTransmitter = isTransmitter;
            if (IsTransmitter)
            {
                ClusterDebug.Log("[Editor Link] Initializing transmitter (this is an Editor instance)");
                m_UdpClient = new UdpClient();
            }
            else
            {
                ClusterDebug.Log($"[Editor Link] Initializing sink (this is an Emitter) - listening on port {m_Config.Port}");
                m_UdpClient = new UdpClient(m_Config.Port);
            }

            m_UdpClient.Client.SendTimeout = 200;

            PlayerLoopExtensions.RegisterUpdate<PreLateUpdate, EditorLinkUpdate>(ProcessIncomingMessages);
            if (!IsTransmitter)
            {
                m_ReceiveTask = ReceiveRawData(m_CancellationTokenSource.Token);
            }
        }

        public void Publish<T>(ReplicationMessage<T> message) where T : unmanaged
        {
#if UNITY_EDITOR
            var endPoint = m_Config.EndPoint;

            var dgram = new byte[Marshal.SizeOf(message)];
            var bytes = message.StoreInBuffer(dgram);

            ClusterDebug.Log($"[Editor Link] sending message to {endPoint} : {message.Guid}, {message.Contents}");

            m_UdpClient.Send(dgram, bytes, endPoint);
#else
            Debug.LogWarning("You cannot publish a Live Link message in a standalone player");
#endif
        }

        public void ConnectReceiver<T>(Guid guid, Action<ReplicationMessage<T>> handler) where T : unmanaged
        {
            m_EventHandlers.Add(guid,
                new MessageHandlerWrapper<ReplicationMessage<T>>(handler));
        }

        public void DisconnectReceiver(Guid guid)
        {
            m_EventHandlers.Remove(guid);
        }

        Task ReceiveRawData(CancellationToken token)
        {
            return Task.Run(() =>
            {
                ClusterDebug.Log("[Editor Link] Start receiving");
                IPEndPoint remoteEndPoint = default;

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var bytes = m_UdpClient.Receive(ref remoteEndPoint);
                        if (bytes.Length > 0)
                        {
                            ClusterDebug.Log("[Editor Link] received message");
                            EnqueueReceivedData(bytes);
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    ClusterDebug.Log("[Editor Link] Socket closed.");
                }

                ClusterDebug.Log("[Editor Link] Stopped receiving");
            }, token);
        }

        internal void EnqueueReceivedData(byte[] data) => m_ReceivedBytes.Enqueue(data);

        internal void ProcessIncomingMessages()
        {
            while (m_ReceivedBytes.TryDequeue(out var bytes))
            {
                var guid = bytes.LoadStruct<Guid>();
                if (m_EventHandlers.TryGetValue(guid, out var processor))
                {
                    processor.Dispatch(bytes);
                }
            }
        }

        public void Dispose()
        {
            ClusterDebug.Log("[Editor Link] Closing connections");
            m_CancellationTokenSource.Cancel();
            m_UdpClient?.Close();

            m_CancellationTokenSource.Dispose();
            try
            {
                m_ReceiveTask.Wait(2000);
            }
            catch (Exception e)
            {
                ClusterDebug.Log($"[Editor Link] {e.Message}");
            }

            PlayerLoopExtensions.DeregisterUpdate<EditorLinkUpdate>(ProcessIncomingMessages);
        }
    }

    static class TaskExtensions
    {
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            await using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return task.Result;
        }
    }
}
