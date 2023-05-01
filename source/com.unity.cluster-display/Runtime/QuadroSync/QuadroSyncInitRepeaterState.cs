using System;
using System.Diagnostics;
using System.Threading;
using Unity.ClusterDisplay.Utils;
using Utils;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    class QuadroSyncInitRepeaterState: QuadroSyncInitState
    {
        internal QuadroSyncInitRepeaterState(ClusterNode node)
            : base(node) { }

        protected override (NodeState, DoFrameResult?) DoFrameImplementation()
        {
            var ret = base.DoFrameImplementation();

            // Base class did perform the initialization, however QuadroSync Swap Barrier does not enter in action
            // immediately and so we must perform some manual synchronization (using the network) until the Swap Barrier
            // is up and running.

            if (!m_BarrierMonitoringInitialized)
            {
                lock (m_Lock)
                {
                    m_Status = new() {
                        NodeId = Node.Config.NodeId,
                        Stage = QuadroBarrierWarmupStage.RepeaterFastPresent,
                        Completed = false
                    };
                }

                m_HeartbeatChangedEvent = new(false);
                m_StatusChangedEvent = new(false);
                m_BarrierSyncDeadline = StopwatchUtils.TimestampIn(Node.Config.HandshakeTimeout);

                if (ServiceLocator.TryGet(out IClusterSyncState clusterSync) &&
                    clusterSync.NodeRole is NodeRole.Backup && clusterSync.RepeatersDelayedOneFrame)
                {
                    clusterSync.OnNodeRoleChanged += ProcessBackupToEmitter;
                }

                Node.UdpAgent.AddPreProcess(UdpAgentPreProcessPriorityTable.MessageSniffing, SniffReceivedMessages);
                SetBarrierWarmupCallback(BarrierWarmupCallback);
                new Thread(RepeatStatusLoop).Start();
                m_BarrierMonitoringInitialized = true;
            }
            return ret;
        }

        /// <summary>
        /// Method used to sniff received messages and detect which emitter are now using the quadro barrier (instead of
        /// the network sync).
        /// </summary>
        /// <param name="message">Message to sniff</param>
        /// <returns>Always <c>PreProcessResult.PassThrough()</c>.</returns>
        PreProcessResult SniffReceivedMessages(ReceivedMessageBase message)
        {
            try
            {
                switch (message.Type)
                {
                    case MessageType.QuadroBarrierWarmupHeartbeat:
                        SniffReceivedQuadroBarrierWarmupHeartbeat((ReceivedMessage<QuadroBarrierWarmupHeartbeat>)message);
                        break;
                    case MessageType.FrameData:
                        SniffReceivedFrameData((ReceivedMessage<FrameData>)message);
                        break;
                }
            }
            catch (Exception e)
            {
                // Not supposed to happen, but we can let the message continue its way through the pipeline without any
                // arm to anybody, so continue processing as if nothing happened.
                ClusterDebug.LogException(e);
            }
            return PreProcessResult.PassThrough();
        }

        /// <summary>
        /// Sniff received <see cref="QuadroBarrierWarmupHeartbeat"/> messages.
        /// </summary>
        /// <param name="message">The received message to sniff.</param>
        void SniffReceivedQuadroBarrierWarmupHeartbeat(ReceivedMessage<QuadroBarrierWarmupHeartbeat> message)
        {
            lock (m_Lock)
            {
                if (!m_LastHeartbeat.HasValue || !m_LastHeartbeat.Value.Equals(message.Payload))
                {
                    m_LastHeartbeat = message.Payload;
                    m_HeartbeatChangedEvent.Set();
                }

                // The only thing we really have to do for the paused state is to confirm we received the message.
                if (m_LastHeartbeat.Value.Stage == QuadroBarrierWarmupStage.RepeatersPaused)
                {
                    SetStatus(new() {NodeId = Node.Config.NodeId,
                        Stage = QuadroBarrierWarmupStage.RepeatersPaused, Completed = true});
                }
            }
        }

        /// <summary>
        /// Sniff received <see cref="FrameData"/> messages.
        /// </summary>
        /// <param name="message">The received message to sniff.</param>
        void SniffReceivedFrameData(ReceivedMessage<FrameData> message)
        {
            lock (m_Lock)
            {
                // We can say we are done if the last stage was completed since receiving a new FrameData indicate the
                // emitter has received all out messages indicating we are done.  At that moment only we can remove the
                // different hooks put in place during DoFrameImplementation.
                if (m_Status is {Stage: QuadroBarrierWarmupStage.LastRepeatersBurst, Completed: true} &&
                    message.Payload.FrameIndex > 0)
                {
                    m_StatusChangedEvent?.Set();
                    m_StatusChangedEvent = null;
                    SetBarrierWarmupCallback(null);

                    // Remove PreProcess while we are pre-processing a message is the perfect recipe for a dead-lock.
                    // So do it in another thread...  I know starting a thread for just that is wasteful, but we do it
                    // only once at initialization time...
                    var udpAgent = Node.UdpAgent;
                    new Thread(() => udpAgent.RemovePreProcess(SniffReceivedMessages)).Start();
                }
            }
        }

        /// <summary>
        /// Thread function that sends and repeats our status once in a while (repeat because UDP messages could be
        /// lost).
        /// </summary>
        void RepeatStatusLoop()
        {
            while (InitializationState.IsSuccess())
            {
                try
                {
                    QuadroBarrierWarmupStatus statusToSend;
                    IUdpAgent udpAgentToSendWith;
                    AutoResetEvent waitEvent;

                    lock (m_Lock)
                    {
                        waitEvent = m_StatusChangedEvent;
                        if (waitEvent == null)
                        {
                            return;
                        }

                        statusToSend = m_Status;
                        udpAgentToSendWith = Node.UdpAgent;
                    }

                    udpAgentToSendWith.SendMessage(MessageType.QuadroBarrierWarmupStatus, statusToSend);

                    waitEvent.WaitOne(k_RepeatMessageInterval);
                }
                catch (Exception e)
                {
                    // No need to continue if cluster is shutting down, something bad must have happened...
                    if (ServiceLocator.TryGet<IClusterSyncState>(out var clusterSyncState) &&
                        clusterSyncState.IsTerminated)
                    {
                        return;
                    }

                    // Something strange is going on, but let's continue, either it was a random problem or something
                    // more dramatic that should eventually cause InitializationState to be marked as failure (but the
                    // repeating of status is not that critical and we do not want to completely stop the process).
                    ClusterDebug.LogException(e);

                    // But wait a little bit to be sure we are not using all the system resources in case the error
                    // condition persists.
                    Thread.Sleep(25);
                }
            }
        }

        /// <summary>
        /// Callback used when doing the present of the fist frame to ensure that QuadroSync's SwapBarrier is up and
        /// running for all nodes of the cluster.
        /// </summary>
        /// <remarks>Called from the rendering thread.</remarks>
        GfxPluginQuadroSyncSystem.BarrierWarmupAction BarrierWarmupCallback()
        {
            lock (m_Lock)
            {
                if (m_PresentDone != null)
                {
                    m_PresentDone.Set();
                    m_PresentDone = null;
                }
            }

            // Unblock if the cluster display is terminating for whatever reason
            var initializationError = GfxPluginQuadroSyncInitializationState.NotInitialized;
            if (ServiceLocator.TryGet<IClusterSyncState>(out var clusterSyncState) &&
                clusterSyncState.IsTerminated)
            {
                initializationError = GfxPluginQuadroSyncInitializationState.UnexpectedTermination;
            }

            // Be sure we do not try to warmup the barrier for too long
            if (Stopwatch.GetTimestamp() > m_BarrierSyncDeadline)
            {
                initializationError = GfxPluginQuadroSyncInitializationState.BarrierWarmupTimeout;
            }

            if (initializationError != GfxPluginQuadroSyncInitializationState.NotInitialized)
            {
                Node.UdpAgent.RemovePreProcess(SniffReceivedMessages);
                SetBarrierWarmupCallback(null);
                ReportInitializationError(initializationError);
                return GfxPluginQuadroSyncSystem.BarrierWarmupAction.ContinueToNextFrame;
            }

            GfxPluginQuadroSyncSystem.BarrierWarmupAction? warmupAction;
            do
            {
                QuadroBarrierWarmupStage heartbeatStage;
                lock (m_Lock)
                {
                    heartbeatStage = m_LastHeartbeat?.Stage ?? QuadroBarrierWarmupStage.RepeaterFastPresent;
                }

                warmupAction = heartbeatStage switch
                {
                    QuadroBarrierWarmupStage.RepeaterFastPresent => RepeaterFastPresent(),
                    QuadroBarrierWarmupStage.RepeatersPaused => RepeatersPaused(),
                    QuadroBarrierWarmupStage.LastRepeatersBurst => LastRepeatersBurst(),
                    _ => throw new InvalidOperationException($"Unknown {nameof(QuadroBarrierWarmupStage)}")
                };
            } while (!warmupAction.HasValue);

            return warmupAction.Value;
        }

        /// <summary>
        /// <see cref="BarrierWarmupCallback"/> implementation's for
        /// <see cref="QuadroBarrierWarmupStage.RepeaterFastPresent"/>.
        /// </summary>
        GfxPluginQuadroSyncSystem.BarrierWarmupAction? RepeaterFastPresent()
        {
            lock (m_Lock)
            {
                Debug.Assert(m_Status.Stage == QuadroBarrierWarmupStage.RepeaterFastPresent);
                if (!m_Status.Completed)
                {
                    m_PresentDone = new(false);
                    new Thread(() => DetectLongPresent(m_PresentDone)).Start();
                }
                return GfxPluginQuadroSyncSystem.BarrierWarmupAction.RepeatPresent;
            }
        }

        /// <summary>
        /// <see cref="BarrierWarmupCallback"/> implementation's for
        /// <see cref="QuadroBarrierWarmupStage.RepeatersPaused"/>.
        /// </summary>
        GfxPluginQuadroSyncSystem.BarrierWarmupAction? RepeatersPaused()
        {
            // We are paused, we shouldn't present anything, so just wait for things to move to the next stage.
            m_HeartbeatChangedEvent.WaitOne();
            return null;
        }

        /// <summary>
        /// <see cref="BarrierWarmupCallback"/> implementation's for
        /// <see cref="QuadroBarrierWarmupStage.LastRepeatersBurst"/>.
        /// </summary>
        GfxPluginQuadroSyncSystem.BarrierWarmupAction? LastRepeatersBurst()
        {
            lock (m_Lock)
            {
                switch (m_Status.Stage)
                {
                    case QuadroBarrierWarmupStage.RepeatersPaused:
                        // We are entering the LastRepeatersBurst stage.
                        SetStatus(new() {NodeId = Node.Config.NodeId,
                            Stage = QuadroBarrierWarmupStage.LastRepeatersBurst, Completed = false});
                        break;
                    case QuadroBarrierWarmupStage.LastRepeatersBurst:
                        ++m_PerformedAdditionalPresentCount;
                        break;
                    default:
                        Debug.LogError("Something is not working as expected when performing the initial " +
                            "QuadroSync's barrier alignment...");
                        // Let's hope RepeatPresent will maybe allow to fix things up...
                        return GfxPluginQuadroSyncSystem.BarrierWarmupAction.RepeatPresent;
                }

                Debug.Assert(m_LastHeartbeat.HasValue);
                if (m_PerformedAdditionalPresentCount < m_LastHeartbeat.Value.AdditionalPresentCount)
                {
                    return GfxPluginQuadroSyncSystem.BarrierWarmupAction.RepeatPresent;
                }
                else
                {
                    SetStatus(new() {NodeId = Node.Config.NodeId,
                        Stage = QuadroBarrierWarmupStage.LastRepeatersBurst, Completed = true});
                    return GfxPluginQuadroSyncSystem.BarrierWarmupAction.BarrierWarmedUp;
                }
            }
        }

        /// <summary>
        /// Thread function that detects really long present (which indicate the barrier is up).
        /// </summary>
        /// <param name="presentFinished">Event that is set when the present this function call was monitoring is
        /// finished.</param>
        void DetectLongPresent(ManualResetEvent presentFinished)
        {
            // Wait for presenting to be over.
            if (!presentFinished.WaitOne(k_BlockDelay))
            {
                // Looks like present is taking a fair amount of time, barrier must be up and running.  This concludes
                // the RepeaterFastPresent stage (at least, for this repeater)
                lock (m_Lock)
                {
                    Debug.Assert(m_Status.Stage == QuadroBarrierWarmupStage.RepeaterFastPresent);
                    SetStatus(new() {NodeId = Node.Config.NodeId,
                        Stage = QuadroBarrierWarmupStage.RepeaterFastPresent, Completed = true});
                }
            }
        }

        /// <summary>
        /// Sets our current status.
        /// </summary>
        /// <param name="status"></param>
        void SetStatus(QuadroBarrierWarmupStatus status)
        {
            Debug.Assert(Monitor.IsEntered(m_Lock));
            m_Status = status;
            m_StatusChangedEvent.Set();
        }

        /// <summary>
        /// Delegate that will monitor for changes in node role and perform some QuadroSync specific processing when a
        /// node changes from backup to emitter.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        static void ProcessBackupToEmitter()
        {
            if (ServiceLocator.TryGet(out IClusterSyncState clusterSync) &&
                clusterSync.NodeRole is NodeRole.Emitter && clusterSync.RepeatersDelayedOneFrame)
            {
                // Switching from backup to emitter when RepeatersDelayedOneFrame is true means that ClusterRenderer
                // (or in fact the IPresenter inside of it) will now buffer one frame before presenting it.  This means
                // that if we do not do anything special frames would now be presented one frame of.  To fix that
                // problem we need to ask for the next frame to be presented without synchronization so that it is
                // presented "quickly" and we can then go back on using synchronized output normally...
                GfxPluginQuadroSyncSystem.ExecuteQuadroSyncCommand(
                    GfxPluginQuadroSyncSystem.EQuadroSyncRenderEvent.QuadroSyncSkipSyncForNextFrame, new IntPtr());
                clusterSync.OnNodeRoleChanged -= ProcessBackupToEmitter;
            }
            else if (clusterSync is {NodeRole: not (NodeRole.Backup or NodeRole.Unassigned)})
            {
                // The node switched to something else than an emitter.  So we will never need to change ask QuadroSync
                // to skip synchronized present of the next frame, so remove this delegate.
                clusterSync.OnNodeRoleChanged -= ProcessBackupToEmitter;
            }
        }

        /// <summary>
        /// Is everything initialized to monitor quadro barrier state?
        /// </summary>
        bool m_BarrierMonitoringInitialized;
        /// <summary>
        /// Event that is signaled when value of <see cref="m_LastHeartbeat"/> changes.
        /// </summary>
        AutoResetEvent m_HeartbeatChangedEvent;
        /// <summary>
        /// Maximum <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> until which we try to warmup the barrier,
        /// after that we abort and terminate cluster display.
        /// </summary>
        long m_BarrierSyncDeadline = long.MaxValue;

        /// <summary>
        /// Used to synchronize access to member variables below
        /// </summary>
        object m_Lock = new();
        /// <summary>
        /// Event that gets signaled every time <see cref="m_Status"/> changes.
        /// </summary>
        AutoResetEvent m_StatusChangedEvent;
        /// <summary>
        /// Last heartbeat message received from the emitter.
        /// </summary>
        QuadroBarrierWarmupHeartbeat? m_LastHeartbeat;
        /// <summary>
        /// Current status of this repeater.
        /// </summary>
        QuadroBarrierWarmupStatus m_Status;
        /// <summary>
        /// Event that is to be signaled by <see cref="BarrierWarmupCallback"/> to indicate the present is done.
        /// </summary>
        ManualResetEvent m_PresentDone;
        /// <summary>
        /// How many of the <see cref="QuadroBarrierWarmupHeartbeat.AdditionalPresentCount"/> have been performed?
        /// </summary>
        uint m_PerformedAdditionalPresentCount;
    }
}
