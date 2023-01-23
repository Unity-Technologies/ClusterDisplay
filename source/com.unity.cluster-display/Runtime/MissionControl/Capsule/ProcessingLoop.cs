using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    /// <summary>
    /// Class responsible for waiting for requests from the MissionControl' capcom.
    /// </summary>
    public class ProcessingLoop
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="addDefaultHandlers">Does the constructor add the default message handlers?</param>
        public ProcessingLoop(bool addDefaultHandlers = true)
        {
            if (addDefaultHandlers)
            {
                m_MessageHandlers[MessagesId.Land] = new LandMessageHandler(m_CancellationTokenSource);
            }
        }

        /// <summary>
        /// Adds a new message handler to the processing loop (that is not owned by it).
        /// </summary>
        /// <param name="messageId">The id of the new message.</param>
        /// <param name="newHandler">The handler for the new message.</param>
        public void AddMessageHandler(Guid messageId, IMessageHandler newHandler)
        {
            m_MessageHandlers[messageId] = newHandler;
        }

        /// <summary>
        /// Main loop accepting connections
        /// </summary>
        /// <param name="listenPort">Port to listen on for new connections from capcom.</param>
        public async Task Start(int listenPort)
        {
            if (m_TcpListener != null)
            {
                throw new InvalidOperationException("Capsule.ProcessingLoop already running...");
            }

            try
            {
                Debug.Log("ProcessingLoop Start creating TcpListener");
                m_TcpListener = new(IPAddress.Any, listenPort);
                m_TcpListener.Start();
                m_CancellationTokenSource.Token.Register(() =>
                {
                    var toStop = m_TcpListener;
                    m_TcpListener = null;
                    toStop.Stop();
                });

                Debug.Log("ProcessingLoop Start Before hooking OnDoPreFrame");
                ClusterSyncLooper.onInstanceDoPreFrame += OnDoPreFrame;
                _ = SendsCapcomMessages();

                while (!m_CancellationTokenSource.IsCancellationRequested)
                {
                    Debug.Log("ProcessingLoop Start Before AcceptTcpClientAsync");
                    var tcpClient = await m_TcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                    Debug.Log("ProcessingLoop Start After AcceptTcpClientAsync");
                    _ = ProcessConnectionAsync(tcpClient);
                }
            }
            catch (Exception e)
            {
                if (!m_CancellationTokenSource.IsCancellationRequested)
                {
                    Debug.LogError($"MissionControl's capsule failed listening for incoming connections on port " +
                        $"{listenPort}: {e}");
                }
            }
            finally
            {
                ClusterSyncLooper.onInstanceDoPreFrame -= OnDoPreFrame;
                m_TcpListener?.Stop();
                m_TcpListener = null;
            }
        }

        /// <summary>
        /// Queue a message to be sent to capcom.
        /// </summary>
        /// <param name="message">Object responsible for sending the message</param>
        public void QueueSendMessage(IToCapcomMessage message)
        {
            lock (m_SendMessageQueueLock)
            {
                m_SendMessageQueue.Enqueue(message);
                m_SendMessageQueueCv.Signal();
            }
        }

        /// <summary>
        /// Stop currently running processing loop.
        /// </summary>
        public void Stop()
        {
            m_CancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Function looping and send messages to capcom when messages are to be sent.
        /// </summary>
        async ValueTask SendsCapcomMessages()
        {
            byte[] messageIdBuffer = new byte[Marshal.SizeOf<Guid>()];

            // ReSharper disable once InconsistentlySynchronizedField
            m_CancellationTokenSource.Token.Register(() => m_SendMessageQueueCv.Cancel());

            try
            {
                while (!m_CancellationTokenSource.IsCancellationRequested)
                {
                    IToCapcomMessage toSend;
                    ValueTask? toWaitOn = null;
                    lock (m_SendMessageQueueLock)
                    {
                        if (!m_SendMessageQueue.TryDequeue(out toSend))
                        {
                            toWaitOn = m_SendMessageQueueCv.SignaledValueTask;
                        }
                    }

                    if (toWaitOn.HasValue)
                    {
                        await toWaitOn.Value.ConfigureAwait(false);
                        continue;
                    }

                    NetworkStream networkStream;
                    lock (m_Lock)
                    {
                        networkStream = m_ToCapcomNetworkStream;
                    }

                    if (networkStream == null)
                    {
                        // No one care about the message, just skip it.
                        toSend.Dispose();
                        continue;
                    }

                    try
                    {
                        await networkStream.WriteStructAsync(toSend.MessageId, messageIdBuffer).ConfigureAwait(false);
                        await toSend.Send(networkStream).ConfigureAwait(false);
                        toSend.Dispose();
                    }
                    catch (Exception e)
                    {
                        // We log it as an error as normally capcom should always outlive the capsule, so it means that
                        // there is really something going wrong...
                        Debug.LogError($"Communication error with capcom: {e}");
                    }
                }
            }
            finally
            {
                lock (m_Lock)
                {
                    m_ToCapcomNetworkStream?.Dispose();
                    m_ToCapcomNetworkStream = null;
                    m_ToCapcomTcpClient?.Dispose();
                    m_ToCapcomTcpClient = null;
                }
            }

        }

        /// <summary>
        /// Establish a connection with a client.
        /// </summary>
        /// <param name="client">The client.</param>
        async ValueTask ProcessConnectionAsync(TcpClient client)
        {
            NetworkStream networkStream = null;
            ConnectionInit connectionInit;
            try
            {
                networkStream = client.GetStream();
                connectionInit = networkStream.ReadStruct<ConnectionInit>();
            }
            catch
            {
                // ReSharper disable once MethodHasAsyncOverload
                networkStream?.Dispose();
                client.Dispose();
                throw;
            }

            switch (connectionInit.MessageFlow)
            {
                default:
                case MessageDirection.CapcomToCapsule:
                    Debug.Log("ProcessingLoop MessageDirection.CapcomToCapsule In");
                    await ProcessMessages(client, networkStream);
                    Debug.Log("ProcessingLoop MessageDirection.CapcomToCapsule Out");
                    break;
                case MessageDirection.CapsuleToCapcom:
                    Debug.Log("ProcessingLoop MessageDirection.CapcomToCapsule In");
                    SaveMessageReceiver(client, networkStream);
                    Debug.Log("ProcessingLoop MessageDirection.CapcomToCapsule Out");
                    break;
            }
        }

        /// <summary>
        /// Loop that process messages received from capcom.
        /// </summary>
        /// <param name="client"><see cref="TcpClient"/> feeding <paramref name="networkStream"/>.</param>
        /// <param name="networkStream">Stream over which we are receiving messages and sending responses.</param>
        async ValueTask ProcessMessages(TcpClient client, NetworkStream networkStream)
        {
            using var clientDisposer = client;
            await using var networkStreamDisposer = networkStream;

            int sizeOfGuid = Marshal.SizeOf<Guid>();
            byte[] messageIdBuffer = new byte[sizeOfGuid];

            while (!m_CancellationTokenSource.IsCancellationRequested)
            {
                // Get the message identifier
                Debug.Log("ProcessingLoop ProcessMessages Before Get Identifier");
                bool gotGuid = await networkStream.ReadAllBytesAsync(messageIdBuffer, 0, sizeOfGuid,
                    m_CancellationTokenSource.Token).ConfigureAwait(false);
                if (!gotGuid)
                {
                    continue;
                }

                Guid messageId = MemoryMarshal.Read<Guid>(messageIdBuffer);
                Debug.Log($"ProcessingLoop ProcessMessages After Get Identifier: {messageId}");

                // Handle the message
                if (m_MessageHandlers.TryGetValue(messageId, out var handler))
                {
                    try
                    {
                        Debug.Log($"ProcessingLoop HandleMessage Before");
                        await handler.HandleMessage(networkStream).ConfigureAwait(false);
                        Debug.Log($"ProcessingLoop HandleMessage After");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Message {messageId} generated an exception: {e}");

                        // We in theory should send an answer but we have no idea what it is, let's hope the capsule will
                        // be able to deal with it...
                    }
                }
                else
                {
                    Debug.LogError($"Received an unknown message from capcom: {messageId}");

                    // We in theory should send an answer but we have no idea what it is, let's hope the capsule will
                    // be able to deal with it...
                }
            }
        }

        /// <summary>
        /// Save information necessary to send messages we are sending to this <see cref="TcpClient"/>.
        /// </summary>
        /// <param name="client"><see cref="TcpClient"/> feeding <paramref name="networkStream"/>.</param>
        /// <param name="networkStream">Stream over which we are sending messages and receiving responses.</param>
        void SaveMessageReceiver(TcpClient client, NetworkStream networkStream)
        {
            lock (m_Lock)
            {
                if (m_ToCapcomTcpClient != null)
                {
                    Debug.LogWarning("Refused message receiver as we already have one.");
                    networkStream.Dispose();
                    client.Dispose();
                    return;
                }

                m_ToCapcomTcpClient = client;
                m_ToCapcomNetworkStream = networkStream;

                // Immediately send a status update so that capcom does not have to wait for the next status change
                // (which could take some time).
                var messageToSend = SendCapsuleStatus.New();
                messageToSend.NodeRole = m_LastKnownRole;
                messageToSend.NodeId = (byte)m_NodeId;
                messageToSend.RenderNodeId = (byte)m_LastRenderNodeId;
                QueueSendMessage(messageToSend);
            }
        }

        /// <summary>
        /// Callback executed before the start of each frame.
        /// </summary>
        void OnDoPreFrame()
        {
            if (ServiceLocator.TryGet<IClusterSyncState>(out var clusterSyncState))
            {
                if (clusterSyncState.NodeRole != m_LastKnownRole || clusterSyncState.RenderNodeID != m_LastRenderNodeId)
                {
                    var messageToSend = SendCapsuleStatus.New();
                    messageToSend.NodeRole = clusterSyncState.NodeRole;
                    messageToSend.NodeId = clusterSyncState.NodeID;
                    messageToSend.RenderNodeId = clusterSyncState.RenderNodeID;

                    m_LastKnownRole = messageToSend.NodeRole;
                    m_NodeId = messageToSend.NodeId;
                    m_LastRenderNodeId = messageToSend.RenderNodeId;

                    QueueSendMessage(messageToSend);
                }
            }
        }

        /// <summary>
        /// Signaled when the application when the capsule has to land.
        /// </summary>
        CancellationTokenSource m_CancellationTokenSource = new();
        /// <summary>
        /// Object responsible to accepting incoming connections
        /// </summary>
        TcpListener m_TcpListener;
        /// <summary>
        /// The different message handlers
        /// </summary>
        Dictionary<Guid, IMessageHandler> m_MessageHandlers = new();

        /// <summary>
        /// Last known <see cref="IClusterSyncState.NodeRole"/>.
        /// </summary>
        NodeRole m_LastKnownRole = NodeRole.Unassigned;
        /// <summary>
        /// Last known <see cref="IClusterSyncState.NodeID"/>.
        /// </summary>
        /// <remarks>Constant through execution.</remarks>
        int m_NodeId = -1;
        /// <summary>
        /// Last known <see cref="IClusterSyncState.RenderNodeID"/>.
        /// </summary>
        int m_LastRenderNodeId = -1;

        /// <summary>
        /// Synchronize access to m_SendMessageQueue* member variables.
        /// </summary>
        object m_SendMessageQueueLock = new();
        /// <summary>
        /// Queue storing <see cref="Action"/> that will send messages to capcoms (there should be one, but nothing
        /// stops us from having multiple) and process the answers.
        /// </summary>
        /// <remarks>Would have loved to use Channel but this is .Net 3.0 only...</remarks>
        Queue<IToCapcomMessage> m_SendMessageQueue = new(100);
        /// <summary>
        /// Signaled every time something is added to <see cref="m_SendMessageQueue"/>.
        /// </summary>
        AsyncConditionVariableValueTask m_SendMessageQueueCv = new();

        /// <summary>
        /// Object used to synchronize access to the member variables below
        /// </summary>
        object m_Lock = new();
        /// <summary>
        /// Tcp client to send messages to capcom.
        /// </summary>
        TcpClient m_ToCapcomTcpClient;
        /// <summary>
        /// Network stream to send messages to capcom.
        /// </summary>
        NetworkStream m_ToCapcomNetworkStream;
    }
}
