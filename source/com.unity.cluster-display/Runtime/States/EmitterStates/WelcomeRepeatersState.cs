using System;
using System.Diagnostics;
using Utils;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay.EmitterStateMachine
{
    /// <summary>
    /// <see cref="NodeState"/> that welcomes <see cref="RepeaterNode"/> withing the cluster.
    /// </summary>
    class WelcomeRepeatersState: NodeState<EmitterNode>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="node">Node we are a state of.</param>
        public WelcomeRepeatersState(EmitterNode node)
            : base(node)
        {
        }

        protected override (NodeState, DoFrameResult?) DoFrameImplementation()
        {
            Debug.Assert(Node.FrameIndex == 0, "WelcomeRepeatersState.DoFrame must always be called for the first " +
                "frame as all nodes in the cluster need to execute all the frames the same way.");

            long registerDeadline = StopwatchUtils.TimestampIn(Node.Config.HandshakeTimeout);
            var udpAgent = Node.UdpAgent;
            var repeatersStatus = Node.RepeatersStatus;

            do
            {
                // Wait for a RegisteringWithEmitter message
                using var receivedMessage =
                    udpAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(registerDeadline));
                if (receivedMessage == null || receivedMessage.Type != MessageType.RegisteringWithEmitter)
                {
                    // Strange that we are receiving anything else, but let's simply skip it...
                    continue;
                }

                // Update our status and set a response
                var receivedRegisteringWithEmitter = (ReceivedMessage<RegisteringWithEmitter>)receivedMessage;
                bool wasRegistered = Node.RepeatersStatus.RepeaterPresence[receivedRegisteringWithEmitter.Payload.NodeId];
                var registeringAnswer =
                    repeatersStatus.ProcessRegisteringMessage(receivedRegisteringWithEmitter.Payload);
                udpAgent.SendMessage(MessageType.RepeaterRegistered, registeringAnswer);
                if (wasRegistered)
                {
                    Node.UdpAgent.Stats.SentMessageWasRepeat(MessageType.RepeaterRegistered);
                }

                // Continue consuming messages until we know about every emitter or it took too much time...
            } while (Stopwatch.GetTimestamp() <= registerDeadline &&
                     repeatersStatus.RepeaterPresence.SetBitsCount < Node.EmitterConfig.ExpectedRepeaterCount);

            // We are done accepting repeaters, let's start sending frames!
            return (new EmitFrameState(Node, Node.RepeatersStatus.RepeaterPresence), null);
        }

        protected override IntPtr GetProfilerMarker() => s_ProfilerMarker;

        /// <summary>
        /// Value returned by <see cref="GetProfilerMarker"/>.
        /// </summary>
        static IntPtr s_ProfilerMarker = CreateProfilingMarker(nameof(WelcomeRepeatersState));
    }
}
