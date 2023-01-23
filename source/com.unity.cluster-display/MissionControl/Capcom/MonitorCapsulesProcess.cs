using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.ClusterDisplay.MissionControl.Capsule;
using Unity.ClusterDisplay.MissionControl.LaunchPad;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// <see cref="IApplicationProcess"/> responsible for listening for incoming messages from capsules.
    /// </summary>
    public class MonitorCapsulesProcess: IApplicationProcess
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="shutdownToken">Token that gets canceled when the application is shutting down.</param>
        /// <param name="application">Application we are a process of (to which we relay the received messages).</param>
        public MonitorCapsulesProcess(CancellationToken shutdownToken, Application application)
        {
            m_ShutdownToken = shutdownToken;
            m_Application = application;

            m_ShutdownToken.Register(() => m_ScanConnectionsCv.Cancel());

            _ = MonitorLoop();
        }

        /// <inheritdoc/>
        public void Process(MissionControlMirror missionControlMirror)
        {
            lock (m_Lock)
            {
                Dictionary<Guid, LaunchPadInformation> launchPads = new();
                foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
                {
                    if (launchPadInformation.Status is not {State: State.Launched})
                    {
                        continue;
                    }

                    launchPads.Add(launchPadInformation.Definition.Identifier, launchPadInformation);
                }

                if (!launchPads.SequenceEqual(m_LaunchPads))
                {
                    m_LaunchPads = launchPads;
                    m_ScanConnectionsCv.Signal();
                }
            }
        }

        /// <summary>
        /// Number of active connections (mostly for unit testing).
        /// </summary>
        public int ConnectionCount { get; private set; }

        /// <summary>
        /// Method that continuously monitors capsules for incoming messages.
        /// </summary>
        async Task MonitorLoop()
        {
            // List of connections to the different capsules (with some state information about that connection)
            Dictionary<Guid, CapsuleConnection> connections = new();
            // Task to wait on for something to happen
            List<Task> waitOnTasks = new();

            while (!m_ShutdownToken.IsCancellationRequested)
            {
                waitOnTasks.Clear();

                // Update the connections
                Task tryAgainShortly = null;
                lock (m_Lock)
                {
                    waitOnTasks.Add(m_ScanConnectionsCv.SignaledTask);

                    // Get rid of connections to capsules of old launchpads.
                    foreach (var connectionPair in connections.ToList())
                    {
                        if (!m_LaunchPads.ContainsKey(connectionPair.Key))
                        {
                            connectionPair.Value.Dispose();
                            connections.Remove(connectionPair.Key);
                        }
                    }

                    // Ensure we have a connection to the capsule of every active launchpads
                    foreach (var launchPad in m_LaunchPads.Values)
                    {
                        if (!connections.TryGetValue(launchPad.Definition.Identifier, out var connection))
                        {
                            try
                            {
                                connection = new CapsuleConnection(launchPad, m_ShutdownToken);
                                connections.Add(launchPad.Definition.Identifier, connection);
                            }
                            catch (Exception)
                            {
                                // There was problem establishing the connection to that capsule.  Maybe it is not fully
                                // started yet, or the opposite, shutting down.  Simply train again shortly.
                                connection?.Dispose();
                                tryAgainShortly ??= Task.Delay(TimeSpan.FromSeconds(2), m_ShutdownToken);
                                continue;
                            }
                        }

                        if (!connection.ProcessingScheduled)
                        {
                            waitOnTasks.Add(connection.WaitForMessageTask);
                        }
                    }

                    ConnectionCount = connections.Count;
                }

                // Wait for something new to do.
                if (tryAgainShortly != null)
                {
                    waitOnTasks.Add(tryAgainShortly);
                }
                await Task.WhenAny(waitOnTasks).ConfigureAwait(false);

                // Try to process every received messages
                foreach (var connectionPair in connections.ToList())
                {
                    if (connectionPair.Value.WaitForMessageTask.IsFaulted)
                    {
                        connectionPair.Value.Dispose();
                        connections.Remove(connectionPair.Key);
                    }
                    else if (connectionPair.Value.WaitForMessageTask.IsCompletedSuccessfully &&
                             !connectionPair.Value.ProcessingScheduled)
                    {
                        connectionPair.Value.ProcessingScheduled = true;
                        m_Application.QueueMessageFromCapsule(connectionPair.Key,
                            connectionPair.Value.WaitForMessageTask.Result, connectionPair.Value.Stream,
                            () =>
                            {
                                // ReSharper disable once AccessToDisposedClosure
                                connectionPair.Value.ListenForNextMessage();
                                m_ScanConnectionsCv.Signal();
                            });
                    }
                }

                // Restart the loop
            }

            // Close all remaining connections
            foreach (var connection in connections.Values)
            {
                try
                {
                    connection?.Dispose();
                }
                catch (Exception)
                {
                    // Silent failure, we are ramping down anyway and we were just trying to be clean...
                }
            }
            ConnectionCount = 0;
        }

        /// <summary>
        /// Connection to a capsule
        /// </summary>
        class CapsuleConnection: IDisposable
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="information">Information about the LaunchPad that launched the capsule.</param>
            /// <param name="cancellationToken">Cancellation token triggered to stop close the connection (and cancel
            /// any pending network access).</param>
            public CapsuleConnection(LaunchPadInformation information, CancellationToken cancellationToken)
            {
                m_CancellationToken = cancellationToken;

                if (information.Status is not {State: State.Launched})
                {
                    throw new ArgumentException("State of the LaunchPad should be \"Launched\"", nameof(information));
                }

                m_CapsuleTcpClient = new TcpClient(information.Definition.Endpoint.Host, information.CapsulePort);
                m_CapsuleNetworkStream = m_CapsuleTcpClient.GetStream();

                ConnectionInit initStruct = new() {MessageFlow = MessageDirection.CapsuleToCapcom};
                m_CapsuleNetworkStream.WriteStruct(initStruct);

                ListenForNextMessage();
            }

            /// <summary>
            /// <see cref="NetworkStream"/> used to receive messages from the capsule.
            /// </summary>
            public NetworkStream Stream => m_CapsuleNetworkStream;

            /// <summary>
            /// Task that indicate we received a message (and the value of that task it the message identifier).
            /// </summary>
            public Task<Guid> WaitForMessageTask => m_WaitForMessage;

            /// <summary>
            /// Was the processing of the message scheduled?
            /// </summary>
            public bool ProcessingScheduled { get; set; } = true;

            /// <summary>
            /// Assuming all the data of the previous message have been received, initiate reception of the next message.
            /// </summary>
            public void ListenForNextMessage()
            {
                Debug.Assert(ProcessingScheduled);

                m_WaitForMessage =
                    m_CapsuleNetworkStream.ReadStructAsync<Guid>(m_MessageIdBuffer, m_CancellationToken).AsTask()
                        // ReSharper disable once PossibleInvalidOperationException
                        .ContinueWith(t => t.Result.Value, m_CancellationToken);
                ProcessingScheduled = false;
            }

            public void Dispose()
            {
                m_CapsuleTcpClient?.Dispose();
                m_CapsuleNetworkStream?.Dispose();
            }

            /// <summary>
            /// Token that gets canceled when we should close the connection aborting any pending IO.
            /// </summary>
            readonly CancellationToken m_CancellationToken;
            /// <summary>
            /// Tcp connection to the capsule.
            /// </summary>
            readonly TcpClient m_CapsuleTcpClient;
            /// <summary>
            /// Stream from the capsule
            /// </summary>
            readonly NetworkStream m_CapsuleNetworkStream;

            /// <summary>
            /// Temporary buffer used for receiving the message id from the capsule.
            /// </summary>
            byte[] m_MessageIdBuffer = new byte[Marshal.SizeOf<Guid>()];
            /// <summary>
            /// Task waiting for a message from the capsule.
            /// </summary>
            Task<Guid> m_WaitForMessage;
        }

        /// <summary>
        /// Token that gets canceled when the application is shutting down.
        /// </summary>
        readonly CancellationToken m_ShutdownToken;
        /// <summary>
        /// Application we are a process of (to which we relay the received messages).
        /// </summary>
        readonly Application m_Application;

        /// <summary>
        /// Synchronize access to member variables below.
        /// </summary>
        object m_Lock = new();
        /// <summary>
        /// List of LaunchPads that might have a capsule to connect to.
        /// </summary>
        Dictionary<Guid, LaunchPadInformation> m_LaunchPads = new();
        /// <summary>
        /// ConditionVariable that get signaled when we need to make a pass to update the list of connections.
        /// </summary>
        AsyncConditionVariable m_ScanConnectionsCv = new();
    }
}
