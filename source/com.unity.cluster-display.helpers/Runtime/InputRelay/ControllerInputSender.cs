using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using LiteNetLib;
using LiteNetLib.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.ClusterDisplay.Helpers
{
    [DefaultExecutionOrder(int.MinValue)]
    internal unsafe class ControllerInputSender : ControllerInputBase<ControllerInputSender>, INetEventListener, INetLogger
    {
        private NetDataWriter m_Writer = new NetDataWriter();                
        private byte[] m_SendBuffer;

        private SHA1CryptoServiceProvider m_Sha1 = new SHA1CryptoServiceProvider();
        private string m_PreviousBufferHash;

        #if UNITY_EDITOR
        [SerializeField] private int m_TargetFPS = 60;
        #endif

        protected override void OnAwake() {}

        protected override void Connect()
        {
            NetDebug.Logger = this;

            Debug.Log($"Listening for control targets on port: {m_ConnectionSettings.port}");
            m_NetManager = new NetManager(this);
            m_NetManager.Start(m_ConnectionSettings.port);

            CommandLineParser.TryParseTargetFPS(out var specifiedFPS);

            #if UNITY_EDITOR
            specifiedFPS = m_TargetFPS;
            #endif

            Application.targetFrameRate = specifiedFPS;
        }

        private void Update ()
        {
            if (m_NetManager == null)
                return;

            m_NetManager.PollEvents();
            if (m_NetManager.ConnectedPeersCount == 0)
                return;

            EmitScreen();
            EmitInput();
        }

        private void EmitInput ()
        {
            if (!m_InputBuffer.IsCreated || m_InputBuffer.Length != k_InputBufferMaxSize)
                m_InputBuffer = new NativeArray<byte>(k_InputBufferMaxSize, Allocator.Persistent);

            int bytesWritten = ClusterSerialization.SaveInputManagerState(m_InputBuffer);
            if (bytesWritten <= 0)
                return;

            if (m_SendBuffer == null || m_SendBuffer.Length != bytesWritten)
                m_SendBuffer = new byte[bytesWritten];

            void * arrayPtr = UnsafeUtility.PinGCArrayAndGetDataAddress(m_SendBuffer, out var gcHandle);
            UnsafeUtility.MemCpy(arrayPtr, m_InputBuffer.GetUnsafePtr(), bytesWritten);
            UnsafeUtility.ReleaseGCObject(gcHandle);

            var receivedBufferHash = Convert.ToBase64String(m_Sha1.ComputeHash(m_SendBuffer));
            if (m_PreviousBufferHash == receivedBufferHash)
                return;
            m_PreviousBufferHash = receivedBufferHash;

            m_Writer.Reset(0);
            m_Writer.Put((byte)MessageType.InputData);
            m_Writer.Put(m_SendBuffer);

            m_NetManager.SendToAll(m_Writer, DeliveryMethod.ReliableUnordered);
            m_Writer.Reset(0);
        }

        private void EmitScreen ()
        {
            m_Writer.Reset(0);
            m_Writer.Put((byte)MessageType.ScreenDimension);
            m_Writer.Put(ControllerMessageUtils.ValueTypeToBytes(new Vector2(Screen.width, Screen.height)));

            m_NetManager.SendToAll(m_Writer, DeliveryMethod.ReliableUnordered);
            m_Writer.Reset(0);
        }

        protected override void CleanUp()
        {
            if (m_InputBuffer.IsCreated)
                m_InputBuffer.Dispose();

            if (m_SendBuffer != null)
                m_SendBuffer = null;

            if (m_NetManager != null)
                m_NetManager.DisconnectAll();
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod) {}
        public void OnConnectionRequest(ConnectionRequest request) => request.Accept();
        public void OnPeerConnected(NetPeer peer) 
        {
            Debug.Log($"Connected to controllable: {peer.EndPoint.Address}:{peer.EndPoint.Port}");
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) 
        {
            Debug.Log($"Controllable disconnected: {peer.EndPoint.Address}:{peer.EndPoint.Port}");
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) {}

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) {}
        public void WriteNet(NetLogLevel level, string str, params object[] args) {}
        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {}
    }
}
