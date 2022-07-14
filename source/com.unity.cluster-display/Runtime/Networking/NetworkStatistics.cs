using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Statistics about a type of message sent over the network.
    /// </summary>
    struct MessageTypeStatistics
    {
        /// <summary>
        /// How many messages have been received and minimally parsed.
        /// </summary>
        /// <remarks>Unparsed messages (immediately discarded from their type) are not counted.</remarks>
        public long Received;

        /// <summary>
        /// How many messages have been sent.
        /// </summary>
        public long Sent;

        /// <summary>
        /// How many of the sent messages were retransmissions of previously sent messages.
        /// </summary>
        public long SentRepeat;
    }

    /// <summary>
    /// Statistics snapshot as returned by <see cref="NetworkStatistics"/>.
    /// </summary>
    class NetworkStatisticsSnapshot
    {
        /// <summary>
        /// How much time are covered by those statistics.
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// Per message type statistics
        /// </summary>
        public Dictionary<MessageType, MessageTypeStatistics> TypeStatistics { get; set; } = new();

        /// <summary>
        /// Count RetransmitFrameData sent because a detected break in the datagram sequence.
        /// </summary>
        public long RetransmitFrameDataSequence { get; set; }

        /// <summary>
        /// Count RetransmitFrameData sent because of a too long gap detected without any new data received.
        /// </summary>
        public long RetransmitFrameDataIdle { get; set; }
    }

    /// <summary>
    /// Various statistics about networking operations.
    /// </summary>
    class NetworkStatistics
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public NetworkStatistics()
        {
            m_CurrentInterval.IntervalStart = Stopwatch.GetTimestamp();
            m_Sum.IntervalStart = m_CurrentInterval.IntervalStart;

            // Add 60 entries to m_History, so with entries of 1 second history will contain for up to 1 minute of data.
            int entryCount = 60;
            for (int i = 0; i < entryCount; ++i)
            {
                var entryStatistics = new IntervalStatistics();
                entryStatistics.IntervalStart = m_CurrentInterval.IntervalStart;
                m_History.Enqueue(entryStatistics);
            }
        }

        /// <summary>
        /// Indicate a message was received.
        /// </summary>
        /// <param name="type">Type of the message</param>
        /// <remarks>Can be called from any thread.</remarks>
        public void MessageReceived(MessageType type)
        {
            m_CurrentInterval.MessageReceived(type);
        }

        /// <summary>
        /// Indicate a message was sent.
        /// </summary>
        /// <param name="type">Type of the message.</param>
        /// <remarks>Can be called from any thread.</remarks>
        public void MessageSent(MessageType type)
        {
            m_CurrentInterval.MessageSent(type);
        }

        /// <summary>
        /// Indicate a sent message was a repeat.
        /// </summary>
        /// <param name="type">Type of the message.</param>
        /// <remarks>Can be called from any thread.</remarks>
        public void SentMessageWasRepeat(MessageType type)
        {
            m_CurrentInterval.SentMessageWasRepeat(type);
        }

        /// <summary>
        /// Indicate a RetransmitFrameData was sent because a detected break in the datagram sequence.
        /// </summary>
        /// <param name="number">Number of RetransmitFrameData sent.</param>
        public void RetransmitFrameDataSequence(int number)
        {
            m_CurrentInterval.RetransmitFrameDataSequence(number);
        }

        /// <summary>
        /// Indicate a RetransmitFrameData was sent because of a too long gap detected without any new data received.
        /// </summary>
        /// <param name="number">Number of RetransmitFrameData sent.</param>
        public void RetransmitFrameDataIdle(int number)
        {
            m_CurrentInterval.RetransmitFrameDataIdle(number);
        }

        /// <summary>
        /// Compute a snapshot of the statistics.
        /// </summary>
        /// <returns>The newly computed snapshot.</returns>
        /// <remarks>Not calling this method often enough might lead to <see cref="NetworkStatisticsSnapshot.Interval"/>
        /// with more variation.</remarks>
        /// <remarks>Can be called from any thread but only one will execute at the time.</remarks>
        public NetworkStatisticsSnapshot ComputeSnapshot()
        {
            var ret = new NetworkStatisticsSnapshot();

            lock (m_ComputeSnapshotLock)
            {
                long now = Stopwatch.GetTimestamp();
                if (m_CurrentInterval.IntervalStart + m_IntervalDuration < now)
                {
                    // m_CurrentInterval is being filled for long enough, transfer its content to the sum and history.
                    var oldHistoryEntry = m_History.Dequeue();
                    m_Sum.SubtractContribution(oldHistoryEntry);
                    var newHistoryEntry = oldHistoryEntry;
                    newHistoryEntry.IntervalStart = now;
                    m_CurrentInterval.ContributeTo(m_Sum, newHistoryEntry);
                    m_CurrentInterval.IntervalStart = now;
                    m_Sum.IntervalStart = m_History.Peek().IntervalStart;
                    m_History.Enqueue(newHistoryEntry);
                }
                IntervalStatistics.FillSnapshot(m_Sum, m_CurrentInterval, ret);
            }

            return ret;
        }

        /// <summary>
        /// Hold statistics about a time interval.
        /// </summary>
        class IntervalStatistics
        {
            public IntervalStatistics()
            {
                var messageTypes =
                    Enum.GetValues(typeof(MessageType)).Cast<MessageType>().OrderBy( mt => (int)mt );
                m_Stats = new MessageTypeStatistics[(int)messageTypes.Last() + 1];
            }

            /// <summary>
            /// <see cref="Stopwatch.GetTimestamp"/> of when the first information was added to the interval.
            /// </summary>
            public long IntervalStart { get; set; }

            /// <summary>
            /// Indicate a message was received.
            /// </summary>
            /// <param name="type">Type of the message.</param>
            public void MessageReceived(MessageType type)
            {
                Interlocked.Increment(ref m_Stats[(int)type].Received);
            }

            /// <summary>
            /// Indicate a message was sent.
            /// </summary>
            /// <param name="type">Type of the message.</param>
            public void MessageSent(MessageType type)
            {
                Interlocked.Increment(ref m_Stats[(int)type].Sent);
            }

            /// <summary>
            /// Indicate a sent message was a repeat.
            /// </summary>
            /// <param name="type">Type of the message.</param>
            public void SentMessageWasRepeat(MessageType type)
            {
                Interlocked.Increment(ref m_Stats[(int)type].SentRepeat);
            }

            /// <summary>
            /// Indicate a RetransmitFrameData was sent because a detected break in the datagram sequence.
            /// </summary>
            /// <param name="number">Number of RetransmitFrameData sent.</param>
            public void RetransmitFrameDataSequence(int number)
            {
                Interlocked.Add(ref m_RetransmitFrameDataSequence, number);
            }

            /// <summary>
            /// Indicate a RetransmitFrameData was sent because of a too long gap detected without any new data received.
            /// </summary>
            /// <param name="number">Number of RetransmitFrameData sent.</param>
            public void RetransmitFrameDataIdle(int number)
            {
                Interlocked.Add(ref m_RetransmitFrameDataIdle, number);
            }

            /// <summary>
            /// Accumulate statistics of <c>this</c> to another <see cref="IntervalStatistics"/>.
            /// </summary>
            /// <param name="accumulator">Object into which statistics are accumulated.</param>
            /// <param name="contribution">What was added to <paramref name="accumulator"/>.</param>
            public void ContributeTo(IntervalStatistics accumulator, IntervalStatistics contribution)
            {
                for (int messageIndex = 0; messageIndex < m_Stats.Length; ++messageIndex)
                {
                    long receivedCount = Interlocked.Exchange(ref m_Stats[messageIndex].Received, 0);
                    contribution.m_Stats[messageIndex].Received = receivedCount;
                    Interlocked.Add(ref accumulator.m_Stats[messageIndex].Received, receivedCount);

                    long sentCount = Interlocked.Exchange(ref m_Stats[messageIndex].Sent, 0);
                    contribution.m_Stats[messageIndex].Sent = sentCount;
                    Interlocked.Add(ref accumulator.m_Stats[messageIndex].Sent, sentCount);

                    long sentRepeatCount = Interlocked.Exchange(ref m_Stats[messageIndex].SentRepeat, 0);
                    contribution.m_Stats[messageIndex].SentRepeat = sentRepeatCount;
                    Interlocked.Add(ref accumulator.m_Stats[messageIndex].SentRepeat, sentRepeatCount);
                }

                long sequenceCount = Interlocked.Exchange(ref m_RetransmitFrameDataSequence, 0);
                contribution.m_RetransmitFrameDataSequence = sequenceCount;
                Interlocked.Add(ref accumulator.m_RetransmitFrameDataSequence, sequenceCount);

                long idleCount = Interlocked.Exchange(ref m_RetransmitFrameDataIdle, 0);
                contribution.m_RetransmitFrameDataIdle = idleCount;
                Interlocked.Add(ref accumulator.m_RetransmitFrameDataIdle, idleCount);
            }

            /// <summary>
            /// Subtract a contribution previously done with <see cref="ContributeTo"/>.
            /// </summary>
            /// <param name="contribution">Contribution as filled by <see cref="ContributeTo"/>.</param>
            public void SubtractContribution(IntervalStatistics contribution)
            {
                for (int messageIndex = 0; messageIndex < m_Stats.Length; ++messageIndex)
                {
                    Interlocked.Add(ref m_Stats[messageIndex].Received, -contribution.m_Stats[messageIndex].Received);
                    Interlocked.Add(ref m_Stats[messageIndex].Sent, -contribution.m_Stats[messageIndex].Sent);
                    Interlocked.Add(ref m_Stats[messageIndex].SentRepeat, -contribution.m_Stats[messageIndex].SentRepeat);
                }

                Interlocked.Add(ref m_RetransmitFrameDataSequence, -contribution.m_RetransmitFrameDataSequence);
                Interlocked.Add(ref m_RetransmitFrameDataIdle, -contribution.m_RetransmitFrameDataIdle);
            }

            /// <summary>
            /// Fill a <see cref="NetworkStatisticsSnapshot"/> from two <see cref="IntervalStatistics"/>.
            /// </summary>
            /// <param name="interval1">First statistics interval (the oldest one).</param>
            /// <param name="interval2">First statistics interval (the most recent one).</param>
            /// <param name="toFill">Snapshot to be filled.</param>
            public static void FillSnapshot(IntervalStatistics interval1, IntervalStatistics interval2,
                NetworkStatisticsSnapshot toFill)
            {
                Debug.Assert(interval1.IntervalStart <= interval2.IntervalStart);
                toFill.Interval =
                    TimeSpan.FromSeconds((double)(Stopwatch.GetTimestamp() - interval1.IntervalStart) / Stopwatch.Frequency);

                Debug.Assert(interval1.m_Stats.Length == interval2.m_Stats.Length);
                for (int messageTypeIndex = 0; messageTypeIndex < interval1.m_Stats.Length; ++messageTypeIndex)
                {
                    var sumRecv = Interlocked.Read(ref interval1.m_Stats[messageTypeIndex].Received) +
                        Interlocked.Read(ref interval2.m_Stats[messageTypeIndex].Received);
                    var sumSent = Interlocked.Read(ref interval1.m_Stats[messageTypeIndex].Sent) +
                        Interlocked.Read(ref interval2.m_Stats[messageTypeIndex].Sent);
                    var sumSentRepeat = Interlocked.Read(ref interval1.m_Stats[messageTypeIndex].SentRepeat) +
                        Interlocked.Read(ref interval2.m_Stats[messageTypeIndex].SentRepeat);
                    if (sumRecv + sumSent + sumSentRepeat > 0)
                    {
                        toFill.TypeStatistics[(MessageType)messageTypeIndex] =
                            new MessageTypeStatistics {Received = sumRecv, Sent = sumSent, SentRepeat = sumSentRepeat};
                    }
                }

                toFill.RetransmitFrameDataSequence = Interlocked.Read(ref interval1.m_RetransmitFrameDataSequence) +
                    Interlocked.Read(ref interval2.m_RetransmitFrameDataSequence);
                toFill.RetransmitFrameDataIdle = Interlocked.Read(ref interval1.m_RetransmitFrameDataIdle) +
                    Interlocked.Read(ref interval2.m_RetransmitFrameDataIdle);
            }

            /// <summary>
            /// Per message type statistics
            /// </summary>
            MessageTypeStatistics[] m_Stats;
            /// <summary>
            /// Count RetransmitFrameData sent because a detected break in the datagram sequence.
            /// </summary>
            long m_RetransmitFrameDataSequence;
            /// <summary>
            /// Count RetransmitFrameData sent because of a too long gap detected without any new data received.
            /// </summary>
            long m_RetransmitFrameDataIdle;
        }

        /// <summary>
        /// Duration (in Stopwatch ticks) of a time interval.
        /// </summary>
        long m_IntervalDuration = Stopwatch.Frequency;
        /// <summary>
        /// Current IntervalStatistics (to which new statistics are accumulated).
        /// </summary>
        IntervalStatistics m_CurrentInterval = new();

        /// <summary>
        /// Object to synchronize access to variables below.
        /// </summary>
        object m_ComputeSnapshotLock = new object();
        /// <summary>
        /// History of IntervalStatistics that have been summed to <see cref="m_Sum"/>.
        /// </summary>
        /// <remarks>Must lock <see cref="m_ComputeSnapshotLock"/> to access this variable.</remarks>
        Queue<IntervalStatistics> m_History = new();
        /// <summary>
        /// Sum of all the statistics in <see cref="m_History"/>.
        /// </summary>
        /// <remarks>Must lock <see cref="m_ComputeSnapshotLock"/> to access this variable.</remarks>
        IntervalStatistics m_Sum = new();
    }
}
