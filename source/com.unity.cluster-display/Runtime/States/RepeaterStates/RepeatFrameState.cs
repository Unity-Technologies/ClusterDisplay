using System;
using System.Diagnostics;
using System.IO;
using Unity.Profiling;
using Utils;

namespace Unity.ClusterDisplay.RepeaterStateMachine
{
    /// <summary>
    /// <see cref="NodeState"/> that wait for frame data from an emitter and repeat it by setting it in the current game
    /// state.
    /// </summary>
    class RepeatFrameState: NodeState<RepeaterNode>, IDisposable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="node">Node we are a state of.</param>
        /// <param name="firstFrameData">Some time the previous state might have received the <see cref="FrameData"/>
        /// for the first frame we have to process.  This is the way of receiving it so that we can start with that
        /// frame and avoid having to ask for retransmission.</param>
        public RepeatFrameState(RepeaterNode node, ReceivedMessage<FrameData> firstFrameData = null)
            : base(node)
        {
            // Remark: Although my past experiences (with Mellanox network adapters) showed me that order of datagrams
            // seem to be preserved between the sender and the receiver, our current setup with an Intel X550 and
            // interrupt moderation activated show that packet order can be slightly broken.  Deactivating interrupt
            // moderation fixed the problem but since packet lost should be something really rare anyway and we prefer
            // to reduce constraint on hardware / driver / system configuration, we decided to simply turn off the
            // feature for now.
            bool orderedReception;
#if CLUSTER_DISPLAY_ORDER_PRESERVING_NETWORK
            orderedReception = true;
#else
            orderedReception = false;
#endif
            m_FrameDataAssembler = new(node.UdpAgent, orderedReception, firstFrameData);
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            m_FrameDataAssembler?.Dispose();
        }

        protected override NodeState DoFrameImplementation()
        {
            // Why do we set the deadline for everything we do at CommunicationTimeout + 1 second?  So that if another
            // repeater is the one causing problem, the emitter will timeout after CommunicationTimeout and continue and
            // this way we will be able to continue correctly (assuming network based synchronization).
            long doFrameDeadline = StopwatchUtils.TimestampIn(Node.Config.CommunicationTimeout + TimeSpan.FromSeconds(1));

            // We do not need to inform the emitter that we are ready to start the next frame on the first frame.
            ReceivedMessage<FrameData> receivedFrameData = null;
            if (m_EmitterExpectingNotificationOnStart)
            {
                using (s_MarkerNetworkSynchronization.Auto())
                {
                    receivedFrameData = PerformNetworkSynchronization(doFrameDeadline);
                }
            }

            using (s_MarkerAssemblingFrameState.Auto())
            {
                // Get the FrameData to use for the frame we are about to start
                m_FrameDataAssembler.WillNeedFrame(Node.FrameIndex);
                var udpAgent = Node.UdpAgent;
                while (receivedFrameData == null && Stopwatch.GetTimestamp() <= doFrameDeadline)
                {
                    var receivedMessage =
                        udpAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(doFrameDeadline));
                    if (receivedMessage == null)
                    {
                        continue;
                    }

                    if (receivedMessage.Type == MessageType.FrameData)
                    {
                        receivedFrameData = (ReceivedMessage<FrameData>)receivedMessage;
                    }
                    else
                    {
                        // Other type of messages, don't really know what they are but let's simply ignore them, they
                        // have no impact on us at the moment...
                        receivedMessage.Dispose();
                    }
                }

                // Final validation / preparation of the received frame data
                if (receivedFrameData == null)
                {
                    // The only this can happen is if we timed out waiting for the FrameData...
                    throw new TimeoutException($"Repeater failed to receive FrameData for frame " +
                        $"{Node.FrameIndex} within the allocated {Node.Config.CommunicationTimeout.TotalSeconds} seconds.");
                }
            }

            using var receivedFrameDataDisposer = receivedFrameData;
            if (receivedFrameData.Payload.FrameIndex != Node.FrameIndex)
            {
                throw new InvalidDataException($"Unexpected FrameData FrameIndex, was expecting " +
                    $"{Node.FrameIndex} but we got {receivedFrameData.Payload.FrameIndex}.");
            }

            using (s_MarkerApplyingState.Auto())
            {
                // Push it to the game state
                RepeaterStateReader.RestoreEmitterFrameData(receivedFrameData.ExtraData.AsNativeArray());
            }

            // RepeatFrameState never switch to another state...
            return null;
        }

        protected override IntPtr GetProfilerMarker() => s_ProfilerMarker;

        /// <summary>
        /// Perform network based synchronization
        /// </summary>
        /// <param name="deadlineTick">Limit of when to wait for successful network synchronization.</param>
        /// <returns><see cref="ReceivedMessage{TM}"/> that made us realize that we lost a
        /// <see cref="EmitterWaitingToStartFrame"/> and that needs to be processed.</returns>
        ReceivedMessage<FrameData> PerformNetworkSynchronization(long deadlineTick)
        {
            var udpAgent = Node.UdpAgent;
            var nodeId = Node.Config.NodeId;

            var waitToStartMessage = new RepeaterWaitingToStartFrame()
            {
                FrameIndex = Node.FrameIndex,
                NodeId = Node.Config.NodeId,
                WillUseNetworkSyncOnNextFrame = !Node.HasExternalSync
            };
            m_EmitterExpectingNotificationOnStart = waitToStartMessage.WillUseNetworkSyncOnNextFrame;

            bool firstRepeaterWaitingToStartFrame = true;
            do
            {
                long stopWaitingForEmitterWaitingDeadline =
                    StopwatchUtils.TimestampIn(s_NotAcknowledgedRepeatWaitToStartInterval);

                // Inform the emitter we are ready to go
                udpAgent.SendMessage(MessageType.RepeaterWaitingToStartFrame, waitToStartMessage);
                if (firstRepeaterWaitingToStartFrame)
                {
                    firstRepeaterWaitingToStartFrame = false;
                }
                else
                {
                    udpAgent.Stats.SentMessageWasRepeat(MessageType.RepeaterWaitingToStartFrame);
                }

                do
                {
                    // Get the next message
                    var receivedMessage =
                        udpAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(stopWaitingForEmitterWaitingDeadline));
                    if (receivedMessage == null)
                    {
                        continue;
                    }

                    switch (receivedMessage.Type)
                    {
                    case MessageType.FrameData:
                        var receivedFrameData = (ReceivedMessage<FrameData>)receivedMessage;
                        if (receivedFrameData.Payload.FrameIndex != Node.FrameIndex)
                        {
                            throw new InvalidDataException($"Unexpected FrameData FrameIndex, was expecting " +
                                $"{Node.FrameIndex} but we got {receivedFrameData.Payload.FrameIndex}.");
                        }
                        // Looks like the emitter sent us a EmitterWaitingToStartFrame but it looks like we missed it.
                        // No big deal, let's move on in trying to process the FrameData.
                        return receivedFrameData;
                    case MessageType.EmitterWaitingToStartFrame:
                        var receivedEmitterWaitingToStartFrame = (ReceivedMessage<EmitterWaitingToStartFrame>)receivedMessage;
                        if (receivedEmitterWaitingToStartFrame.Payload.FrameIndex == Node.FrameIndex)
                        {
                            var payload = receivedEmitterWaitingToStartFrame.Payload;
                            bool emitterKnowsWeAreReady = !payload.IsWaitingOn(nodeId);
                            receivedMessage.Dispose();
                            if (emitterKnowsWeAreReady)
                            {
                                // We know the emitter knows about us, we can move on waiting for the FrameData
                                return null;
                            }
                        }
                        else if (receivedEmitterWaitingToStartFrame.Payload.FrameIndex > Node.FrameIndex)
                        {
                            throw new InvalidDataException($"Unexpected EmitterWaitingToStartFrame FrameIndex, " +
                                $"was expecting {Node.FrameIndex} but we got " +
                                $"{receivedEmitterWaitingToStartFrame.Payload.FrameIndex}.");
                        }
                        // ... and let's simply ignore old FrameIndex.  A packet that was lost on the network?
                        break;
                    default:
                        // Some other message, just discard it.
                        receivedMessage.Dispose();
                        break;
                    }
                } while (Stopwatch.GetTimestamp() < stopWaitingForEmitterWaitingDeadline);

            } while (Stopwatch.GetTimestamp() <= deadlineTick);

            // If we reach this point it is because we haven't got any feedback from the emitter in time -> timeout
            throw new TimeoutException("Repeater failed to perform network synchronization with emitter for " +
                $"frame {Node.FrameIndex} within the allocated {Node.Config.CommunicationTimeout.TotalSeconds} seconds.");
        }

        /// <summary>
        /// Object responsible for assembling all the different parts of a FrameData and for requesting retransmission
        /// of lost messages.
        /// </summary>
        FrameDataAssembler m_FrameDataAssembler;
        /// <summary>
        /// Is the emitter expecting a <see cref="RepeaterWaitingToStartFrame"/> to be sent when starting the frame?
        /// </summary>
        /// <remarks>Emitter is always expecting a <see cref="RepeaterWaitingToStartFrame"/> when starting frame 1.
        /// </remarks>
        bool m_EmitterExpectingNotificationOnStart = true;

        /// <summary>
        /// How much time between every repetition of RepeaterWaitingToStartFrame.
        /// </summary>
        /// <remarks>Need to be low as in theory every node will waiting for us if the message we sent to the emitter
        /// was lost.</remarks>
        static TimeSpan s_NotAcknowledgedRepeatWaitToStartInterval = TimeSpan.FromMilliseconds(1);
        /// <summary>
        /// Value returned by <see cref="GetProfilerMarker"/>.
        /// </summary>
        static IntPtr s_ProfilerMarker = CreateProfilingMarker(nameof(RepeatFrameState));
        /// <summary>
        /// <see cref="ProfilerMarker"/> used to identify the time spent waiting in the network based synchronization.
        /// </summary>
        static ProfilerMarker s_MarkerNetworkSynchronization = new ProfilerMarker("NetworkSynchronization");
        /// <summary>
        /// <see cref="ProfilerMarker"/> used to identify the time spent waiting the frame state from the network.
        /// </summary>
        static ProfilerMarker s_MarkerAssemblingFrameState = new ProfilerMarker("AssemblingFrameState");
        /// <summary>
        /// <see cref="ProfilerMarker"/> used to identify the time spent applying the frame state.
        /// </summary>
        static ProfilerMarker s_MarkerApplyingState = new ProfilerMarker("ApplyingState");
    }
}
