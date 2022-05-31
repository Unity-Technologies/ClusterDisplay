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

#if !UNITY_EDITOR
using UnityEngine.PlayerLoop;
using Unity.ClusterDisplay.Scripting;
using Unity.ClusterDisplay.Utils;
#endif

#if CLUSTER_DISPLAY_HAS_MISSION_CONTROL && UNITY_EDITOR
using Unity.ClusterDisplay.MissionControl.Editor;
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Scripting
{
    interface IMessageProcessor
    {
        public void Dispatch(ReadOnlySpan<byte> data);
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

    class EditorLiveLink : IDisposable
    {
        const int k_Port = 40000;
        UdpClient m_UdpClient;
        CancellationTokenSource m_CancellationTokenSource = new();
        Dictionary<Guid, IMessageProcessor> m_EventHandlers = new();

        readonly ConcurrentQueue<byte[]> m_ReceivedBytes = new();
        Task m_ReceiveTask;

        struct EditorLiveLinkUpdate { }

        public bool IsTransmitter { get; }

        public EditorLiveLink(IClusterSyncState clusterSyncState)
        {
#if UNITY_EDITOR
            IsTransmitter = true;
            ClusterDebug.Log("[Editor Live Link] Initializing transmitter (this is an Editor instance)");
            m_UdpClient = new UdpClient();

#else
            if (clusterSyncState is {NodeRole: NodeRole.Emitter})
            {
                ClusterDebug.Log("[Editor Live Link] Initializing sink (this is an Emitter)");
                m_UdpClient = new UdpClient(k_Port);
            }

            PlayerLoopExtensions.RegisterUpdate<PreLateUpdate, EditorLiveLinkUpdate>(ProcessIncomingMessages);
            m_ReceiveTask = ReceiveRawData(m_CancellationTokenSource.Token);
#endif
            if (m_UdpClient != null)
            {
                m_UdpClient.Client.ReceiveTimeout = 200;
                m_UdpClient.Client.SendTimeout = 200;
            }
        }

        public void Publish<T>(ReplicationMessage<T> message) where T : unmanaged
        {
#if CLUSTER_DISPLAY_HAS_MISSION_CONTROL && UNITY_EDITOR
            var endPoint = GetReceiverEndPoint();
            if (endPoint == null) return;

            var dgram = new byte[Marshal.SizeOf(message)];
            var bytes = message.StoreInBuffer(dgram);

            ClusterDebug.Log($"[Editor Live Link] sending message to {endPoint} : {message.Guid}, {message.Contents}");

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

        async Task ReceiveRawData(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                ClusterDebug.Log("[Editor Live Link] Start receiving");
                var result = await m_UdpClient.ReceiveAsync().WithCancellation(token);
                ClusterDebug.Log("[Editor Live Link] received message");
                m_ReceivedBytes.Enqueue(result.Buffer);
            }
            ClusterDebug.Log("[Editor Live Link] Stopped receiving");
        }

#if CLUSTER_DISPLAY_HAS_MISSION_CONTROL && UNITY_EDITOR
        static IPEndPoint GetReceiverEndPoint()
        {
            var settings = MissionControlSettings.instance;
            return !string.IsNullOrEmpty(settings.LiveLinkReceiverAddress)
                ? new IPEndPoint(IPAddress.Parse(settings.LiveLinkReceiverAddress), k_Port)
                : null;
        }
#endif

        void ProcessIncomingMessages()
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
            m_CancellationTokenSource?.Dispose();
            m_UdpClient?.Dispose();
            m_ReceiveTask?.Dispose();
#if !UNITY_EDITOR
            PlayerLoopExtensions.DeregisterUpdate<EditorLiveLinkUpdate>(ProcessIncomingMessages);
#endif
        }
    }

    static class TempExtensions
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
