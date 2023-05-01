using System;
using System.Diagnostics;
using Utils;
#if !UNITY_EDITOR
using UnityEngine;
#endif

namespace Unity.ClusterDisplay.EmitterStateMachine
{
    /// <summary>
    /// <see cref="NodeState"/> that propagate the quit message and terminate the application / game once all repeaters
    /// have acknowledged the quit request.
    /// </summary>
    class PropagateQuitState: NodeState<EmitterNode>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="node">Node we are a state of.</param>
        public PropagateQuitState(EmitterNode node)
            : base(node)
        {
        }

        protected override (NodeState, DoFrameResult?) DoFrameImplementation()
        {
            var udpAgent = Node.UdpAgent;

            long repeatQuitDeadline = 0;
            NodeIdBitVector waitForAckFrom = new(Node.RepeatersStatus.RepeaterPresence);
            do
            {
                // Do we need to send / repeat the quit message
                if (Stopwatch.GetTimestamp() > repeatQuitDeadline)
                {
                    PropagateQuit quitMessage = new();
                    udpAgent.SendMessage(MessageType.PropagateQuit, quitMessage);
                    if (repeatQuitDeadline != 0)
                    {
                        Node.UdpAgent.Stats.SentMessageWasRepeat(MessageType.PropagateQuit);
                    }

                    repeatQuitDeadline = StopwatchUtils.TimestampIn(k_RepeatPropagateQuitInterval);
                }

                // Wait for a RegisteringWithEmitter message
                using var receivedMessage =
                    udpAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(repeatQuitDeadline));
                if (receivedMessage is not {Type: MessageType.QuitReceived})
                {
                    // Skip all other messages, out only focus right now is to know when all repeaters have received
                    // the quit message so that we can also quit.
                    continue;
                }

                // Update our status and set a response
                var quitReceived = (ReceivedMessage<QuitReceived>)receivedMessage;
                waitForAckFrom[quitReceived.Payload.NodeId] = false;

                // Continue consuming messages until we know about every emitter or (or something somewhere decide it
                // takes too much time and kill this process).
            } while (waitForAckFrom.SetBitsCount > 0);

            // That's it, every not have received the quit signal, we can now quit.
            Quit();
            return (null, DoFrameResult.FrameDone);
        }

        /// <summary>
        /// Overridable method called to trigger quit of the application / game
        /// </summary>
        protected virtual void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        protected override IntPtr GetProfilerMarker() => s_ProfilerMarker;

        /// <summary>
        /// Value returned by <see cref="GetProfilerMarker"/>.
        /// </summary>
        static IntPtr s_ProfilerMarker = CreateProfilingMarker(nameof(PropagateQuit));

        /// <summary>
        /// Time between repeats of <see cref="MessageType.PropagateQuit"/>.
        /// </summary>
        static readonly TimeSpan k_RepeatPropagateQuitInterval = TimeSpan.FromMilliseconds(100);
    }
}
