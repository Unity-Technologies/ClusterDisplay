using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Unity.Collections;
using UnityEngine.Pool;
using Utils;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Class responsible for assembling multiple <see cref="FrameData"/> into a single complete
    /// <see cref="ReceivedMessage{FrameData}"/>.
    /// </summary>
    /// <remarks>This class also takes care of requesting retransmissions when lost messages and ignoring duplicate ones
    /// (already asked by another repeater?).</remarks>
    /// <remarks>Food for thought to improve implementation.  If we see we are getting cases of repeaters all asking for
    /// the same thing then we could probably improve this class to avoid this by sniffing other repeaters retransmission
    /// request and avoiding to ask for the same thing.</remarks>
    class FrameDataAssembler: IDisposable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="udpAgent">Object responsible for network access from which we are receiving fragments of the
        /// whole frame data (and over which we ask for retransmission if packet loss are suspected).</param>
        /// <param name="orderedReception">Assumes datagrams ordering is preserved along the complete transmission chain.
        /// This means that the <see cref="FrameDataAssembler"/> will immediately ask for a retransmission as soon as a
        /// gap is detected in the datagram sequence.</param>
        /// <param name="firstFrameData">Some time a <see cref="NodeState"/> might have missed packet and only realize
        /// it once it receives a <see cref="FrameData"/>.  The goal of this member variable is to allow processing the
        /// <see cref="FrameData"/> as if it would have never been removed from the ReceivedMessage queue.</param>
        public FrameDataAssembler(IUdpAgent udpAgent, bool orderedReception,
            ReceivedMessage<FrameData> firstFrameData = null)
        {
            if (!udpAgent.ReceivedMessageTypes.Contains(MessageType.FrameData))
            {
                throw new ArgumentException("UdpAgent does not support receiving required MessageType.FrameData");
            }

            m_CurrentPartialFrame = new PartialFrameData(orderedReception);
            m_NextPartialFrame = new PartialFrameData(orderedReception);

            CreateNewNativeExtraDataPool();
            m_GetNewNativeExtraDataDelegate = GetNewNativeExtraData;

            UdpAgent = udpAgent;

            if (firstFrameData != null && firstFrameData.ExtraData != null)
            {
                Debug.Assert(m_CurrentPartialFrame.FrameIndex == ulong.MaxValue);
                m_CurrentPartialFrame.FrameIndex = firstFrameData.Payload.FrameIndex;
                m_CurrentPartialFrame.ConsumeReceivedData(firstFrameData, m_GetNewNativeExtraDataDelegate);
                SendPendingRetransmissionRequests(m_CurrentPartialFrame.PendingRetransmissions);
            }

            UdpAgent.AddPreProcess(UdpAgentPreProcessPriorityTable.frameDataProcessing, PreProcessReceivedMessage);

            m_FrameCompletionMonitorThreadRunning = true;
            m_FrameCompletionMonitorActive = new(false);
            m_FrameCompletionMonitorThread = new(FrameCompletionMonitorThreadFunc);
            m_FrameCompletionMonitorThread.Start();
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            // Stop receiving callbacks about received datagrams
            if (UdpAgent != null)
            {
                UdpAgent.RemovePreProcess(PreProcessReceivedMessage);
            }

            // Stop thread monitoring received data
            m_FrameCompletionMonitorThreadRunning = false;
            m_FrameCompletionMonitorActive?.Set();
            m_FrameCompletionMonitorThread?.Join();
        }

        /// <summary>
        /// Network access object from which we are receiving fragments of the whole frame data (and over which we ask
        /// for retransmission if packet loss are suspected).
        /// </summary>
        public IUdpAgent UdpAgent { get; }

        /// <summary>
        /// How long must a partial frame data remain before we request retransmission of the missing parts.
        /// </summary>
        public TimeSpan FrameCompletionDelay
        {
            get => TimeSpan.FromSeconds(m_FrameCompletionDelayTicks / (double)Stopwatch.Frequency);
            set => m_FrameCompletionDelayTicks = (long)(value.TotalSeconds * Stopwatch.Frequency);
        }

        /// <summary>
        /// Method to be called by the user of the FrameAssembler to inform it that it will need the data for the given
        /// frame index and is about to block its execution until it becomes available.
        /// </summary>
        /// <param name="frameIndex">Index of the frame</param>
        /// <remarks>This will trigger sending retransmission request at regular interval until the frame is received.
        /// </remarks>
        public void WillNeedFrame(ulong frameIndex)
        {
            lock (m_ThisLock)
            {
                if (m_CurrentPartialFrame.FrameIndex == ulong.MaxValue)
                {
                    m_CurrentPartialFrame.FrameIndex = frameIndex;
                }
                if (frameIndex == m_CurrentPartialFrame.FrameIndex)
                {
                    m_FrameCompletionMonitorActive.Set();
                }
                else if (frameIndex + 1 != m_CurrentPartialFrame.FrameIndex) // Happens when reception of frameIndex is
                {                                                            // completed before this method is called.
                    Debug.LogWarning($"FrameDataAssembler is being informed its user needs frame {frameIndex}, " +
                        $"however the current frame is {m_CurrentPartialFrame.FrameIndex}.");
                }
            }
        }

        /// <summary>
        /// Preprocess a received message and assemble complete FrameData from them.
        /// </summary>
        /// <param name="received">Received <see cref="ReceivedMessageBase"/> to preprocess.</param>
        /// <returns>Summary of what happened during the pre-processing.</returns>
        PreProcessResult PreProcessReceivedMessage(ReceivedMessageBase received)
        {
            // We are only interested in FrameData we receive, everything else should simply pass through
            if (received.Type != MessageType.FrameData)
            {
                return PreProcessResult.PassThrough();
            }

            var receivedFrameData = (ReceivedMessage<FrameData>)received;
            if (received.ExtraData == null)
            {
                // FrameData with no extra data, it does not provide any useful information, let's just discard it.
                return PreProcessResult.Stop();
            }

            lock (m_ThisLock)
            {
                if (m_CurrentPartialFrame.FrameIndex == ulong.MaxValue) // Special case for first frame received
                {
                    m_CurrentPartialFrame.FrameIndex = receivedFrameData.Payload.FrameIndex;
                }
                if (receivedFrameData.Payload.FrameIndex == m_CurrentPartialFrame.FrameIndex)
                {
                    m_CurrentPartialFrame.ConsumeReceivedData(receivedFrameData, m_GetNewNativeExtraDataDelegate);
                    SendPendingRetransmissionRequests(m_CurrentPartialFrame.PendingRetransmissions);
                }
                else if (receivedFrameData.Payload.FrameIndex == m_CurrentPartialFrame.FrameIndex + 1)
                {
                    // Looks like we are receiving data for the next frame.  Packets ordering issue?  Anyhow, save
                    // it so that we don't have to ask to retransmit it when m_CurrentPartialFrame is completed.
                    m_NextPartialFrame.FrameIndex = receivedFrameData.Payload.FrameIndex;
                    m_NextPartialFrame.ConsumeReceivedData(receivedFrameData, m_GetNewNativeExtraDataDelegate);
                    SendPendingRetransmissionRequests(m_NextPartialFrame.PendingRetransmissions);
                }
                // We could in theory receive messages for older or newer frames, but we cannot really do anything
                // for them, so continue waiting for m_CurrentPartialFrame to be completed...

                // Always deliver FrameData in order, so the only one we can try to deliver is m_CurrentPartialFrame.
                // Always check for IsFrameDataComplete even if the message did not have any useful contribution as
                // previous received message might have completed m_CurrentPartialFrame while m_NextPartialFrame was
                // already complete and waiting for delivery.
                if (m_CurrentPartialFrame.IsFrameDataComplete)
                {
                    // CurrentPartialFrame is not partial anymore, so update received so that it contains the new
                    // complete extra data.
                    receivedFrameData.Payload = new FrameData()
                        {
                            FrameIndex = m_CurrentPartialFrame.FrameIndex,
                            DataLength = m_CurrentPartialFrame.DataLength,
                            DatagramIndex = 0,
                            DatagramDataOffset = 0
                        };
                    receivedFrameData.AdoptExtraData(m_CurrentPartialFrame.ConsumeCompletedFrame());

                    // Prepare m_CurrentPartialFrame to receive next frame
                    if (m_NextPartialFrame.FrameIndex == m_CurrentPartialFrame.FrameIndex + 1)
                    {
                        (m_CurrentPartialFrame, m_NextPartialFrame) = (m_NextPartialFrame, m_CurrentPartialFrame);
                        m_NextPartialFrame.FrameIndex = ulong.MaxValue;
                    }
                    else
                    {
                        ++m_CurrentPartialFrame.FrameIndex;
                    }

                    // Deactivate FrameCompletionMonitor until our user inform us that the next frame is needed (this
                    // way the FrameCompletionMonitorThread will not constantly wake up to discover it has nothing
                    // to do).
                    m_FrameCompletionMonitorActive.Reset();

                    // Done
                    return PreProcessResult.PassThrough();
                }
            }

            return PreProcessResult.Stop();
        }

        /// <summary>
        /// Method to be called after increasing m_NativeExtraDataPoolAllocSize to make the NativeExtraData allocated
        /// by the pool larger.
        /// </summary>
        /// <remarks>Assumes caller has already locked <see cref="m_ThisLock"/>.</remarks>
        void CreateNewNativeExtraDataPool()
        {
            m_NativeExtraDataPool?.Dispose();
            m_NativeExtraDataPool = new(() => new NativeExtraData(this, m_NativeExtraDataPoolAllocSize));
        }

        /// <summary>
        /// Returns a new <see cref="NativeExtraData"/> that can store at least the requested amount of content.
        /// </summary>
        /// <param name="length">Length of data to fit.</param>
        /// <returns>A new (or recycled from the pool) <see cref="NativeExtraData"/>.</returns>
        NativeExtraData GetNewNativeExtraData(int length)
        {
            lock (m_ThisLock)
            {
                if (length > m_NativeExtraDataPoolAllocSize)
                {
                    do
                    {
                        m_NativeExtraDataPoolAllocSize *= 2;
                    } while (length > m_NativeExtraDataPoolAllocSize);

                    CreateNewNativeExtraDataPool();
                }
                return m_NativeExtraDataPool.Get();
            }
        }

        /// <summary>
        /// Return a <see cref="NativeExtraData"/> to a free pool so that can be reused for future FrameData.
        /// </summary>
        /// <param name="toReturn"><see cref="NativeExtraData"/> to return to a free pool.</param>
        void ReturnNativeExtraData(NativeExtraData toReturn)
        {
            lock (m_ThisLock)
            {
                if (toReturn.RawArray.Length >= m_NativeExtraDataPoolAllocSize)
                {
                    m_NativeExtraDataPool.Release(toReturn);
                }
                // else, this is an old NativeDataArray for smaller frames, forget about it, a new larger one will be
                // allocated next time GetNewNativeExtraData is called.
            }
        }

        /// <summary>
        /// Send retransmission requests waiting to be transmitted.
        /// </summary>
        /// <param name="pendingQueue">Queue of pending retransmissions.</param>
        void SendPendingRetransmissionRequests(Queue<RetransmitFrameData> pendingQueue)
        {
            int count = 0;
            while (pendingQueue.Count > 0)
            {
                UdpAgent.SendMessage(MessageType.RetransmitFrameData, pendingQueue.Dequeue());
                ++count;
            }
            UdpAgent.Stats.RetransmitFrameDataSequence(count);
        }

        /// <summary>
        /// Main loop of <see cref="m_FrameCompletionMonitorThread"/> that when active (controlled through
        /// <see cref="m_FrameCompletionMonitorActive"/>) will check for frames with missing data every
        /// <see cref="FrameCompletionDelay"/>.
        /// </summary>
        void FrameCompletionMonitorThreadFunc()
        {
            long ticksToMillisecond = Stopwatch.Frequency / 1000;

            while (m_FrameCompletionMonitorThreadRunning)
            {
                // Wait for the thread to be active
                m_FrameCompletionMonitorActive.WaitOne();
                if (!m_FrameCompletionMonitorThreadRunning)
                {
                    break;
                }

                int sleepTime;
                lock (m_ThisLock)
                {
                    // We should have a current frame as the WillNeedFrame method should have set one if there wasn't
                    // already one set.
                    Debug.Assert(m_CurrentPartialFrame.FrameIndex != ulong.MaxValue);

                    long frameCompletionDelayTicks = m_FrameCompletionDelayTicks;
                    if (!m_CurrentPartialFrame.HasReceivedSomething)
                    {
                        // We never received a datagram for that frame, not a single one.  Chances are the emitter is not
                        // yet ready to send it.
                        // In theory, it is also possible that we missed all the datagrams of the frame, however, chances
                        // that either all the nodes received nothing at all or that only this node missed everything are
                        // quite low, so we increase frameCompletionDelayTicks a lot to avoid having every repeater
                        // hammering the emitter to repeat while it is simply not yet ready...
                        frameCompletionDelayTicks *= 10;
                        if (m_CurrentPartialFrame.LastReceivedDatagramTimestamp == 0)
                        {
                            m_CurrentPartialFrame.UpdateLastReceivedDatagramTimestampToNow();
                        }
                    }

                    long timestampNow = Stopwatch.GetTimestamp();
                    if (!m_CurrentPartialFrame.IsFrameDataComplete &&
                        m_CurrentPartialFrame.LastReceivedDatagramTimestamp + frameCompletionDelayTicks <= timestampNow) // Don't ask for retransmission if we are processing an inbound datagram
                    {
                        // It's being too long since we received some data for m_CurrentPartialFrame, ask for the
                        // missing parts.
                        m_CurrentPartialFrame.AskForRetransmission(UdpAgent);
                        sleepTime = (int)(m_FrameCompletionDelayTicks / ticksToMillisecond);
                        m_CurrentPartialFrame.UpdateLastReceivedDatagramTimestampToNow();
                    }
                    else
                    {
                        // Still have some time to wait.
                        var elapsedTicks = timestampNow - m_CurrentPartialFrame.LastReceivedDatagramTimestamp;
                        sleepTime = (int)((m_FrameCompletionDelayTicks - elapsedTicks)/ ticksToMillisecond);
                    }
                    sleepTime = Math.Max(sleepTime, 1); // Sleep at least 1 millisecond before checkin again
                }

                // Wait a little bit before checking again
                Thread.Sleep(sleepTime);
            }
        }

        /// <summary>
        /// Object to be locked when code needs to serialize access to member variables.
        /// </summary>
        readonly object m_ThisLock = new object();
        /// <summary>
        /// Data about the frame we are currently rebuilding a complete FrameData for.
        /// </summary>
        /// <remarks>Should lock <see cref="m_ThisLock"/> before accessing it.</remarks>
        PartialFrameData m_CurrentPartialFrame;
        /// <summary>
        /// In case some packets are lost while retransmitting m_CurrentPartialFrame, this is used to store the data of
        /// the next frame.
        /// </summary>
        /// <remarks>Normally we shouldn't really need it as the emitter should somehow wait that everyone is ready to
        /// present m_CurrentPartialFrame before starting to send the next one.  But let's play safe.</remarks>
        /// <remarks>Should lock <see cref="m_ThisLock"/> before accessing it.</remarks>
        PartialFrameData m_NextPartialFrame;
        /// <summary>
        /// Pool that contains old NativeExtraData that can be re-used.
        /// </summary>
        /// <remarks>Should lock <see cref="m_ThisLock"/> before accessing it.  Using
        /// <see cref="ConcurrentObjectPool{T}"/> wouldn't be enough since we need to ensure some coherency with the
        /// other member variables (in particular <see cref="m_NativeExtraDataPoolAllocSize"/>).</remarks>
        ObjectPool<NativeExtraData> m_NativeExtraDataPool;
        /// <summary>
        /// How many bytes are allocated in each of the NativeExtraData in <see cref="m_NativeExtraDataPool"/>.
        /// </summary>
        /// <remarks>Should lock <see cref="m_ThisLock"/> before accessing it.</remarks>
        int m_NativeExtraDataPoolAllocSize = ushort.MaxValue;
        /// <summary>
        /// <see cref="Func{T0, TResult}"/> set once to avoid constant heap allocation to create one from
        /// <see cref="GetNewNativeExtraData"/> when calling <see cref="PartialFrameData.ConsumeReceivedData"/>.
        /// </summary>
        readonly Func<int, NativeExtraData> m_GetNewNativeExtraDataDelegate;

        /// <summary>
        /// Thread that monitors for frame data reception that appear to take too long and ask for missing packets
        /// retransmission.
        /// </summary>
        Thread m_FrameCompletionMonitorThread;
        /// <summary>
        /// Is FrameCompletionMonitorThread active?
        /// </summary>
        ManualResetEvent m_FrameCompletionMonitorActive;
        /// <summary>
        /// Indicate if m_FrameCompletionMonitorThread should keep on running
        /// </summary>
        volatile bool m_FrameCompletionMonitorThreadRunning;
        /// <summary>
        /// How long (int Stopwatch ticks) must a partial frame data remain before we request retransmission of the
        /// missing parts.
        /// </summary>
        /// <remarks>Default value of 4 milliseconds.  On a good network with fair system usage condition, sending a
        /// packet and getting back the answer normally takes below 2 milliseconds.  So waiting 4 milliseconds should
        /// allow plenty of time to process the request without waiting too long increasing time to get a complete
        /// frame.</remarks>
        long m_FrameCompletionDelayTicks = (4 * Stopwatch.Frequency) / 1000;

        /// <summary>
        /// IReceivedMessageData that is used to represent the extra data associated to
        /// <see cref="ReceivedMessage{FrameData}"/> generated by FrameDataAssembler.
        /// </summary>
        class NativeExtraData : IReceivedMessageData
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="owner">Owning <see cref="FrameDataAssembler"/> that contains the pool to which we should be
            /// returned to.</param>
            /// <param name="size">Size in bytes of the <see cref="NativeArray{Byte}"/> contained in this class.</param>
            public NativeExtraData(FrameDataAssembler owner, int size)
            {
                m_Owner = owner;
                m_NativeArray = new NativeArray<byte>(size, Allocator.Persistent);
            }

            /// <summary>
            /// Raw access to the <see cref="NativeArray{Byte}"/> contained in this <see cref="NativeExtraData"/>.
            /// </summary>
            public NativeArray<byte> RawArray => m_NativeArray;

            /// <summary>
            /// Set the actual length of <see cref="RawArray"/> that is use by the extra data.
            /// </summary>
            /// <param name="extraDataLength"></param>
            /// <exception cref="ArgumentOutOfRangeException"></exception>
            public void SetUsedLength(int extraDataLength)
            {
                if (extraDataLength > m_NativeArray.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(extraDataLength),
                        "dataLength > size of the ManagedExtraData.");
                }
                m_ExtraDataLength = extraDataLength;
                m_ManagedArray = null;
            }

            public ReceivedMessageDataFormat PreferredFormat => ReceivedMessageDataFormat.NativeArray;

            public int Length => m_ExtraDataLength;

            public void AsManagedArray(out byte[] array, out int dataStart, out int dataLength)
            {
                if (m_ManagedArray == null)
                {
                    m_ManagedArray = new byte[m_ExtraDataLength];
                    NativeArray<byte>.Copy(m_NativeArray, m_ManagedArray, m_ExtraDataLength);
                }

                array = m_ManagedArray;
                dataStart = 0;
                dataLength = m_ExtraDataLength;
            }

            public NativeArray<byte> AsNativeArray()
            {
                return m_NativeArray.GetSubArray(0, m_ExtraDataLength);
            }

            public void Release()
            {
                m_ExtraDataLength = 0;
                m_ManagedArray = null;
                m_Owner.ReturnNativeExtraData(this);
            }

            /// <summary>
            /// Owning <see cref="UdpAgent"/> that contains the pool to which we should be returned to.
            /// </summary>
            FrameDataAssembler m_Owner;
            /// <summary>
            /// Contains the data of this <see cref="NativeExtraData"/>.
            /// </summary>
            NativeArray<byte> m_NativeArray;
            /// <summary>
            /// Actual length of <see cref="m_NativeArray"/> that is use by the extra data.
            /// </summary>
            int m_ExtraDataLength;
            /// <summary>
            /// Cache for multiple calls to AsManagedArray.
            /// </summary>
            byte[] m_ManagedArray;
        }

        /// <summary>
        /// Information about a partial FrameData being reconstructed.
        /// </summary>
        class PartialFrameData
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="orderedReception">Assumes datagrams ordering is preserved along the complete transmission
            /// chain.  This means that the <see cref="PartialFrameData"/> will immediately ask for a retransmission as
            /// soon as a gap is detected in the datagram sequence.</param>
            public PartialFrameData(bool orderedReception)
            {
                m_OrderedReception = orderedReception;
            }

            /// <summary>
            /// Index of the frame this objects contains information about the data.
            /// </summary>
            public ulong FrameIndex { get; set; } = ulong.MaxValue;

            /// <summary>
            /// Process the received FrameData (copy extra data if needed or just ignore it if it is a duplicate).
            /// </summary>
            /// <param name="receivedFrameData">The received FrameData </param>
            /// <param name="extraDataProvider">Function called to get a new <see cref="NativeExtraData"/> that can
            /// store the requested amount of data.</param>
            /// <returns>Is the FrameData complete (all messages forming it received)?</returns>
            public void ConsumeReceivedData(ReceivedMessage<FrameData> receivedFrameData,
                Func<int, NativeExtraData> extraDataProvider)
            {
                Debug.Assert(receivedFrameData.Payload.FrameIndex == FrameIndex);
                Debug.Assert(receivedFrameData.ExtraData != null, "ConsumeReceivedData shouldn't be called if it has no extra data (nothing to consume)");

                // Skip messages we already processed in the past
                if (receivedFrameData.Payload.DatagramIndex < m_ReceivedDatagrams.Length &&
                    m_ReceivedDatagrams[receivedFrameData.Payload.DatagramIndex])
                {
                    return;
                }

                long preprocessStartTimestamp = Stopwatch.GetTimestamp();

                // Deal with the first message contributing to this FrameData
                if (m_ReceivedDatagramsCount == 0)
                {
                    if (m_ExtraData == null || receivedFrameData.Payload.DataLength > m_ExtraData.RawArray.Length)
                    {
                        m_ExtraData?.Release();
                        m_ExtraData = extraDataProvider(receivedFrameData.Payload.DataLength);
                    }
                    m_FrameDataBytesLeftToCopy = receivedFrameData.Payload.DataLength;
                    m_DataLength = receivedFrameData.Payload.DataLength;
                }

                // Validate last aspects of the received message
                int dataEndByte = receivedFrameData.Payload.DatagramDataOffset + receivedFrameData.ExtraData.Length;
                if ( dataEndByte > m_ExtraData.RawArray.Length)
                {
                    Debug.LogError($"Received an invalid packet, it contains data from byte " +
                        $"{receivedFrameData.Payload.DatagramDataOffset} to {dataEndByte} while the frame is supposed " +
                        $"to be {m_ExtraData.RawArray.Length} bytes long.");
                    return;
                }

                // Ensure m_ReceivedDatagrams is large enough to accommodate the new received datagram.  If not, grow
                // and snap to increments of 32 bits.
                if (receivedFrameData.Payload.DatagramIndex >= m_ReceivedDatagrams.Length)
                {
                    int newLength = receivedFrameData.Payload.DatagramIndex;
                    newLength &= ~0x1F;
                    newLength += 32;
                    var newBits = new bool[newLength];
                    m_ReceivedDatagrams.CopyTo(newBits, 0);
                    for (int i = m_ReceivedDatagrams.Length; i < newBits.Length; ++i)
                    {
                        newBits[i] = false;
                    }
                    m_ReceivedDatagrams = new BitArray(newBits);
                }
                Debug.Assert(receivedFrameData.Payload.DatagramIndex < m_ReceivedDatagrams.Length);

                // Copy the data
                switch (receivedFrameData.ExtraData.PreferredFormat)
                {
                    case ReceivedMessageDataFormat.ManagedArray:
                        receivedFrameData.ExtraData.AsManagedArray(out var bytes, out var startIndex, out var length);
                        NativeArray<byte>.Copy(bytes, startIndex, m_ExtraData.RawArray,
                            receivedFrameData.Payload.DatagramDataOffset, length);
                        break;
                    case ReceivedMessageDataFormat.NativeArray:
                    default:
                        NativeArray<byte>.Copy(receivedFrameData.ExtraData.AsNativeArray(), 0, m_ExtraData.RawArray,
                            receivedFrameData.Payload.DatagramDataOffset, receivedFrameData.ExtraData.Length);
                        break;
                }

                // Ideally datagrams should all be transmitted in sequence with no gap.  Any gap indicate a potentially
                // missing datagram.  I know, an ordering problem will cause a gap that will trigger an unnecessary
                // retransmission, but again, that should be the exception and shouldn't happen often and we prefer to
                // retransmit a little bit more to reduce latency (and so increase framerate).
                if (m_OrderedReception && receivedFrameData.Payload.DatagramIndex > m_LastReceivedDatagramIndex + 1)
                {
                    PendingRetransmissions.Enqueue(new()
                    {
                        FrameIndex = receivedFrameData.Payload.FrameIndex,
                        DatagramIndexIndexStart = m_LastReceivedDatagramIndex + 1,
                        DatagramIndexIndexEnd = receivedFrameData.Payload.DatagramIndex
                    });
                }

                // Update member variables
                m_ReceivedDatagrams[receivedFrameData.Payload.DatagramIndex] = true;
                ++m_ReceivedDatagramsCount;
                m_FrameDataBytesLeftToCopy -= receivedFrameData.ExtraData.Length;
                if (receivedFrameData.Payload.DatagramIndex > m_LastReceivedDatagramIndex)
                {
                    m_LastReceivedDatagramIndex = receivedFrameData.Payload.DatagramIndex;
                    m_LastReceivedDatagramDataEnd = receivedFrameData.Payload.DatagramDataOffset +
                        receivedFrameData.ExtraData.Length;
                }
                m_LastReceivedDatagramTimestamp = preprocessStartTimestamp;
            }

            /// <summary>
            /// Returns the assembled <see cref="NativeExtraData"/> for this completed frame and clear to prepare for
            /// next frame.
            /// </summary>
            public NativeExtraData ConsumeCompletedFrame()
            {
                m_ExtraData.SetUsedLength(m_DataLength);
                var ret = m_ExtraData;
                m_ExtraData = null;
                m_ReceivedDatagrams.SetAll(false);
                m_ReceivedDatagramsCount = 0;
                m_LastReceivedDatagramIndex = -1;
                m_LastReceivedDatagramDataEnd = 0;
                m_LastReceivedDatagramTimestamp = 0;
                return ret;
            }

            /// <summary>
            /// Send the necessary retransmissions so that we receive the missing parts of the frame data.
            /// </summary>
            /// <param name="udpAgent">Network access used to send the retransmission requests.</param>
            public void AskForRetransmission(IUdpAgent udpAgent)
            {
                int lastDatagramReceived = -1;
                int askedRetransmit = 0;
                for (int datagramIndex = 0; datagramIndex <= m_LastReceivedDatagramIndex; ++datagramIndex)
                {
                    if (m_ReceivedDatagrams[datagramIndex])
                    {
                        if (datagramIndex > lastDatagramReceived + 1)
                        {
                            // Found a gap with missing datagrams, ask for a retransmission.
                            udpAgent.SendMessage(MessageType.RetransmitFrameData, new RetransmitFrameData()
                            {
                                FrameIndex = this.FrameIndex,
                                DatagramIndexIndexStart = lastDatagramReceived + 1,
                                DatagramIndexIndexEnd = datagramIndex
                            });
                            ++askedRetransmit;
                        }
                        lastDatagramReceived = datagramIndex;
                    }
                }

                // For missing data after the last received datagram (or for nothing ever received).
                if (m_LastReceivedDatagramDataEnd < m_DataLength || m_DataLength == 0)
                {
                    udpAgent.SendMessage(MessageType.RetransmitFrameData, new RetransmitFrameData()
                    {
                        FrameIndex = this.FrameIndex,
                        DatagramIndexIndexStart = lastDatagramReceived + 1,
                        DatagramIndexIndexEnd = int.MaxValue // We do not exactly know how many datagrams compose the
                                                             // frame data but it's ok since the emitter will clamp.
                    });
                    ++askedRetransmit;
                }

                udpAgent.Stats.RetransmitFrameDataIdle(askedRetransmit);
            }

            /// <summary>
            /// Is the frame reception completed (do we have all parts of the data)?
            /// </summary>
            public bool IsFrameDataComplete => m_ReceivedDatagramsCount > 0 && m_FrameDataBytesLeftToCopy == 0;

            /// <summary>
            /// Have we received at least one datagram of the frame?
            /// </summary>
            public bool HasReceivedSomething => m_ReceivedDatagramsCount > 0;

            /// <summary>
            /// Datagrams that appear to be lost and need to be retransmitted.
            /// </summary>
            public Queue<RetransmitFrameData> PendingRetransmissions { get; } = new();

            /// <summary>
            /// <see cref="Stopwatch.GetTimestamp"/> of when the last datagram was received.
            /// </summary>
            public long LastReceivedDatagramTimestamp => m_LastReceivedDatagramTimestamp;

            /// <summary>
            /// Update <see cref="LastReceivedDatagramTimestamp"/> to now.
            /// </summary>
            public void UpdateLastReceivedDatagramTimestampToNow()
            {
                m_LastReceivedDatagramTimestamp = Stopwatch.GetTimestamp();
            }

            /// <summary>
            /// Size (in bytes) of all the data to be transmitted from the emitter to the repeaters for
            /// <see cref="FrameIndex"/>.
            /// </summary>
            public int DataLength => m_DataLength;

            /// <summary>
            /// Should we assume that datagrams ordering is preserved along the complete transmission chain.  This means
            /// that the <see cref="PartialFrameData"/> will immediately ask for a retransmission as soon as a gap is
            /// detected in the datagram sequence.
            /// </summary>
            bool m_OrderedReception;
            /// <summary>
            /// Datagrams received so far (and for which we have copied the data to <see cref="m_ExtraData"/>).
            /// </summary>
            /// <remarks>Default to 32 messages to form a FrameData, which should in theory allow us to receive +/- 44kb
            /// per frame which should normally be enough (and we wil simply grow if it is not enough).</remarks>
            BitArray m_ReceivedDatagrams = new(32);
            /// <summary>
            /// Number of unique datagrams received (for which we copied the extra data to <see cref="m_ExtraData"/>).
            /// </summary>
            int m_ReceivedDatagramsCount;
            /// <summary>
            /// <see cref="NativeExtraData"/> that contains the data of all received <see cref="FrameData"/> assembled
            /// into a single buffer ready to be used as <see cref="ReceivedMessage{TM}.ExtraData"/> as soon as all
            /// parts have been received.
            /// </summary>
            NativeExtraData m_ExtraData;
            /// <summary>
            /// How many bytes are still to be copied to AssembledData.
            /// </summary>
            int m_FrameDataBytesLeftToCopy;
            /// <summary>
            /// Index of the last datagram received (used to detect potentially lost datagrams).
            /// </summary>
            int m_LastReceivedDatagramIndex = -1;
            /// <summary>
            /// Index of the byte immediately after the last byte of data copied to m_ExtraData by datagram
            /// <see cref="m_LastReceivedDatagramIndex"/>.
            /// </summary>
            int m_LastReceivedDatagramDataEnd;
            /// <summary>
            /// <see cref="Stopwatch.GetTimestamp"/> of when the last datagram was received.
            /// </summary>
            long m_LastReceivedDatagramTimestamp;
            /// <summary>
            /// Size (in bytes) of all the data to be transmitted from the emitter to the repeaters for
            /// <see cref="FrameIndex"/>.
            /// </summary>
            int m_DataLength;
        }
    }
}
