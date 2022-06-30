using System;
using Unity.Profiling;
using UnityEngine;
using Utils;

namespace Unity.ClusterDisplay.EmitterStateMachine
{
    /// <summary>
    /// <see cref="NodeState"/> that transmit the current game state to repeaters.
    /// </summary>
    class EmitFrameState: NodeState<EmitterNode>, IDisposable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="node">Node we are a state of.</param>
        public EmitFrameState(EmitterNode node)
            : base(node)
        {
            m_Splitter = new(Node.UdpAgent, new ConcurrentObjectPool<FrameDataBuffer>(() => new FrameDataBuffer()));
            m_StartHandler = new(Node.UdpAgent, Node.RepeatersStatus.RepeaterPresence);
            m_Emitter = new(Node.Config.RepeatersDelayed);
            Node.UdpAgent.OnMessagePreProcess += AnswerRegisteringWithEmitter;
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            m_Emitter?.Dispose();
            m_StartHandler?.Dispose();
            m_Splitter?.Dispose();
        }

        protected override NodeState DoFrameImplementation()
        {
            // Special case for delayed repeaters.  Gather game state for frame 0 and the custom data will be gathered
            // when starting frame 1.  Don't sync or anything, in fact let's try to finish ASAP so that we can move on
            // to frame 1 so that we can sent frame 0 and repeaters at last also receive frame 0.
            if (Node.FrameIndex == 0 && Node.Config.RepeatersDelayed)
            {
                m_Emitter.GatherPreFrameState();
                return null;
            }

            ulong effectiveFrameIndex = Node.Config.RepeatersDelayed ? Node.FrameIndex - 1 : Node.FrameIndex;

            // Perform network based start of frame synchronization
            if (m_UsingNetworkSync)
            {
                using (s_MarkerNetworkSynchronization.Auto())
                {
                    var stillWaitingOn =
                        m_StartHandler.TryWaitForAllRepeatersReady(effectiveFrameIndex, Node.Config.CommunicationTimeout);
                    if (stillWaitingOn != null)
                    {
                        // Looks like some repeater nodes are not responding, drop them...
                        Debug.LogError($"Repeaters {stillWaitingOn} did not signaled they were ready within " +
                            $"{Node.Config.CommunicationTimeout.TotalSeconds} seconds, they will be dropped from the cluster.");

                        m_StartHandler.DropRepeaters(stillWaitingOn);

                        byte[] nodeIds = stillWaitingOn.ExtractSetBits();
                        foreach (var nodeId in nodeIds)
                        {
                            Node.RepeatersStatus[nodeId] = new RepeaterStatus();
                        }
                    }

                    // Prepare for next frame
                    m_UsingNetworkSync = m_StartHandler.PrepareForNextFrame(effectiveFrameIndex + 1);
                }
            }

            // Emit the frame data for this frame
            using (s_MarkerGatherState.Auto())
            {
                m_Emitter.GatherFrameState();
            }
            using (s_MarkerPublishState.Auto())
            {
                m_Emitter.PublishCurrentState(effectiveFrameIndex, m_Splitter);
            }

            // EmitFrameState never switch to another state...
            return null;
        }

        /// <summary>
        /// Handle reception of <see cref="RegisteringWithEmitter"/> and reply if their registration was accepted or not.
        /// </summary>
        /// <param name="message">Received message</param>
        /// <remarks>Necessary to deal with the case where the <see cref="RepeaterRegistered"/> sent during
        /// <see cref="WelcomeRepeatersState"/> was lost.  Repeater will repeat the <see cref="RepeaterRegistered"/>
        /// and so we have to also repeat the answer.  We however do not accept new repeaters, it is too late.</remarks>
        ReceivedMessageBase AnswerRegisteringWithEmitter(ReceivedMessageBase message)
        {
            if (message.Type != MessageType.RegisteringWithEmitter)
            {
                return message;
            }

            using var receivedRegisteringWithEmitter = message as ReceivedMessage<RegisteringWithEmitter>;
            if (receivedRegisteringWithEmitter != null)
            {
                var answer =
                    Node.RepeatersStatus.ProcessRegisteringMessage(receivedRegisteringWithEmitter.Payload, false);
                Node.UdpAgent.SendMessage(MessageType.RepeaterRegistered, answer);
                Node.UdpAgent.Stats.SentMessageWasRepeat(MessageType.RepeaterRegistered);
            }

            return null;
        }

        protected override IntPtr GetProfilerMarker() => s_ProfilerMarker;

        /// <summary>
        /// Object responsible for splitting data of a frame, sending it and deal with retransmission requests.
        /// </summary>
        FrameDataSplitter m_Splitter;
        /// <summary>
        /// Object responsible in collecting ready state of repeater nodes when performing network synchronization.
        /// </summary>
        FrameWaitingToStartHandler m_StartHandler;
        /// <summary>
        /// Object responsible for assembling the FrameData to transmit and sending it using m_Splitter.
        /// </summary>
        EmitterStateWriter m_Emitter;
        /// <summary>
        /// Do we still have some repeaters using a network based inter-node synchronization (in opposition to an
        /// hardware based synchronization).
        /// </summary>
        bool m_UsingNetworkSync = true;

        /// <summary>
        /// Value returned by <see cref="GetProfilerMarker"/>.
        /// </summary>
        static IntPtr s_ProfilerMarker = CreateProfilingMarker(nameof(EmitFrameState));
        /// <summary>
        /// <see cref="ProfilerMarker"/> used to identify the time spent waiting in the network based synchronization.
        /// </summary>
        static ProfilerMarker s_MarkerNetworkSynchronization = new ProfilerMarker("NetworkSynchronization");
        /// <summary>
        /// <see cref="ProfilerMarker"/> used to identify the time spent waiting gathering the frame state to send.
        /// </summary>
        static ProfilerMarker s_MarkerGatherState = new ProfilerMarker("GatherState");
        /// <summary>
        /// <see cref="ProfilerMarker"/> used to identify the time spent sending the frame state.
        /// </summary>
        static ProfilerMarker s_MarkerPublishState = new ProfilerMarker("PublishState");
    }
}
