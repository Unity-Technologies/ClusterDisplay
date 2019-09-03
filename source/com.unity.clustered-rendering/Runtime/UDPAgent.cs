using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterRendering
{
    internal class UDPAgent
    {
        public static int MaxSupportedNodeCount {get => 64;}

        private struct Message
        {
            public MessageHeader header;
            public byte[] payload;
            public TimeSpan ts;
        }
        private struct PendingAck
        {
            public Message message;
            public UInt64 m_MissingAcks;
            public TimeSpan ts;
        }

        private IPAddress m_MulticastAddress;
        private int m_RxPort;
        private int m_TxPort;
        private int m_Timeout;

        private UInt64 m_NextMessageId = 0;

        public UInt64 AllNodesMask { get; private set; }
        public byte LocalNodeID { get; private set; }
        public UInt64 LocalNodeIDMask => (UInt64) 1 << LocalNodeID;

        private UdpClient m_Connection;
        private IPEndPoint m_TxEndPoint;
        private IPEndPoint m_RxEndPoint;

        private BlockingCollection<Message> m_TxQueue = new BlockingCollection<Message>();
        private ConcurrentQueue<Message> m_RxQueue = new ConcurrentQueue<Message>();
        private List<PendingAck> m_TxQueuePendingAcks = new List<PendingAck>();
        private CancellationTokenSource m_CTS = new CancellationTokenSource();

        private ConcurrentQueue<MessageHeader> m_DeadMessages = new ConcurrentQueue<MessageHeader>();

        private Stopwatch m_Clock = new Stopwatch();
        private TimeSpan m_AcceptedAckDelay = new TimeSpan(0,0,0,0,4);
        private TimeSpan m_MessageAckTimeout;

        public AutoResetEvent RxWait { get; private set; }

        public UDPAgent(byte localNodeID, string ip, int rxPort, int txPort, int timeOut)
        {
            RxWait = new AutoResetEvent(false);
            LocalNodeID = localNodeID;
            m_Timeout = timeOut;
            m_RxPort = rxPort;
            m_TxPort = txPort;
            m_MulticastAddress = IPAddress.Parse(ip);
            m_MessageAckTimeout = new TimeSpan(0, 0, 0, timeOut);
        }

        public bool Start()
        {
            try
            {
                m_Clock.Start();
                var conn = new UdpClient(m_RxPort, AddressFamily.InterNetwork) { Ttl = 1 };
                conn.JoinMulticastGroup(m_MulticastAddress);
                m_TxEndPoint = new IPEndPoint(m_MulticastAddress, m_TxPort);
                m_RxEndPoint = new IPEndPoint(m_MulticastAddress, m_RxPort);
                m_Connection = conn;

                m_Connection.BeginReceive(ReceiveMessage, null);
                Task.Run( () => SendMessages(m_CTS.Token), m_CTS.Token);
                Task.Run( () => ResendDroppedMsgs(m_CTS.Token), m_CTS.Token);
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
                return false;
            }

            return true;
        }

        public void Stop()
        {
            m_CTS.Cancel();
            m_TxQueue.CompleteAdding();
            m_Connection.Close();
            m_Connection.Dispose();

            m_TxQueue.Dispose();
        }

        public UInt64 NewNodeNotification(byte newNodeId)
        {
            if(newNodeId+1 > MaxSupportedNodeCount)
                throw new ArgumentOutOfRangeException($"Node id must be in the range of [0,{MaxSupportedNodeCount-1}]");

            AllNodesMask |= (UInt64)1 << newNodeId;

            return AllNodesMask;
        }

        public bool NextAvailableRxMsg(out MessageHeader header, out byte[] payload)
        {
            if (m_RxQueue.TryDequeue(out var msg))
            {
                header = msg.header;
                payload = msg.payload;
                return true;
            }
            else
            {
                header = default;
                payload = null;
                return false;
            }
        }

        // msgRaw should have a blank space at start for header that gets written by this method.
        public bool PublishMessage(MessageHeader msgHeader, byte[] msgRaw)
        {
            try
            {
                msgHeader.m_Version = MessageHeader.CurrentVersion;
                msgHeader.OriginID = LocalNodeID;
                msgHeader.SequenceID = m_NextMessageId++;
                msgHeader.OffsetToPayload = (UInt16)Marshal.SizeOf<MessageHeader>();

                if (msgHeader.DestinationIDs == 0)
                {
                    if (msgHeader.Flags.HasFlag(MessageHeader.EFlag.Broadcast))
                        msgHeader.DestinationIDs = AllNodesMask;
                    else
                    {
                        Debug.LogError("Cannot PublishMessage with not destination nodes selected: " + msgHeader.MessageType);
                        return false;
                    }
                }

                msgHeader.StoreInBuffer(msgRaw);
                var msg = new Message()
                {
                    ts = m_Clock.Elapsed,
                    header = msgHeader,
                    payload = msgRaw
                };

                m_TxQueue.Add(msg);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }

            return true;
        }

        public bool PublishMessage(MessageHeader msgHeader)
        {
            try
            {
                msgHeader.m_Version = MessageHeader.CurrentVersion;
                msgHeader.OriginID = LocalNodeID;
                msgHeader.SequenceID = m_NextMessageId++;
                msgHeader.OffsetToPayload = (UInt16)Marshal.SizeOf<MessageHeader>();

                if (msgHeader.DestinationIDs == 0)
                {
                    if (msgHeader.Flags.HasFlag(MessageHeader.EFlag.Broadcast))
                        msgHeader.DestinationIDs = AllNodesMask;
                    else
                    {
                        Debug.LogError("Cannot PublishMessage with not destination nodes selected: " + msgHeader.MessageType);
                        return false;
                    }
                }

                var rawMsg = msgHeader.ToByteArray();
                var msg = new Message()
                {
                    ts = m_Clock.Elapsed,
                    header = msgHeader,
                    payload = rawMsg,
                };

                m_TxQueue.Add(msg);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }

            return true;
        }

        private void ReceiveMessage(IAsyncResult ar)
        {
            try
            {
                var receiveBytes = m_Connection.EndReceive(ar, ref m_RxEndPoint);

                var header = MessageHeader.FromByteArray(receiveBytes);
                if(header.OriginID != LocalNodeID && (header.DestinationIDs &= LocalNodeIDMask) == LocalNodeIDMask)
                {
                    if (header.MessageType == EMessageType.AckMsgRx)
                    {
                        lock (m_TxQueuePendingAcks)
                        {
                            for (int i = 0; i < m_TxQueuePendingAcks.Count; i++)
                            {
                                if (m_TxQueuePendingAcks[i].message.header.SequenceID == header.SequenceID)
                                {
                                    var pendingAck = m_TxQueuePendingAcks[i];
                                    pendingAck.m_MissingAcks &= ~((UInt64) 1 << header.OriginID);

                                    if(pendingAck.m_MissingAcks != 0)
                                        m_TxQueuePendingAcks[i] = pendingAck;
                                    else
                                        m_TxQueuePendingAcks.RemoveAt(i);
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        var msg = new Message() {header = header, payload = receiveBytes};
                        m_RxQueue.Enqueue(msg);
                        RxWait.Set();

                        SendMsgRxAck(ref header);
                    }
                }

                if( !m_CTS.IsCancellationRequested )
                    m_Connection.BeginReceive(ReceiveMessage, null);
            }
            catch (Exception e)
            {
                // Trigger some sort of error
            }
        }

        private void SendMsgRxAck(ref MessageHeader rxHeader)
        {
            var ack = new MessageHeader()
            {
                DestinationIDs = (UInt64)1 << rxHeader.OriginID,
                OriginID = LocalNodeID,
                MessageType = EMessageType.AckMsgRx,
                SequenceID = rxHeader.SequenceID,
                PayloadSize = 0,
                m_Version = MessageHeader.CurrentVersion,
                Flags = MessageHeader.EFlag.DoesNotRequireAck,
                OffsetToPayload = (UInt16)Marshal.SizeOf<MessageHeader>()
            };

            var buffer = ack.ToByteArray();
            var ackMsg = new Message()
            {
                ts = m_Clock.Elapsed,
                header = ack,
                payload = buffer
            };

            m_TxQueue.Add(ackMsg);
        }

        private void SendMessages( CancellationToken ctk )
        {
            try
            {
                foreach (var msg in m_TxQueue.GetConsumingEnumerable())
                {
                    if (!msg.header.Flags.HasFlag(MessageHeader.EFlag.DoesNotRequireAck))
                    {
                        var pendingAck = new PendingAck()
                        {
                            message = msg,
                            ts = m_Clock.Elapsed
                        };

                        if (!msg.header.Flags.HasFlag(MessageHeader.EFlag.Resending))
                            pendingAck.m_MissingAcks = (msg.header.DestinationIDs & AllNodesMask) & ~LocalNodeIDMask;

                        lock (m_TxQueuePendingAcks)
                            m_TxQueuePendingAcks.Add(pendingAck);
                    }

                    m_Connection.SendAsync(msg.payload, msg.payload.Length, m_TxEndPoint);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void ResendDroppedMsgs(CancellationToken ctk)
        {
            try
            {
                PendingAck[] expired = new PendingAck[1000];
                int expiredCount = 0;
                var tsNow = m_Clock.Elapsed;
                while (!ctk.IsCancellationRequested)
                {
                    lock (m_TxQueuePendingAcks)
                    {
                        for (var i = 0; i < m_TxQueuePendingAcks.Count; i++)
                        {
                            if ((tsNow - m_TxQueuePendingAcks[i].ts) >= m_AcceptedAckDelay)
                            {
                                expired[expiredCount++] = m_TxQueuePendingAcks[i];
                                m_TxQueuePendingAcks.RemoveAt(i--);
                            }
                        }
                    }

                    // These acks are late in coming, so re-sending messages
                    for (var i = 0; i < expiredCount; i++)
                    {
                        var expiredAck = expired[i];
                        expiredAck.message.header.DestinationIDs = expiredAck.m_MissingAcks;
                        expiredAck.message.header.StoreInBuffer(expiredAck.message.payload);

                        if (tsNow - expiredAck.message.ts > m_MessageAckTimeout)
                        {
                            m_DeadMessages.Enqueue(expiredAck.message.header);
                        }
                        else
                        {
                            expiredAck.message.header.Flags |= MessageHeader.EFlag.Resending;
                            expiredAck.ts = m_Clock.Elapsed;
                            m_TxQueue.Add(expiredAck.message, ctk);
                        }
                    }
                }

                Thread.Sleep(1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }
    }
}
