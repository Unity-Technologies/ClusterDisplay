using System;
using System.Diagnostics;
using System.IO;
using Utils;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay.RepeaterStateMachine
{
    /// <summary>
    /// <see cref="NodeState"/> that register a <see cref="RepeaterNode"/> with the cluster's emitter.
    /// </summary>
    class RegisterWithEmitterState: NodeState<RepeaterNode>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="node">Node we are a state of.</param>
        public RegisterWithEmitterState(RepeaterNode node)
            : base(node) { }

        protected override (NodeState, DoFrameResult?) DoFrameImplementation()
        {
            Debug.Assert(Node.FrameIndex == 0, "RegisterWithEmitterState.DoFrame must always be called for the " +
                "first frame as all nodes in the cluster need to execute all the frames the same way.");

            long registerDeadline = StopwatchUtils.TimestampIn(Node.Config.HandshakeTimeout);
            var udpAgent = Node.UdpAgent;

            var registerMessage = new RegisteringWithEmitter()
            {
                NodeId = Node.Config.NodeId,
                IPAddressBytes = BitConverter.ToUInt32(udpAgent.AdapterAddress.GetAddressBytes(), 0)
            };

            var retransmitRegistrationInterval = s_RetransmitRegistrationInterval;
            ReceivedMessage<FrameData> repeatFrameStateFirstFrameData = null;

            bool hasReceivedRepeaterRegistered = false;
            bool firstRegisteringWithEmitter = true;
            do
            {
                // Broadcast to a listening emitter that we are here!
                udpAgent.SendMessage(MessageType.RegisteringWithEmitter, registerMessage);
                if (firstRegisteringWithEmitter)
                {
                    firstRegisteringWithEmitter = false;
                }
                else
                {
                    udpAgent.Stats.SentMessageWasRepeat(MessageType.RegisteringWithEmitter);
                }

                // Wait for it's answer (for up to retransmitRegistrationInterval)
                long stopWaitingForRepeaterRegistered = StopwatchUtils.TimestampIn(retransmitRegistrationInterval);
                stopWaitingForRepeaterRegistered = Math.Min(stopWaitingForRepeaterRegistered, registerDeadline);
                do
                {
                    var maxConsumeTime = StopwatchUtils.TimeUntil(stopWaitingForRepeaterRegistered);
                    using var receivedMessage = udpAgent.TryConsumeNextReceivedMessage(maxConsumeTime);
                    if (receivedMessage != null)
                    {
                        switch (receivedMessage.Type)
                        {
                            case MessageType.RepeaterRegistered:
                                var receivedRepeaterRegistered = (ReceivedMessage<RepeaterRegistered>)receivedMessage;
                                var payload = receivedRepeaterRegistered.Payload;
                                if (payload.NodeId == registerMessage.NodeId &&
                                    payload.IPAddressBytes == registerMessage.IPAddressBytes)
                                {
                                    if (!receivedRepeaterRegistered.Payload.Accepted)
                                    {
                                        throw new InvalidOperationException("Emitter rejected the repeater.");
                                    }
                                    hasReceivedRepeaterRegistered = true;
                                }
                                break;
                            case MessageType.FrameData:
                                // Two ways this can happen, the RepeaterRegistered response was lost or we were late
                                // and someone else took our place.  In any case, we cannot continue until the emitter
                                // did acknowledge us.  And if the response was lost then we have to start ASAP to
                                // process frames, so immediately retransmit a RegisteringWithEmitter message.
                                var receivedFrameData = (ReceivedMessage<FrameData>)receivedMessage;
                                if (receivedFrameData.Payload.FrameIndex != 0)
                                {
                                    throw new InvalidDataException("Received FrameData for frame " +
                                        $"{receivedFrameData.Payload.FrameIndex} while we were expecting data for frame 0.");
                                }
                                repeatFrameStateFirstFrameData = receivedFrameData.TransferToNewInstance();
                                retransmitRegistrationInterval = s_ShortRetransmitRegistrationInterval;
                                break;
                            case MessageType.PropagateQuit:
                                return (new ProcessQuitMessageState(Node), null);
                        }
                    }
                } while (!hasReceivedRepeaterRegistered && stopWaitingForRepeaterRegistered > Stopwatch.GetTimestamp());

            } while (!hasReceivedRepeaterRegistered && registerDeadline > Stopwatch.GetTimestamp());

            using var repeatFrameStateFirstFrameDataDisposer = repeatFrameStateFirstFrameData;
            if (!hasReceivedRepeaterRegistered)
            {
                throw new TimeoutException("Repeater failed to register with emitter in less than " +
                    $"{Node.Config.HandshakeTimeout.TotalSeconds} seconds");
            }

            return (new RepeatFrameState(Node, repeatFrameStateFirstFrameData), null);
        }

        protected override IntPtr GetProfilerMarker() => s_ProfilerMarker;

        /// <summary>
        /// We want to re-broadcast our presence every 250 milliseconds (in case the emitter started a little bit after
        /// us).
        /// </summary>
        static TimeSpan s_RetransmitRegistrationInterval = TimeSpan.FromMilliseconds(250);
        /// <summary>
        /// This is the time interval between retransmission of RegisteringWithEmitter when we detect something is bad
        /// and we think we are late.
        /// </summary>
        static TimeSpan s_ShortRetransmitRegistrationInterval = TimeSpan.FromMilliseconds(5);
        /// <summary>
        /// Value returned by <see cref="GetProfilerMarker"/>.
        /// </summary>
        static IntPtr s_ProfilerMarker = CreateProfilingMarker(nameof(RegisterWithEmitterState));
    }
}
