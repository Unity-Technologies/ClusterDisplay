using System;
using System.Diagnostics;
using System.Threading;
using Unity.ClusterDisplay.Utils;
using Utils;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    class QuadroSyncInitEmitterState: QuadroSyncInitState
    {
        internal QuadroSyncInitEmitterState(ClusterNode node)
            : base(node) { }

        protected override NodeState DoFrameImplementation()
        {
            var ret = base.DoFrameImplementation();

            // Base class did perform the initialization, however QuadroSync Swap Barrier does not enter in action
            // immediately and so we must perform some manual synchronization (using the network) until the Swap Barrier
            // is up and running.

            if (!m_EmittersMonitoringInitialized)
            {
                // Delay syncing between emitter and repeaters to the second frame when repeaters are delayed as we will
                // otherwise try to perform the syncing while the repeaters are still waiting for their first frame data
                // before even trying to render something.
                if (Node.Config.RepeatersDelayed)
                {
                    Debug.Assert(Node.FrameIndex == 0);
                    m_PresentToSkip = 1;
                }

                m_RepeaterHasCompleted = new(false);
                m_HeartbeatChanged = new(false);
                m_BarrierSyncDeadline = StopwatchUtils.TimestampIn(Node.Config.HandshakeTimeout);

                Node.UdpAgent.AddPreProcess(UdpAgentPreProcessPriorityTable.MessageSniffing, SniffReceivedMessages);
                SetBarrierWarmupCallback(BarrierWarmupCallback);
                new Thread(SendHeartbeatLoop).Start();
                m_EmittersMonitoringInitialized = true;
            }
            return ret;
        }

        new EmitterNode Node => base.Node as EmitterNode;

        /// <summary>
        /// Callback used when doing the present of the fist frame to ensure that QuadroSync's SwapBarrier is up and
        /// running for all nodes of the cluster.
        /// </summary>
        /// <remarks>Called from the rendering thread.</remarks>
        GfxPluginQuadroSyncSystem.BarrierWarmupAction BarrierWarmupCallback()
        {
            // Inform DetectLongPresent thread that the present is over
            Thread toJoin;
            lock (m_Lock)
            {
                if (m_PresentDone != null)
                {
                    m_PresentDone.Set();
                    m_PresentDone = null;
                }
                toJoin = m_DetectLongPresent;
                m_DetectLongPresent = null;
            }
            if (toJoin != null)
            {
                toJoin.Join();
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
                if (clusterSyncState != null)
                {
                    clusterSyncState.Terminate();
                }
                return GfxPluginQuadroSyncSystem.BarrierWarmupAction.ContinueToNextFrame;
            }

            // See what we need to do next
            GfxPluginQuadroSyncSystem.BarrierWarmupAction? warmupAction;
            do
            {
                QuadroBarrierWarmupStage heartbeatStage;
                lock (m_Lock)
                {
                    heartbeatStage = m_Heartbeat.Stage;
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
                if (m_PresentToSkip > 0)
                {
                    --m_PresentToSkip;
                    return GfxPluginQuadroSyncSystem.BarrierWarmupAction.ContinueToNextFrame;
                }

                // Is everybody's barrier up?  Repeaters are presenting frames as fast as they can while we are slow.
                // So this means that if they are able to detect their barrier is up it is because the present was long
                // because it is waiting for the emitter (us) to produce a frame.
                if (m_StageCompletedRepeaters.SetBitsCount >= Node.RepeatersStatus.RepeaterPresence.SetBitsCount)
                {
                    // However, it is possible that last repeaters detected it was waiting at about the same moment the
                    // emitter (us) decided to produce another frame (so that we can also warmup our barrier).  So let's
                    // wait a little bit that this emitter has the time to start presenting another frame and be blocked
                    // on that present.
                    Thread.Sleep(k_BlockDelay);

                    // Now let's move on to the next stage, pausing the repeaters while we will advance to find out how
                    // many frames of latency is in the Present call.
                    SetHeartbeat(new() {Stage = QuadroBarrierWarmupStage.RepeatersPaused});
                    return null;
                }
            }

            if (m_RepeaterHasCompleted.WaitOne(k_BlockDelay * 2))
            {
                // One of the repeaters signaled us it completed its stage, let's re-evaluate things.
                return null;
            }

            // Ok, repeaters are producing frames quickly without detecting their barrier is warmed up...  Probably
            // because our own barrier is not warmed up yet, let's present once more and hope it is enough for a good
            // warmup.
            return GfxPluginQuadroSyncSystem.BarrierWarmupAction.RepeatPresent;
        }

        /// <summary>
        /// <see cref="BarrierWarmupCallback"/> implementation's for
        /// <see cref="QuadroBarrierWarmupStage.RepeatersPaused"/>.
        /// </summary>
        GfxPluginQuadroSyncSystem.BarrierWarmupAction? RepeatersPaused()
        {
            // Ok, repeaters will not present anymore frame until we move to the next stage.  We must now find how many
            // frames are in the "present pipeline".  Depending on QuadroSync's PrePresentWait setting,
            // NvAPI_D3D1x_Present might block when doing the present, or on the next frame if previous frame was not
            // yet presented.

            // So to make a long story short, we have either one of those two cases:
            // 1. NvAPI_D3D1x_Present off (default setting but not recommended because it reduce the frame rate):
            //    Emitter:   Nothing is waiting to be presented
            //    Repeaters: One frame waiting to be presented
            // 2. NvAPI_D3D1x_Present on (1 frame more of latency but higher frame rate because less wait):
            //    Emitter:   Nothing is waiting to be presented
            //    Repeaters: Two frames waiting to be presented

            // And there is no simple and reliable API to query this.  So let's find it by ourselves by counting how
            // many present the emitter has to do before blocking (because waiting on the repeaters that are not doing
            // present anymore).

            // Expected result:
            // Case #1: 2
            // Case #2: 4

            lock (m_Lock)
            {
                // Before doing any of that, validate all repeaters know they should not do present anymore.
                bool shouldPresent =
                    m_StageCompletedRepeaters.SetBitsCount >= Node.RepeatersStatus.RepeaterPresence.SetBitsCount;

                if (shouldPresent)
                {
                    // So let's present a frame and check if it blocks
                    Debug.Assert(m_PresentDone == null);
                    m_PresentDone = new(false);
                    Debug.Assert(m_DetectLongPresent == null);
                    m_DetectLongPresent = new Thread(() => DetectLongPresent(m_PresentDone));
                    m_DetectLongPresent.Start();
                    return GfxPluginQuadroSyncSystem.BarrierWarmupAction.RepeatPresent;
                }
            }

            // If we reach this point it is because repeaters haven't yet confirmed they are done presenting frames.  So
            // wait until we receive a confirmation.
            m_RepeaterHasCompleted.WaitOne();
            return null; // Return null to re-evaluate things from the start once a repeater informed us it is paused.
        }

        /// <summary>
        /// <see cref="BarrierWarmupCallback"/> implementation's for
        /// <see cref="QuadroBarrierWarmupStage.LastRepeatersBurst"/>.
        /// </summary>
        GfxPluginQuadroSyncSystem.BarrierWarmupAction? LastRepeatersBurst()
        {
            // There is in fact nothing to be done by the emitter in that phase, just to be sure that every repeater
            // have received that LastRepeatersBurst message.
            bool done;
            lock (m_Lock)
            {
                done = m_StageCompletedRepeaters.SetBitsCount >= Node.RepeatersStatus.RepeaterPresence.SetBitsCount;
                if (done)
                {
                    m_HeartbeatChanged = null;
                }
            }

            if (done)
            {
                Node.UdpAgent.RemovePreProcess(SniffReceivedMessages);
                SetBarrierWarmupCallback(null);
                return GfxPluginQuadroSyncSystem.BarrierWarmupAction.BarrierWarmedUp;
            }
            else
            {
                m_RepeaterHasCompleted.WaitOne();
                return null;
            }
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
                    case MessageType.QuadroBarrierWarmupStatus:
                        SniffReceivedQuadroBarrierWarmupStatus((ReceivedMessage<QuadroBarrierWarmupStatus>)message);
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
        /// Sniff received <see cref="QuadroBarrierWarmupStatus"/> messages.
        /// </summary>
        /// <param name="message">The received message to sniff.</param>
        void SniffReceivedQuadroBarrierWarmupStatus(ReceivedMessage<QuadroBarrierWarmupStatus> message)
        {
            lock (m_Lock)
            {
                if (!message.Payload.Completed)
                {
                    // The only thing we care about are nodes that completed their stage.
                    return;
                }

                var nodeId = message.Payload.NodeId;
                if (!m_StageCompletedRepeaters[nodeId])
                {
                    m_StageCompletedRepeaters[nodeId] = true;
                    m_RepeaterHasCompleted.Set();
                }
            }
        }

        /// <summary>
        /// Thread function that sends (or repeat) the heartbeat to repeaters once in a while (repeat because UDP
        /// messages could be lost).
        /// </summary>
        void SendHeartbeatLoop()
        {
            while (InitializationState.IsSuccess())
            {
                try
                {
                    QuadroBarrierWarmupHeartbeat heartbeatToSend;
                    IUdpAgent udpAgentToSendWith;
                    AutoResetEvent heartbeatChanged;
                    lock (m_Lock)
                    {
                        heartbeatToSend = m_Heartbeat;
                        udpAgentToSendWith = Node.UdpAgent;
                        heartbeatChanged = m_HeartbeatChanged;
                    }

                    udpAgentToSendWith.SendMessage(MessageType.QuadroBarrierWarmupHeartbeat, heartbeatToSend);

                    if (heartbeatChanged == null)
                    {
                        return;
                    }

                    heartbeatChanged.WaitOne(k_RepeatMessageInterval);
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
                    // repeating the heartbeat is not that critical and we do not want to completely stop the process).
                    ClusterDebug.LogException(e);

                    // But wait a little bit to be sure we are not using all the system resources in case the error
                    // condition persists.
                    Thread.Sleep(25);
                }
            }
        }

        /// <summary>
        /// Thread function that detects really long present (which indicates that the emitter is waiting for the
        /// repeaters and so that we found out how long is the present queue).
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
                    // See comments in RepeatersPaused for why / 2.
                    ++m_PresentWhileRepeatersArePaused;
                    uint repeatersAdditionalPresent = m_PresentWhileRepeatersArePaused / 2;
                    SetHeartbeat(new() {Stage = QuadroBarrierWarmupStage.LastRepeatersBurst,
                        AdditionalPresentCount = repeatersAdditionalPresent});
                }
            }
            else
            {
                lock (m_Lock)
                {
                    ++m_PresentWhileRepeatersArePaused;
                }
            }
        }

        /// <summary>
        /// Sets the next heartbeat to send.
        /// </summary>
        /// <param name="heartbeat"></param>
        void SetHeartbeat(QuadroBarrierWarmupHeartbeat heartbeat)
        {
            Debug.Assert(Monitor.IsEntered(m_Lock));
            m_Heartbeat = heartbeat;
            m_HeartbeatChanged.Set();
            m_StageCompletedRepeaters = new();
        }

        /// <summary>
        /// Is everything initialized to monitor emitters?
        /// </summary>
        bool m_EmittersMonitoringInitialized;
        /// <summary>
        /// Indicates that a repeater has completed its stage (that we were waiting on).
        /// </summary>
        AutoResetEvent m_RepeaterHasCompleted;
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
        /// How many present should simply proceed to next frame before syncing?
        /// </summary>
        uint m_PresentToSkip;
        /// <summary>
        /// Heartbeat to send to repeaters.
        /// </summary>
        /// <remarks>The stage of the heartbeat should be considered our current stage.</remarks>
        QuadroBarrierWarmupHeartbeat m_Heartbeat;
        /// <summary>
        /// Something has changed in the heartbeat (and it should be sent to repeaters before the next beat).
        /// </summary>
        /// <remarks>Indicate to <see cref="SendHeartbeatLoop"/> to terminate if null.</remarks>
        AutoResetEvent m_HeartbeatChanged;
        /// <summary>
        /// Repeaters that have completed the current stage
        /// </summary>
        NodeIdBitVector m_StageCompletedRepeaters = new();
        /// <summary>
        /// How many present have been done while repeaters are paused
        /// (<see cref="QuadroBarrierWarmupStage.RepeatersPaused"/>).
        /// </summary>
        uint m_PresentWhileRepeatersArePaused;
        /// <summary>
        /// Event that is to be signaled by <see cref="BarrierWarmupCallback"/> to indicate the present is done.
        /// </summary>
        ManualResetEvent m_PresentDone;
        /// <summary>
        /// Thread started (and waiting on <see cref="m_PresentDone"/>) to detect long to produce frame.
        /// </summary>
        Thread m_DetectLongPresent;
    }
}
