using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
                m_TcpListener = new(IPAddress.Any, listenPort);
                m_TcpListener.Start();
                m_CancellationTokenSource.Token.Register(() => {
                    var toStop = m_TcpListener;
                    m_TcpListener = null;
                    toStop.Stop();
                });
                while (!m_CancellationTokenSource.IsCancellationRequested)
                {
                    var tcpClient = await m_TcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = ClientLoop(tcpClient);
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
                m_TcpListener?.Stop();
                m_TcpListener = null;
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
        /// Main processing loop of the capsule manager
        /// </summary>
        async ValueTask ClientLoop(TcpClient client)
        {
            using var clientDisposer = client;
            await using var networkStream = client.GetStream();

            int sizeOfGuid = Marshal.SizeOf<Guid>();
            byte[] messageIdBuffer = new byte[sizeOfGuid];

            while (!m_CancellationTokenSource.IsCancellationRequested)
            {
                // Get the message identifier
                bool gotGuid = await networkStream.ReadAllBytesAsync(messageIdBuffer, 0, sizeOfGuid,
                    m_CancellationTokenSource.Token).ConfigureAwait(false);
                if (!gotGuid)
                {
                    continue;
                }
                Guid messageId = MemoryMarshal.Read<Guid>(messageIdBuffer);

                // Handle the message
                if (m_MessageHandlers.TryGetValue(messageId, out var handler))
                {
                    try
                    {
                        await handler.HandleMessage(networkStream).ConfigureAwait(false);
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
                    Debug.LogError($"Received an unknown message from the capsule: {messageId}");
                    // We in theory should send an answer but we have no idea what it is, let's hope the capsule will
                    // be able to deal with it...
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
    }
}
