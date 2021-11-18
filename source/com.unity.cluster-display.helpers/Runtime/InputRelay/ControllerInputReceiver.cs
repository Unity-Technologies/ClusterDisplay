using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.LowLevel;
using Unity.ClusterDisplay.Graphics;

namespace Unity.ClusterDisplay.Helpers
{
    internal class ControllerInputReceiver : ControllerInputBase<ControllerInputReceiver>, INetEventListener, INetLogger
    {
        internal interface IReceiver
        {
            void OnMessageReceived(MessageType messageType, byte[] messageData, int payloadOffset, int payloadSize);
        }

        private readonly Queue<byte[]> m_QueuedInputBuffers = new Queue<byte[]>();

        public delegate void OnMessageReceived(MessageType messageType, byte[] messageData, int payloadOffset, int payloadSize);
        public OnMessageReceived onMessageReceived;

        protected override void OnAwake() {}

        private void InsertControlLoop ()
        {
            var newLoop = PlayerLoop.GetCurrentPlayerLoop();

            var initList = newLoop.subSystemList[0].subSystemList.ToList();
            if (initList.Any((x) => x.type == this.GetType()))
                return; // We don't need to assert or insert anything if our loop already exists.

            var indexOfPlayerUpdateTime = initList.FindIndex((playerLoop) =>
                
            #if UNITY_2020_2_OR_NEWER
                playerLoop.type == typeof(UnityEngine.PlayerLoop.TimeUpdate.WaitForLastPresentationAndUpdateTime));
            #else
                playerLoop.type == typeof(UnityEngine.PlayerLoop.Initialization.PlayerUpdateTime));
            #endif
            
            initList.Insert(indexOfPlayerUpdateTime + 1, new PlayerLoopSystem()
            {
                type = this.GetType(),
                updateDelegate = ControlUpdate
            });

            newLoop.subSystemList[0].subSystemList = initList.ToArray();
            PlayerLoop.SetPlayerLoop(newLoop);
        }

        protected override void Connect()
        {
            NetDebug.Logger = this;

            Debug.Log($"Attempting to connect to controller at address: {m_ConnectionSettings.emitterAddress}:{m_ConnectionSettings.port}");
            m_NetManager = new NetManager(this);

            m_NetManager.Start();
            m_NetManager.Connect(m_ConnectionSettings.emitterAddress, m_ConnectionSettings.port, new NetDataWriter());

            InsertControlLoop();
        }

        private unsafe void ControlUpdate()
        {
            m_NetManager.PollEvents();
            if (m_NetManager.ConnectedPeersCount == 0)
                return;

            if (m_QueuedInputBuffers.Count > 0)
            {
                var queuedBuffer = m_QueuedInputBuffers.Dequeue();

                if (!m_InputBuffer.IsCreated || m_InputBuffer.Length != k_InputBufferMaxSize)
                    m_InputBuffer = new NativeArray<byte>(k_InputBufferMaxSize, Allocator.Persistent);

                fixed (void* ptr = queuedBuffer)
                    UnsafeUtility.MemCpy(m_InputBuffer.GetUnsafePtr(), ptr, queuedBuffer.Length);
            }

            if (!m_InputBuffer.IsCreated)
                return;

            ClusterSerialization.RestoreInputManagerState(m_InputBuffer);
        }

        protected virtual void OnSetScreenDimdension(Vector2 screenDimension) {}
        public unsafe void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            var messageType = (MessageType)reader.RawData[reader.UserDataOffset];

            switch (messageType)
            {
                case MessageType.InputData:
                {
                    var buffer = new byte[reader.UserDataSize - 1];
                    Array.Copy(reader.RawData, reader.UserDataOffset + 1, buffer, 0, reader.UserDataSize - 1);
                    m_QueuedInputBuffers.Enqueue(buffer);
                } break;

                default:
                    onMessageReceived?.Invoke(messageType, reader.RawData, reader.UserDataOffset + 1, reader.UserDataSize - 1);
                    break;
            }

            reader.Recycle();
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            if (m_NetManager.ConnectedPeersCount > 0)
                return;

            m_NetManager.Connect(remoteEndPoint, new NetDataWriter());
        }

        protected override void CleanUp ()
        {
            if (m_InputBuffer.IsCreated)
                m_InputBuffer.Dispose();

            m_QueuedInputBuffers.Clear();

            if (m_NetManager != null)
                m_NetManager.DisconnectAll();
        }

        public unsafe void OnPeerConnected(NetPeer peer) 
        {
            Debug.Log($"Connected to controller: {m_ConnectionSettings.emitterAddress}:{m_ConnectionSettings.port}");
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer))
                return;
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) 
        {
            Debug.Log($"Disconnected from controller: {m_ConnectionSettings.emitterAddress}:{m_ConnectionSettings.port}");
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) {}
        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) {}
        public void OnConnectionRequest(ConnectionRequest request) {}
        public void WriteNet(NetLogLevel level, string str, params object[] args) {}
    }
}
