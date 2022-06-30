using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Utils;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Class responsible to listen for <see cref="RepeaterWaitingToStartFrame"/>, answer them and maintain a list of
    /// all the repeater nodes that are ready.
    /// </summary>
    /// <remarks>Emitter is handling WaitingToStartFrame logic in a dedicated class like this one connected to
    /// <see cref="IUdpAgent.OnMessagePreProcess"/> instead of manually in a <see cref="NodeState"/> so that it can
    /// react as quickly as possible to <see cref="RepeaterWaitingToStartFrame"/> so that repeater receives an
    /// <see cref="EmitterWaitingToStartFrame"/> quickly to avoid repeating <see cref="RepeaterWaitingToStartFrame"/>
    /// for nothing.</remarks>
    class FrameWaitingToStartHandler: IDisposable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="udpAgent">Object through which we we are receiving <see cref="RepeaterWaitingToStartFrame"/>
        /// and transmitting <see cref="EmitterWaitingToStartFrame"/>.</param>
        /// <param name="toWaitFor">Repeater nodes that we have to wait on through network synchronization.</param>
        public FrameWaitingToStartHandler(IUdpAgent udpAgent, NodeIdBitVectorReadOnly toWaitFor)
        {
            if (!udpAgent.ReceivedMessageTypes.Contains(MessageType.RepeaterWaitingToStartFrame))
            {
                throw new ArgumentException("UdpAgent does not support receiving required MessageType.RepeaterWaitingToStartFrame");
            }

            m_ToWaitFor = new (toWaitFor);
            m_StillWaitingOn = new (toWaitFor);

            UdpAgent = udpAgent;
            UdpAgent.OnMessagePreProcess += PreProcessReceivedMessage;
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            if (UdpAgent != null)
            {
                UdpAgent.OnMessagePreProcess -= PreProcessReceivedMessage;
            }
        }

        /// <summary>
        /// Network access object through which we we are receiving <see cref="RepeaterWaitingToStartFrame"/> and
        /// transmitting <see cref="EmitterWaitingToStartFrame"/>.
        /// </summary>
        public IUdpAgent UdpAgent { get; }

        /// <summary>
        /// Block execution until all repeaters are ready to start the given frame.
        /// </summary>
        /// <param name="frameIndex">Frame index</param>
        /// <param name="maxTime">Maximum amount of time the caller want to wait.</param>
        /// <returns>Nodes we are still waiting on or <c>null</c> if all repeater nodes are ready.</returns>
        public NodeIdBitVectorReadOnly TryWaitForAllRepeatersReady(ulong frameIndex, TimeSpan maxTime)
        {
            long deadlineTimestamp = StopwatchUtils.TimestampIn(maxTime);
            lock (m_ThisLock)
            {
                while ((Stopwatch.GetTimestamp() <= deadlineTimestamp) &&
                       (frameIndex > m_FrameIndex ||
                        (frameIndex == m_FrameIndex && m_StillWaitingOn.SetBitsCount > 0)))
                {
                    Monitor.Wait(m_ThisLock, StopwatchUtils.TimeUntil(deadlineTimestamp));
                }

                if (frameIndex == m_FrameIndex && m_StillWaitingOn.SetBitsCount == 0)
                {
                    return null;
                }
                else
                {
                    return new NodeIdBitVectorReadOnly(m_StillWaitingOn);
                }
            }
        }

        /// <summary>
        /// Prepare the <see cref="FrameWaitingToStartHandler"/> for handling <see cref="RepeaterWaitingToStartFrame"/>
        /// of the next frame.
        /// </summary>
        /// <param name="frameIndex">Index o</param>
        /// <returns>Do we have at least one repeater to wait for on the next frame (<c>true</c>) or we are done with
        /// network synchronization for the rest of this run (<c>false</c>).</returns>
        /// <exception cref="ArgumentException">If is not equal to previous frameIndex + 1</exception>
        public bool PrepareForNextFrame(ulong frameIndex)
        {
            lock (m_ThisLock)
            {
                if (frameIndex != m_FrameIndex + 1)
                {
                    throw new ArgumentException(nameof(frameIndex));
                }

                ++m_FrameIndex;
                m_StillWaitingOn.SetFrom(m_ToWaitFor);
                return m_StillWaitingOn.SetBitsCount > 0;
            }
        }

        /// <summary>
        /// Remove the given repeaters from the list of repeaters we will be waiting on next frame.
        /// </summary>
        /// <param name="toDrop">Repeater nodes to remove from the list.</param>
        public void DropRepeaters(NodeIdBitVectorReadOnly toDrop)
        {
            lock (m_ThisLock)
            {
                m_ToWaitFor.Clear(toDrop);
                m_StillWaitingOn.Clear(toDrop);
            }
        }

        /// <summary>
        /// Preprocess a received message and track the state of repeaters that are ready to proceed to the next frame.
        /// </summary>
        /// <param name="received">Received <see cref="ReceivedMessageBase"/> to preprocess.</param>
        /// <returns>Preprocessed <see cref="ReceivedMessageBase"/> or null if the message is to be dropped.</returns>
        ReceivedMessageBase PreProcessReceivedMessage(ReceivedMessageBase received)
        {
            // We are only interested in FrameData we receive, everything else should simply pass through
            if (received.Type != MessageType.RepeaterWaitingToStartFrame)
            {
                return received;
            }

            using var receivedWaitingToStart = received as ReceivedMessage<RepeaterWaitingToStartFrame>;
            Debug.Assert(receivedWaitingToStart != null); // since received.Type == MessageType.RepeaterWaitingToStartFrame

            lock (m_ThisLock)
            {
                if (receivedWaitingToStart.Payload.FrameIndex == m_FrameIndex)
                {
                    if (!receivedWaitingToStart.Payload.WillUseNetworkSyncOnNextFrame)
                    {
                        m_ToWaitFor[receivedWaitingToStart.Payload.NodeId] = false;
                    }

                    bool wasSet = m_StillWaitingOn[receivedWaitingToStart.Payload.NodeId];
                    m_StillWaitingOn[receivedWaitingToStart.Payload.NodeId] = false;

                    if (m_StillWaitingOn.SetBitsCount == 0)
                    {
                        Monitor.PulseAll(m_ThisLock);
                    }

                    var answer = new EmitterWaitingToStartFrame()
                    {
                        FrameIndex = receivedWaitingToStart.Payload.FrameIndex
                    };
                    unsafe
                    {
                        m_StillWaitingOn.CopyTo(answer.WaitingOn);
                    }
                    UdpAgent.SendMessage(MessageType.EmitterWaitingToStartFrame, answer);

                    if (!wasSet)
                    {
                        UdpAgent.Stats.SentMessageWasRepeat(MessageType.EmitterWaitingToStartFrame);
                    }
                }
                else if (receivedWaitingToStart.Payload.FrameIndex < m_FrameIndex)
                {
                    var answer = new EmitterWaitingToStartFrame()
                    {
                        FrameIndex = receivedWaitingToStart.Payload.FrameIndex
                    };
                    unsafe
                    {
                        k_AllZero.CopyTo(answer.WaitingOn);
                    }
                    UdpAgent.SendMessage(MessageType.EmitterWaitingToStartFrame, answer);
                    UdpAgent.Stats.SentMessageWasRepeat(MessageType.EmitterWaitingToStartFrame);
                }
                else if (receivedWaitingToStart.Payload.FrameIndex > m_FrameIndex)
                {
                    throw new InvalidDataException("Received a RepeaterWaitingToStartFrame for frame " +
                        $"{receivedWaitingToStart.Payload.FrameIndex} while we are gathering status for {m_FrameIndex}.");
                }
            }

            // We never pass any RepeaterWaitingToStartFrame through...
            return null;
        }

        /// <summary>
        /// Object to be locked when code needs to serialize access to member variables.
        /// </summary>
        readonly object m_ThisLock = new();
        /// <summary>
        /// Index of the frame we are currently processing.
        /// </summary>
        ulong m_FrameIndex;
        /// <summary>
        /// Repeaters that are using network synchronization and that we have to wait for.
        /// </summary>
        NodeIdBitVector m_ToWaitFor;
        /// <summary>
        /// Repeaters that we are still waiting on for the current frame.
        /// </summary>
        NodeIdBitVector m_StillWaitingOn;

        /// <summary>
        /// <see cref="NodeIdBitVector"/> with all bits always set to 0.
        /// </summary>
        static readonly NodeIdBitVector k_AllZero = new();
    }
}
