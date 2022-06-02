using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Class responsible to generate multiple <see cref="FrameData"/> messages from a single big
    /// <see cref="FrameDataBuffer"/> and handle retransmission of packets if needed.
    /// </summary>
    class FrameDataSplitter: IDisposable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="udpAgent">Lower lever network access on which we are sending individual fragments of the whole
        /// frame data.</param>
        /// <param name="retransmitHistory">Number of frames we keep in history to be retransmitted (must be >= 2).</param>
        /// <exception cref="ArgumentException">If retransmitHistory &lt; 2.</exception>
        public FrameDataSplitter(IUDPAgent udpAgent, int retransmitHistory = 2)
        {
            if (retransmitHistory < 2)
            {
                throw new ArgumentException("retransmitHistory need to be >= 2.");
            }
            if (!udpAgent.ReceivedMessageTypes.Contains(MessageType.RetransmitFrameData))
            {
                throw new ArgumentException("UDPAgent does not support receiving required MessageType.RetransmitFrameData");
            }

            UDPAgent = udpAgent;
            m_MaxDataPerMessage = UDPAgent.MaximumMessageSize - Marshal.SizeOf<FrameData>();
            m_SentFramesInformation = new SentFrameInformation[retransmitHistory];
            m_NewestSentFramesInformationIndex = retransmitHistory - 1;
            for (int i = 0; i < retransmitHistory; ++i)
            {
                m_SentFramesInformation[i].DatagramSentTimestamp = new long[64]; // 64 datagrams should be enough for most frames
            }

            UDPAgent.OnMessagePreProcess += PreProcessReceivedMessage;
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~FrameDataSplitter() => Dispose(false);

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Lower lever network access on which we are sending individual fragments of the whole frame data.
        /// </summary>
        public IUDPAgent UDPAgent { get; private set; }

        /// <summary>
        /// Send the specified frame over the network (splitting it in multiple smaller packets that can then be
        /// reassembled).
        /// </summary>
        /// <param name="frameIndex">Index of the frame this message contains information about.</param>
        /// <param name="frameData">The data to be transmitted.  Caller of this method shouldn't reused it for anything
        /// else until this object returns it through <see cref="UnusedFrameDataBuffers"/>.</param>
        /// <exception cref="ArgumentException">If <see cref="frameIndex"/> is not equal to the <see cref="frameIndex"/>
        /// of the previous call + 1.</exception>
        public void SendFrameData(ulong frameIndex, ref FrameDataBuffer frameData)
        {
            // First things, let's add an entry to m_SentFramesInformation (in case we get retransmission requests while
            // we are still transmitting).
            lock (m_SentFramesInformation)
            {
                var previousLast = m_SentFramesInformation[m_NewestSentFramesInformationIndex];
                if (previousLast.DataBuffer != null && previousLast.FrameIndex + 1 != frameIndex)
                {
                    throw new ArgumentException("Non consecutive frameIndex detected, previous one was " +
                        previousLast.FrameIndex + " and the new one is " + frameIndex);
                }

                var newFrameToSend = m_SentFramesInformation[m_OldestSentFramesInformationIndex];
                if (newFrameToSend.DataBuffer != null)
                {
                    Debug.Assert(newFrameToSend.DataBuffer.IsValid); // Otherwise it was disposed of before we were done which is bad...
                    UnusedFrameDataBuffers.Enqueue(newFrameToSend.DataBuffer);
                }
                newFrameToSend.FrameIndex = frameIndex;
                newFrameToSend.DataBuffer = frameData;
                Array.Clear(newFrameToSend.DatagramSentTimestamp, 0, newFrameToSend.DatagramSentTimestamp.Length);
                m_SentFramesInformation[m_OldestSentFramesInformationIndex] = newFrameToSend;

                m_OldestSentFramesInformationIndex =
                    (m_OldestSentFramesInformationIndex + 1) % m_SentFramesInformation.Length;
                m_NewestSentFramesInformationIndex =
                    (m_NewestSentFramesInformationIndex + 1) % m_SentFramesInformation.Length;
                ++m_SentFrames;

                // We can now send the messages necessary to transmit the complete frameData.
                int nbrDatagrams = frameData.Length / m_MaxDataPerMessage;
                if (frameData.Length % m_MaxDataPerMessage > 0)
                {
                    ++nbrDatagrams;
                }
                for (int datagramIndex = 0; datagramIndex < nbrDatagrams; ++datagramIndex)
                {
                    SendDatagramOf(newFrameToSend, datagramIndex);
                }
            }

            frameData = null;
        }

        /// <summary>
        /// <see cref="FrameDataBuffer"/> that are not used anymore by the <see cref="FrameDataSplitter"/> and can be
        /// reused to contain data of other frames.
        /// </summary>
        public ConcurrentQueue<FrameDataBuffer> UnusedFrameDataBuffers { get; } = new();

        /// <summary>
        /// Send the specified datagram.
        /// </summary>
        /// <param name="frameInformation">Information about the frame the datagram is part of.</param>
        /// <param name="datagramIndex">Index of datagram in the sequence of datagrams for that frame.</param>
        void SendDatagramOf(SentFrameInformation frameInformation, int datagramIndex)
        {
            if (datagramIndex > frameInformation.DatagramSentTimestamp.Length)
            {
                var newArray = new long[datagramIndex + 16];
                Array.Copy(frameInformation.DatagramSentTimestamp, newArray, frameInformation.DatagramSentTimestamp.Length);
                Array.Clear(newArray, frameInformation.DatagramSentTimestamp.Length,
                    newArray.Length - frameInformation.DatagramSentTimestamp.Length);
                frameInformation.DatagramSentTimestamp = newArray;
            }

            // Check that the datagram has not already been sent "not long ago" to avoid unnecessary retransmission when
            // may repeaters ask for retransmission of the same thing.
            if (Stopwatch.GetTimestamp() < frameInformation.DatagramSentTimestamp[datagramIndex] + m_ShortRetransmissionDelayTicks)
            {
                return;
            }

            var frameData = new FrameData()
            {
                FrameIndex = frameInformation.FrameIndex,
                DataLength = frameInformation.DataBuffer.Length,
                DatagramIndex = datagramIndex,
                DatagramDataOffset = m_MaxDataPerMessage * datagramIndex
            };
            int dataToSend = Math.Min(frameInformation.DataBuffer.Length - frameData.DatagramDataOffset,
                m_MaxDataPerMessage);
            UDPAgent.SendMessage(MessageType.FrameData, frameData,
                frameInformation.DataBuffer.DataSpan(frameData.DatagramDataOffset, dataToSend));
            frameInformation.DatagramSentTimestamp[datagramIndex] = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Preprocess a received message and check for FrameData retransmission request.
        /// </summary>
        /// <param name="received">Received <see cref="ReceivedMessageBase"/> to preprocess.</param>
        /// <returns>Preprocessed <see cref="ReceivedMessageBase"/> or null if the message is to be dropped.</returns>
        ReceivedMessageBase PreProcessReceivedMessage(ReceivedMessageBase received)
        {
            // We are only interested in retransmit requests, everything else should simply pass through
            if (received.Type != MessageType.RetransmitFrameData)
            {
                return received;
            }

            using var toDisposeOfReceivedAtExit = received;
            var retransmitMessage = (ReceivedMessage<RetransmitFrameData>)received;
            Debug.Assert(retransmitMessage != null); // since received.Type == MessageType.FrameData

            lock (m_SentFramesInformation) // Remark, we have to keep the lock for the whole time we are retransmitting
            {                              // as otherwise another thread could move the DataBuffer to UnusedFrameDataBuffers.
                                           // Not ideal, but amount of retransmission should be low, so this is not too bad.

                // Validate frame index range
                ulong oldestFrameIndex = m_SentFramesInformation[m_OldestSentFramesInformationIndex].FrameIndex;
                ulong newestFrameIndex = m_SentFramesInformation[m_NewestSentFramesInformationIndex].FrameIndex;
                if (retransmitMessage.Payload.FrameIndex < oldestFrameIndex ||
                    retransmitMessage.Payload.FrameIndex > newestFrameIndex)
                {
                    // Don't send warning message if client ask retransmission for "the next frame", it might simply be
                    // a little bit faster than us and we should be able to send it shortly...
                    if (retransmitMessage.Payload.FrameIndex != newestFrameIndex + 1)
                    {
                        Debug.LogWarning($"Asking to retransmit a frame for which we currently do not have data " +
                            $"anymore: {retransmitMessage.Payload.FrameIndex}, we only have frames in the range of " +
                            $"[{oldestFrameIndex}, {newestFrameIndex}], skipping.");
                    }
                    return null;
                }

                // Validate we have data for that frame
                int bufferIndex = (m_OldestSentFramesInformationIndex +
                        (int)(retransmitMessage.Payload.FrameIndex - oldestFrameIndex)) % m_SentFramesInformation.Length;
                if (m_SentFrames < (ulong)m_SentFramesInformation.Length)
                {
                    // Not enough frames have been sent yet and content of m_SentFramesInformation is not totally stable,
                    // search among all entries for one that looks to what we are searching.
                    for (int i = 0; i < m_SentFramesInformation.Length; ++i)
                    {
                        if (m_SentFramesInformation[i].FrameIndex == retransmitMessage.Payload.FrameIndex &&
                            m_SentFramesInformation[i].DataBuffer != null)
                        {
                            bufferIndex = i;
                            break;
                        }
                    }
                }
                var frameDataInformation = m_SentFramesInformation[bufferIndex];
                if (frameDataInformation.DataBuffer == null)
                {
                    Debug.LogWarning($"Asking to retransmit a frame for which we have no data.");
                    return null;
                }

                // Re-send datagrams
                int nbrDatagrams = frameDataInformation.DataBuffer.Length / m_MaxDataPerMessage;
                if (frameDataInformation.DataBuffer.Length % m_MaxDataPerMessage > 0)
                {
                    ++nbrDatagrams;
                }
                int stopDatagramIndex = Math.Min(retransmitMessage.Payload.DatagramIndexIndexEnd, nbrDatagrams);
                for (int datagramIndex = Math.Max(0, retransmitMessage.Payload.DatagramIndexIndexStart);
                     datagramIndex < stopDatagramIndex; ++datagramIndex)
                {
                    SendDatagramOf(frameDataInformation, datagramIndex);
                    UDPAgent.Stats.SentMessageWasRepeat(MessageType.FrameData);
                }
            }

            return null;
        }

        /// <summary>
        /// Information stored about each sent frame that we keep to be able to retransmit parts of in case of a need.
        /// </summary>
        struct SentFrameInformation
        {
            /// <summary>
            /// Index of the frame this struct contains information about.
            /// </summary>
            public ulong FrameIndex;
            /// <summary>
            /// The data that was transmitted.
            /// </summary>
            public FrameDataBuffer DataBuffer;
            /// <summary>
            /// <see cref="Stopwatch.GetTimestamp"/> of when was the datagram corresponding to the index last sent.
            /// </summary>
            public long[] DatagramSentTimestamp;
        }

        /// <summary>
        /// Method unifying finalizer / IDisposable implementation.
        /// </summary>
        /// <param name="disposing"><c>true</c> if called from <see cref="IDisposable.Dispose"/> or <c>false</c> if
        /// called from the finalizer.</param>
        protected void Dispose(bool disposing)
        {
            if (!m_DisposedOf)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects)
                    if (UDPAgent != null)
                    {
                        UDPAgent.OnMessagePreProcess -= PreProcessReceivedMessage;
                    }

                    // Dispose of FrameDataBuffer we still know about (no one should be using them anyway, so let's
                    // proactively dispose of them instead of waiting for GC).
                    lock (m_SentFramesInformation)
                    {
                        foreach (var sentFrameInformation in m_SentFramesInformation)
                        {
                            if (sentFrameInformation.DataBuffer != null)
                            {
                                Debug.Assert(sentFrameInformation.DataBuffer.IsValid);
                                sentFrameInformation.DataBuffer.Dispose();
                            }
                        }
                    }
                    while (UnusedFrameDataBuffers.TryDequeue(out var frameDataBuffer))
                    {
                        frameDataBuffer.Dispose();
                    }
                }

                // Free unmanaged resources (unmanaged objects) and override finalizer

                // Done
                m_DisposedOf = true;
            }
        }
        bool m_DisposedOf;

        /// <summary>
        /// Maximum amount of frame data that can be sent with each <see cref="FrameData"/> through
        /// <see cref="UDPAgent"/>.
        /// </summary>
        readonly int m_MaxDataPerMessage;
        /// <summary>
        /// Minimum delay between two transmission of the same datagram to avoid unnecessary retransmission of datagrams.
        /// </summary>
        /// <remarks>2 milliseconds</remarks>
        readonly long m_ShortRetransmissionDelayTicks = Stopwatch.Frequency * 2 / 1000;

        /// <summary>
        /// Frame kept in case we need to retransmit sections of.
        /// </summary>
        /// <remarks>Should always be locked before accessing.</remarks>
        SentFrameInformation[] m_SentFramesInformation;
        /// <summary>
        /// Index of the oldest <see cref="SentFrameInformation"/> in m_SentFramesInformation.
        /// </summary>
        /// <remarks>Should always be access with a lock on <see cref="m_SentFramesInformation"/>.</remarks>
        int m_OldestSentFramesInformationIndex;
        /// <summary>
        /// Index of the most recent <see cref="SentFrameInformation"/> in m_SentFramesInformation.
        /// </summary>
        /// <remarks>Should always be access with a lock on <see cref="m_SentFramesInformation"/>.</remarks>
        int m_NewestSentFramesInformationIndex;
        /// <summary>
        /// How many frames have been sent.
        /// </summary>
        ulong m_SentFrames;
    }
}
