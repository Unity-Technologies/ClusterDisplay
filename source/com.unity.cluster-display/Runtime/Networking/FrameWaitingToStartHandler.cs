using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using Utils;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Class responsible to listen for <see cref="RepeaterWaitingToStartFrame"/>, answer them and maintain a list of
    /// all the repeater nodes that are ready.
    /// </summary>
    /// <remarks>Emitter is handling WaitingToStartFrame logic in a dedicated class like this one connected to
    /// <see cref="IUdpAgent.AddPreProcess"/> instead of manually in a <see cref="NodeState"/> so that it can react as
    /// quickly as possible to <see cref="RepeaterWaitingToStartFrame"/> so that repeater receives an
    /// <see cref="EmitterWaitingToStartFrame"/> quickly to avoid repeating <see cref="RepeaterWaitingToStartFrame"/>
    /// for nothing.</remarks>
    class FrameWaitingToStartHandler: IDisposable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="udpAgent">Object through which we we are receiving <see cref="RepeaterWaitingToStartFrame"/>
        /// and transmitting <see cref="EmitterWaitingToStartFrame"/>.</param>
        /// <param name="emitterNodeId">Identifier of the emitter node executing this code.</param>
        /// <param name="toWaitFor">Repeater nodes that we have to wait on through network synchronization.</param>
        /// <param name="firstFrameIndex">Index of the first frame we will be waiting for (and then increment).</param>
        /// <param name="clusterTopology">List to monitor for changes to the cluster topology (which might interrupt the
        /// wait).</param>
        public FrameWaitingToStartHandler(IUdpAgent udpAgent, byte emitterNodeId, NodeIdBitVectorReadOnly toWaitFor,
            ulong firstFrameIndex = 0, [CanBeNull] ClusterTopology clusterTopology = null)
        {
            m_EmitterNodeId = emitterNodeId;
            m_ClusterTopology = clusterTopology;
            m_FrameIndex = firstFrameIndex;

            if (!udpAgent.ReceivedMessageTypes.Contains(MessageType.RepeaterWaitingToStartFrame))
            {
                throw new ArgumentException("UdpAgent does not support receiving required MessageType.RepeaterWaitingToStartFrame");
            }

            m_ToWaitFor = new (toWaitFor);
            m_StillWaitingOn = new (toWaitFor);

            UdpAgent = udpAgent;
            UdpAgent.AddPreProcess(UdpAgentPreProcessPriorityTable.RepeaterWaitingToStartFrame, PreProcessReceivedMessage);
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            if (UdpAgent != null)
            {
                UdpAgent.RemovePreProcess(PreProcessReceivedMessage);
            }
            if (m_UpdatedClusterTopologyChangedRegistered)
            {
                m_ClusterTopology.Changed -= Changed;
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
            // Manage registration to cluster topology changes so that we immediately wake up when a change happens.
            if (m_ClusterTopology != null)
            {
                if (!m_UpdatedClusterTopologyChangedRegistered)
                {
                    m_ClusterTopology.Changed += Changed;
                }
            }

            long deadlineTimestamp = StopwatchUtils.TimestampIn(maxTime);
            lock (m_ThisLock)
            {
                while ((Stopwatch.GetTimestamp() <= deadlineTimestamp) &&
                       (frameIndex > m_FrameIndex ||
                        (frameIndex == m_FrameIndex && m_StillWaitingOn.SetBitsCount > 0)))
                {
                    if (m_ClusterTopology?.Entries != null &&
                        (!m_LastAnalyzedTopology.TryGetTarget(out var lastAnalyzedTopology) ||
                         !ReferenceEquals(lastAnalyzedTopology, m_ClusterTopology.Entries)))
                    {
                        // First thing to check, is this node still the emitter, if no then stop right away and don't
                        // claim we are done waiting for the repeater (return the last list we knew we were still
                        // waiting on).
                        bool stillEmitter = false;
                        foreach (var entry in m_ClusterTopology.Entries)
                        {
                            if (entry.NodeId == m_EmitterNodeId && entry.NodeRole == NodeRole.Emitter)
                            {
                                stillEmitter = true;
                            }
                        }
                        if (!stillEmitter)
                        {
                            return new NodeIdBitVectorReadOnly(m_StillWaitingOn);
                        }

                        // Now let's check if some of the repeaters are gone
                        HandleRemovedRepeaters(m_ClusterTopology.Entries);
                        m_LastAnalyzedTopology.SetTarget(m_ClusterTopology.Entries);
                        continue; // Since we might have removed the last repeater we were waiting on...
                    }

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
        /// <returns>What to do with the received message.</returns>
        PreProcessResult PreProcessReceivedMessage(ReceivedMessageBase received)
        {
            // We are only interested in FrameData we receive, everything else should simply pass through
            if (received.Type != MessageType.RepeaterWaitingToStartFrame)
            {
                return PreProcessResult.PassThrough();
            }

            var receivedWaitingToStart = (ReceivedMessage<RepeaterWaitingToStartFrame>)received;

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
                    m_StillWaitingOn.CopyTo(out answer.WaitingOn0, out answer.WaitingOn1, out answer.WaitingOn2,
                        out answer.WaitingOn3);
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
                    k_AllZero.CopyTo(out answer.WaitingOn0, out answer.WaitingOn1, out answer.WaitingOn2,
                        out answer.WaitingOn3);
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
            return PreProcessResult.Stop();
        }

        /// <summary>
        /// Delegate called when <see cref="ClusterTopology.Changed"/> is fired.
        /// </summary>
        void Changed()
        {
            lock (m_ThisLock)
            {
                Monitor.PulseAll(m_ThisLock);
            }
        }

        /// <summary>
        /// Check for repeaters that are not included in the cluster topology anymore.
        /// </summary>
        void HandleRemovedRepeaters(IReadOnlyList<ClusterTopologyEntry> topologyEntries)
        {
            NodeIdBitVector repeatersStillPresent = new();
            foreach (var entry in topologyEntries)
            {
                if (entry.NodeRole is NodeRole.Repeater or NodeRole.Backup)
                {
                    repeatersStillPresent[entry.NodeId] = true;
                }
            }

            foreach (var toWaitFor in m_ToWaitFor.ExtractSetBits())
            {
                if (!repeatersStillPresent[toWaitFor])
                {
                    m_ToWaitFor[toWaitFor] = false;
                    m_StillWaitingOn[toWaitFor] = false;
                }
            }
        }

        /// <summary>
        /// Identifier of the emitter node executing this code.
        /// </summary>
        readonly byte m_EmitterNodeId;
        /// <summary>
        /// List to monitor for changes to the cluster topology (which might interrupt the wait).
        /// </summary>
        readonly ClusterTopology m_ClusterTopology;

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
        /// Have we registered <see cref="ClusterTopology.Changed"/> to be informed immediately when topology changes
        /// are received?
        /// </summary>
        bool m_UpdatedClusterTopologyChangedRegistered;
        /// <summary>
        /// Last analyzed topology change.
        /// </summary>
        WeakReference<IReadOnlyList<ClusterTopologyEntry>> m_LastAnalyzedTopology = new(null);

        /// <summary>
        /// <see cref="NodeIdBitVector"/> with all bits always set to 0.
        /// </summary>
        static readonly NodeIdBitVector k_AllZero = new();
    }
}
