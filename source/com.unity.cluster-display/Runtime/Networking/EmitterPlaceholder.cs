using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using JetBrains.Annotations;
using Unity.Collections;
using Utils;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// The objective of this class is to act like an <see cref="EmitterNode"/> to replace one that cannot perform its
    /// work anymore by surveying the current state of the cluster and preparing it so that a new
    /// <see cref="EmitterNode"/> can take its place.
    /// </summary>
    /// <remarks>The lifetime of such a placeholder is:
    /// <list type="number">
    /// <item>Survey other nodes of the cluster to know their state.</item>
    /// <item>Gather all the data from other nodes so that it can be retransmitted to other nodes that might need it.
    /// </item>
    /// <item>Perform network synchronization to allow advancing all repeaters to the same point.</item>
    /// </list>
    /// ... and report when all nodes are synchronized (waiting for data from the same frame) so that we can change the
    /// <see cref="RepeaterNode"/> (with a role of <see cref="NodeRole.Backup"/>) to an <see cref="EmitterNode"/>.
    /// <br/><br/>Implementation of this class might not be 100% as efficient as it could be (doing some avoidable heap
    /// allocation) but it should only be running when a node fail and not for long, so the amount of generated garbage
    /// should in the end be negligible and it allows for simpler code...
    /// </remarks>
    class EmitterPlaceholder : IDisposable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="updatedClusterTopology">Updated cluster topology (that might still receive some future updates)
        /// that triggered the need for this placeholder to transition to a new topology.</param>
        /// <param name="udpAgent">Object through which we will perform communication with other nodes in the cluster.
        /// Must support reception of <see cref="EmitterPlaceholder.ReceiveMessageTypes"/>.</param>
        public EmitterPlaceholder(ClusterTopology updatedClusterTopology, IUdpAgent udpAgent)
        {
#if DEBUG
            foreach (var messageType in ReceiveMessageTypes)
            {
                Debug.Assert(udpAgent.ReceivedMessageTypes.Contains(messageType));
            }
#endif

            m_UpdatedClusterTopology = updatedClusterTopology;
            m_UdpAgent = udpAgent;

            m_UdpAgent.AddPreProcess(UdpAgentPreProcessPriorityTable.EmitterPlaceholder, PreProcessMessage);
            m_UpdatedClusterTopology.Changed += ClusterTopologyUpdated;
            ClusterTopologyUpdated();

            m_MaxDataPerMessage = m_UdpAgent.MaximumMessageSize - Marshal.SizeOf<FrameData>();

            m_WorkThread = new(WorkThreadFunc);
            m_WorkThread.Start();
        }

        /// <summary>
        /// Returns if all the repeaters are synchronized (<see cref="RepeatersSurveyAnswer.LastReceivedFrameIndex"/>
        /// matching everywhere)
        /// </summary>
        public bool RepeatersSynchronized
        {
            get
            {
                if (!m_HasResponseFromAllRepeaters.WaitOne(0))
                {
                    return false;
                }

                lock (m_Lock)
                {
                    return m_SurveyMinLastReceivedFrameIndex == m_SurveyMaxLastReceivedFrameIndex;
                }
            }
        }

        /// <summary>
        /// Return for how long the responses to the survey have been stable.
        /// </summary>
        public TimeSpan SurveyStableSince
        {
            get
            {
                if (!m_HasResponseFromAllRepeaters.WaitOne(0))
                {
                    return TimeSpan.Zero;
                }

                lock (m_Lock)
                {
                    return StopwatchUtils.ElapsedSince(m_LastChangeInSurvey);
                }
            }
        }

        /// <summary>
        /// <see cref="RepeatersSurveyAnswer"/> of every repeater (or backup) nodes.
        /// </summary>
        public IReadOnlyCollection<RepeatersSurveyAnswer> RepeatersSurveyResult => m_SurveyAnswers.Values;

        /// <summary>
        /// <see cref="MessageType"/> that the <see cref="IUdpAgent"/> must process upon reception.
        /// </summary>
        public static IReadOnlyCollection<MessageType> ReceiveMessageTypes => s_ReceiveMessageTypes;

        public void Dispose()
        {
            m_WorkThreadSpinning = false;
            m_HasResponseFromAllRepeaters.Set();
            m_WorkThread?.Join();

            m_UdpAgent.RemovePreProcess(PreProcessMessage);
            m_UpdatedClusterTopology.Changed -= ClusterTopologyUpdated;
        }

        /// <summary>
        /// Main loop of <see cref="EmitterPlaceholder"/>.
        /// </summary>
        void WorkThreadFunc()
        {
            while (m_WorkThreadSpinning)
            {
                long nextSurveyTimestamp = StopwatchUtils.TimestampIn(k_SurveyInterval);

                // Update the survey of the state of the repeater nodes
                m_UdpAgent.SendMessage(MessageType.SurveyRepeaters, new SurveyRepeaters());
                if (!m_HasResponseFromAllRepeaters.WaitOne(k_SurveyInterval))
                {
                    continue;
                }

                // Gather all frames to be retransmitted in place of the emitter.
                FetchMissingDataBuffers();

                // The really important job of this class (responding to received messages to allow repeater nodes to
                // all reach the same state) is done "in the background" by PreProcessMessage.  So simply wait a little
                // bit and re-do a survey to monitor progress in the repeaters.
                Thread.Sleep(StopwatchUtils.TimeUntil(nextSurveyTimestamp));
            }
        }

        /// <summary>
        /// Fetch data of frames that some of the repeaters are missing and others have.
        /// </summary>
        void FetchMissingDataBuffers()
        {
            List<ulong> framesToFetch = new();
            lock (m_Lock)
            {
                for (ulong frameIndex = m_SurveyMinLastReceivedFrameIndex + 1;
                     frameIndex <= m_SurveyMaxLastReceivedFrameIndex; ++frameIndex)
                {
                    if (!m_FrameDataHistory.ContainsKey(frameIndex))
                    {
                        framesToFetch.Add(frameIndex);
                    }
                }
            }

            if (framesToFetch.Any())
            {
                var fetchDeadline = StopwatchUtils.TimestampIn(k_FetchFramesTimespan);
                foreach (var frameIndex in framesToFetch)
                {
                    var frameDataBuffer = FetchOldFrameDataBuffer(frameIndex, fetchDeadline);
                    if (frameDataBuffer != null)
                    {
                        lock (m_Lock)
                        {
                            m_FrameDataHistory[frameIndex] = frameDataBuffer;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fetch data of an old frame from the repeaters.
        /// </summary>
        /// <param name="frameIndex">Index of the old frame to fetch.</param>
        /// <param name="fetchDeadline">Maximum time until which to wait for all frame data.</param>
        /// <returns>Fetched data.</returns>
        [CanBeNull]
        FrameDataBuffer FetchOldFrameDataBuffer(ulong frameIndex, long fetchDeadline)
        {
            NativeArray<byte> dataBufferStorage = new();
            HashSet<int> receivedDatagrams = new();
            int receivedBytes = 0;

            List<ClusterTopologyEntry> topologyEntries = new();
            lock (m_Lock)
            {
                topologyEntries.AddRange(m_TopologyEntries);
            }

            while ((!dataBufferStorage.IsCreated || receivedBytes < dataBufferStorage.Length) &&
                   Stopwatch.GetTimestamp() < fetchDeadline)
            {
                foreach (var topologyEntry in topologyEntries)
                {
                    if (topologyEntry.NodeRole is not (NodeRole.Repeater or NodeRole.Backup))
                    {
                        continue;
                    }

                    // Ask the repeater node to transmit us this "old data".
                    m_UdpAgent.SendMessage(MessageType.RetransmitReceivedFrameData,
                        new RetransmitReceivedFrameData() {NodeId = topologyEntry.NodeId, FrameIndex = frameIndex});
                    long nextActivityDeadline =
                        Math.Min(StopwatchUtils.TimestampIn(k_FetchFrameDataMaxIdleTime), fetchDeadline);

                    while ((!dataBufferStorage.IsCreated || receivedBytes < dataBufferStorage.Length) &&
                           Stopwatch.GetTimestamp() < nextActivityDeadline)
                    {
                        // Wait until we receive a RetransmittedReceivedFrameData
                        using var receivedMessage =
                            m_UdpAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(nextActivityDeadline));
                        if (receivedMessage is not ReceivedMessage<RetransmittedReceivedFrameData> receivedRetransmittedFrameData)
                        {
                            continue;
                        }

                        // Quick return for cases where that repeater does not have the data
                        if (receivedRetransmittedFrameData.Payload.DataLength <= 0)
                        {
                            break;
                        }

                        // Allocate a buffer large enough to store the assembled FrameData
                        if (!dataBufferStorage.IsCreated ||
                            receivedRetransmittedFrameData.Payload.DataLength != dataBufferStorage.Length)
                        {
                            if (dataBufferStorage.IsCreated)
                            {
                                dataBufferStorage.Dispose();
                            }

                            dataBufferStorage =
                                new(receivedRetransmittedFrameData.Payload.DataLength, Allocator.Persistent);
                            receivedDatagrams.Clear();
                            receivedBytes = 0;
                        }

                        // Skip already processed datagrams
                        if (receivedDatagrams.Contains(receivedRetransmittedFrameData.Payload.DatagramIndex))
                        {
                            continue;
                        }
                        receivedDatagrams.Add(receivedRetransmittedFrameData.Payload.DatagramIndex);

                        // Copy data to the assembled buffer
                        NativeArray<byte>.Copy(receivedRetransmittedFrameData.ExtraData.AsNativeArray(), 0,
                            dataBufferStorage, receivedRetransmittedFrameData.Payload.DatagramDataOffset,
                            receivedRetransmittedFrameData.ExtraData.Length);
                        receivedBytes += receivedRetransmittedFrameData.ExtraData.Length;

                        // We received one part of the frame data...  node appear to be responding, increase max time to
                        // receive next datagram forming the full frame data.
                        nextActivityDeadline =
                            Math.Min(StopwatchUtils.TimestampIn(k_FetchFrameDataMaxIdleTime), fetchDeadline);
                    }

                    if (dataBufferStorage.IsCreated && receivedBytes >= dataBufferStorage.Length)
                    {
                        break;
                    }
                }
            }

            if (dataBufferStorage.IsCreated && receivedBytes >= dataBufferStorage.Length)
            {
                FrameDataBuffer ret = new();
                ret.AdoptNativeArray(dataBufferStorage);
                return ret;
            }
            if (dataBufferStorage.IsCreated)
            {
                dataBufferStorage.Dispose();
            }
            return null;
        }

        /// <summary>
        /// Preprocess a received message and assemble complete FrameData from them.
        /// </summary>
        /// <param name="received">Received <see cref="ReceivedMessageBase"/> to preprocess.</param>
        /// <returns>What to do of the received message.</returns>
        PreProcessResult PreProcessMessage(ReceivedMessageBase received)
        {
            return received switch
            {
                ReceivedMessage<RetransmitFrameData> retransmitRequest =>
                    PreProcessRetransmitFrameData(retransmitRequest),
                ReceivedMessage<RepeaterWaitingToStartFrame> repeaterWaitingToStartFrame =>
                    PreProcessRepeaterWaitingToStartFrame(repeaterWaitingToStartFrame),
                ReceivedMessage<RepeatersSurveyAnswer> surveyAnswer =>
                    PreProcessRepeatersSurveyAnswer(surveyAnswer),
                _ => PreProcessResult.PassThrough()
            };
        }

        /// <summary>
        /// Process a received <see cref="RetransmitFrameData"/>.
        /// </summary>
        /// <param name="retransmitRequest">Received <see cref="RetransmitFrameData"/>.</param>
        /// <returns>What to do of the received message.</returns>
        PreProcessResult PreProcessRetransmitFrameData(ReceivedMessage<RetransmitFrameData> retransmitRequest)
        {
            Dictionary<int, NativeArray<byte>.ReadOnly> datagramsData = new();
            int dataLength;
            lock (m_Lock)
            {
                if (m_FrameDataHistory.TryGetValue(retransmitRequest.Payload.FrameIndex, out var frameData))
                {
                    dataLength = frameData.Length;
                    for (int datagramIndex = retransmitRequest.Payload.DatagramIndexIndexStart;
                         datagramIndex < retransmitRequest.Payload.DatagramIndexIndexEnd; ++datagramIndex)
                    {
                        int datagramStart = datagramIndex * m_MaxDataPerMessage;
                        if (datagramStart >= frameData.Length)
                        {
                            break;
                        }
                        int datagramEnd = Math.Min((datagramIndex + 1) * m_MaxDataPerMessage, frameData.Length);
                        datagramsData[datagramIndex] = frameData.DataSpan(datagramStart, datagramEnd - datagramStart);
                    }
                }
                else
                {
                    return PreProcessResult.Stop();
                }

                // Remark: No problem in having NativeArray<byte>.ReadOnly (used outside the lock) referencing data in
                // m_FrameDataHistory (that needs to be locked) as once data is added to m_FrameDataHistory it is never
                // modified or removed (until disposable of this).
            }

            for (int datagramIndex = retransmitRequest.Payload.DatagramIndexIndexStart;
                 datagramIndex < retransmitRequest.Payload.DatagramIndexIndexEnd; ++datagramIndex)
            {
                if (datagramsData.TryGetValue(datagramIndex, out var datagramData))
                {
                    m_UdpAgent.SendMessage(MessageType.FrameData, new FrameData() {
                        FrameIndex = retransmitRequest.Payload.FrameIndex, DataLength = dataLength,
                        DatagramIndex = datagramIndex, DatagramDataOffset = datagramIndex * m_MaxDataPerMessage
                    }, datagramData);
                }
                else
                {
                    break;
                }
            }

            return PreProcessResult.Stop();
        }

        /// <summary>
        /// Process a received <see cref="RepeaterWaitingToStartFrame"/>.
        /// </summary>
        /// <param name="repeaterWaitingToStartFrame">Received <see cref="RepeaterWaitingToStartFrame"/>.</param>
        /// <returns>What to do of the received message.</returns>
        PreProcessResult PreProcessRepeaterWaitingToStartFrame(ReceivedMessage<RepeaterWaitingToStartFrame>
            repeaterWaitingToStartFrame)
        {
            if (!m_HasResponseFromAllRepeaters.WaitOne(0))
            {
                // We are still waiting for feedback from other repeaters, we cannot allow that node to continue forward
                // yet.
                return PreProcessResult.Stop();
            }

            bool sendEmitterWaitingToStartFrame;
            lock (m_Lock)
            {
                // Is the repeater asking to move forward from the oldest frame?  If so let's say we are ready to go.
                sendEmitterWaitingToStartFrame =
                    repeaterWaitingToStartFrame.Payload.FrameIndex == m_SurveyMinLastReceivedFrameIndex + 1;
            }

            if (sendEmitterWaitingToStartFrame)
            {
                m_UdpAgent.SendMessage(MessageType.EmitterWaitingToStartFrame, new EmitterWaitingToStartFrame()
                {
                    FrameIndex = m_SurveyMinLastReceivedFrameIndex + 1,
                    WaitingOn0 = 0, WaitingOn1 = 0, WaitingOn2 = 0, WaitingOn3 = 0
                });
            }

            return PreProcessResult.Stop();
        }

        /// <summary>
        /// Process a received <see cref="RepeatersSurveyAnswer"/>.
        /// </summary>
        /// <param name="surveyAnswer">Received <see cref="RepeatersSurveyAnswer"/>.</param>
        /// <returns>What to do of the received message.</returns>
        PreProcessResult PreProcessRepeatersSurveyAnswer(ReceivedMessage<RepeatersSurveyAnswer> surveyAnswer)
        {
            lock (m_Lock)
            {
                if (!m_SurveyAnswers.TryGetValue(surveyAnswer.Payload.NodeId, out var previousSurveyAnswer) ||
                    !previousSurveyAnswer.Equals(surveyAnswer.Payload))
                {
                    m_SurveyAnswers[surveyAnswer.Payload.NodeId] = surveyAnswer.Payload;
                    m_LastChangeInSurvey = Stopwatch.GetTimestamp();
                    UpdateSurveyAnalysis();
                }
            }
            return PreProcessResult.Stop();
        }

        /// <summary>
        /// Delegate called when the desired cluster topology changes.
        /// </summary>
        void ClusterTopologyUpdated()
        {
            lock (m_Lock)
            {
                m_TopologyEntries.Clear();
                m_TopologyEntries.AddRange(m_UpdatedClusterTopology.Entries);

                // Check if there wouldn't be old survey entries (for nodes that are not part of the new updated
                // topology).
                bool surveyAnalysisNeedsUpdate = false;
                foreach (var surveyEntryNodeId in m_SurveyAnswers.Keys.ToList())
                {
                    ClusterTopologyEntry? topologyEntry = null;
                    foreach (var currentTopologyEntry in m_TopologyEntries)
                    {
                        if (currentTopologyEntry.NodeId == surveyEntryNodeId)
                        {
                            topologyEntry = currentTopologyEntry;
                            break;
                        }
                    }

                    if (topologyEntry is not {NodeRole: (NodeRole.Repeater or NodeRole.Backup)})
                    {
                        m_SurveyAnswers.Remove(surveyEntryNodeId);
                        surveyAnalysisNeedsUpdate = true;
                    }
                }

                if (surveyAnalysisNeedsUpdate)
                {
                    m_LastChangeInSurvey = Stopwatch.GetTimestamp();
                    UpdateSurveyAnalysis();
                }
            }
        }

        /// <summary>
        /// Updates the member variables derived from the survey responses.
        /// </summary>
        void UpdateSurveyAnalysis()
        {
            Debug.Assert(Monitor.IsEntered(m_Lock)); // Caller should have locked it for us
            if (!m_HasResponseFromAllRepeaters.WaitOne(0))
            {
                bool hasAnswerFromAllRepeaters = true;
                foreach (var topologyEntry in m_TopologyEntries)
                {
                    if (topologyEntry.NodeRole is not (NodeRole.Repeater or NodeRole.Backup))
                    {
                        continue;
                    }

                    if (!m_SurveyAnswers.ContainsKey(topologyEntry.NodeId))
                    {
                        hasAnswerFromAllRepeaters = false;
                        break;
                    }
                }

                if (hasAnswerFromAllRepeaters)
                {
                    m_HasResponseFromAllRepeaters.Set();
                }
            }

            m_SurveyMinLastReceivedFrameIndex = m_SurveyAnswers.Values.Min(a => a.LastReceivedFrameIndex);
            m_SurveyMaxLastReceivedFrameIndex = m_SurveyAnswers.Values.Max(a => a.LastReceivedFrameIndex);
        }

        /// <summary>
        /// Object to use to send or receive datagrams.
        /// </summary>
        readonly IUdpAgent m_UdpAgent;
        /// <summary>
        /// Updated topology of the cluster (to which we registered for change events so that the PlaceHolder can know
        /// of changes to the topology while its running).
        /// </summary>
        readonly ClusterTopology m_UpdatedClusterTopology;
        /// <summary>
        /// Maximum amount of frame data that can be sent with each <see cref="FrameData"/> through
        /// <see cref="UdpAgent"/>.
        /// </summary>
        readonly int m_MaxDataPerMessage;
        /// <summary>
        /// Thread that perform the work of this class.
        /// </summary>
        Thread m_WorkThread;
        /// <summary>
        /// Indicate if <see cref="m_WorkThread"/> should keep on running.
        /// </summary>
        volatile bool m_WorkThreadSpinning = true;
        /// <summary>
        /// Have we received a <see cref="RepeatersSurveyAnswer"/> from all repeaters (or backups)?
        /// </summary>
        ManualResetEvent m_HasResponseFromAllRepeaters = new(false);

        /// <summary>
        /// Object used to synchronize access to the member variables below.
        /// </summary>
        object m_Lock = new();
        /// <summary>
        /// Latest survey answer from each node
        /// </summary>
        Dictionary<byte, RepeatersSurveyAnswer> m_SurveyAnswers = new();
        /// <summary>
        /// <see cref="Stopwatch.GetTimestamp"/> of last update to <see cref="m_SurveyAnswers"/>.
        /// </summary>
        long m_LastChangeInSurvey;
        /// <summary>
        /// Minimum <see cref="RepeatersSurveyAnswer.LastReceivedFrameIndex"/> in <see cref="m_SurveyAnswers"/>.
        /// </summary>
        ulong m_SurveyMinLastReceivedFrameIndex = ulong.MaxValue;
        /// <summary>
        /// Maximum <see cref="RepeatersSurveyAnswer.LastReceivedFrameIndex"/> in <see cref="m_SurveyAnswers"/>.
        /// </summary>
        ulong m_SurveyMaxLastReceivedFrameIndex = ulong.MaxValue;
        /// <summary>
        /// Local copy (that we know will not change while <see cref="m_Lock"/> is locked) of the cluster topology we
        /// are targeting.
        /// </summary>
        List<ClusterTopologyEntry> m_TopologyEntries = new();
        /// <summary>
        /// Previously received <see cref="FrameData"/> that have been reassembled from repeaters in the cluster.
        /// </summary>
        Dictionary<ulong, FrameDataBuffer> m_FrameDataHistory = new();

        /// <summary>
        /// Interval between re-run of the repeaters survey.
        /// </summary>
        static readonly TimeSpan k_SurveyInterval = TimeSpan.FromMilliseconds(50);
        /// <summary>
        /// Timespan allowed to fetch <see cref="FrameData"/> from other repeaters before re-doing a survey of the
        /// current state.
        /// </summary>
        static readonly TimeSpan k_FetchFramesTimespan = TimeSpan.FromMilliseconds(250);
        /// <summary>
        /// Timespan allowed to fetch <see cref="FrameData"/> from a single repeater
        /// </summary>
        static readonly TimeSpan k_FetchFrameDataMaxIdleTime = TimeSpan.FromMilliseconds(25);
        /// <summary>
        /// <see cref="MessageType"/> that <see cref="m_UdpAgent"/> must process upon reception.
        /// </summary>
        static MessageType[] s_ReceiveMessageTypes = {MessageType.RetransmitFrameData,
            MessageType.RepeaterWaitingToStartFrame, MessageType.RepeatersSurveyAnswer,
            MessageType.RetransmittedReceivedFrameData};
    }
}
