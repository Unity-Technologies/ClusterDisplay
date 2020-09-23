using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    internal class UDPAgent
    {
        public static int MaxSupportedNodeCount {get => 64;}

        private bool m_ExtensiveLogging = false;

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
        private int m_TotalResendCount;
        private int m_TotalSentCount;
        string m_AdapterName;

        private UInt64 m_NextMessageId;

        public UInt64 AllNodesMask { get; set; }
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
        private TimeSpan m_AcceptedAckDelay = new TimeSpan(0,0,0,1,000);
        private TimeSpan m_MessageAckTimeout;

        private MessageHeader.EFlag m_ExtraHdrFlags = MessageHeader.EFlag.None;

        public Action<string> OnError { get; set; }

        public NetworkingStats CurrentNetworkStats
        {
            get
            {
                lock (m_TxQueuePendingAcks)
                {
                    var stats = new NetworkingStats()
                    {
                        rxQueueSize = m_RxQueue.Count,
                        txQueueSize = m_TxQueue.Count,
                        pendingAckQueueSize = m_TxQueuePendingAcks.Count,
                        failedMsgs = m_DeadMessages.Count,
                        totalResends = m_TotalResendCount,
                        msgsSent = m_TotalSentCount,
                    };
                    return stats;
                }

            }
        }


        public AutoResetEvent RxWait { get; private set; }

        public bool IsTxQueueEmpty => m_TxQueue.Count == 0;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localNodeID"></param>
        /// <param name="ip"></param>
        /// <param name="rxPort"></param>
        /// <param name="txPort"></param>
        /// <param name="timeOut"></param>
        /// <param name="adapterName">Adapter name cannot be lo0 on OSX due to some obscure bug: https://github.com/dotnet/corefx/issues/25699#issuecomment-349263573 </param>
        public UDPAgent(byte localNodeID, string ip, int rxPort, int txPort, int timeOut, string adapterName)
        {
            RxWait = new AutoResetEvent(false);
            LocalNodeID = localNodeID;
            m_RxPort = rxPort;
            m_TxPort = txPort;
            m_MulticastAddress = IPAddress.Parse(ip);
            m_MessageAckTimeout = new TimeSpan(0, 0, 0, timeOut);
            m_AdapterName = adapterName;
        }

        public bool Start()
        {
            try
            {
                if (Application.isEditor)
                    m_ExtraHdrFlags = MessageHeader.EFlag.SentFromEditorProcess;

                m_Clock.Start();
                m_TxEndPoint = new IPEndPoint(m_MulticastAddress, m_TxPort);
                m_RxEndPoint = new IPEndPoint(IPAddress.Any, m_RxPort);

                var conn = new UdpClient();

                // Bind a particular NIC
                if (!string.IsNullOrEmpty(m_AdapterName))
                {
                    for (var index = 0; index < NetworkInterface.GetAllNetworkInterfaces().Length; index++)
                    {
                        var nic = NetworkInterface.GetAllNetworkInterfaces()[index];
                        if (nic.OperationalStatus != OperationalStatus.Up || nic.Name != m_AdapterName)
                            continue;
                        foreach (var ip in nic.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                conn.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
                                    ip.Address.GetAddressBytes());
                            }
                        }

                        break;
                    }
                }

                conn.Client.DontFragment = false;
                conn.Client.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true );
                conn.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
                conn.Client.Bind(m_RxEndPoint);

                conn.JoinMulticastGroup(m_MulticastAddress);
                m_Connection = conn;

                m_Connection.BeginReceive(ReceiveMessage, null);
                Task.Run( () => ResendDroppedMsgs(m_CTS.Token), m_CTS.Token);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }

            return true;
        }

        public void Stop()
        {
            try
            {
                m_CTS.Cancel();
                m_TxQueue.CompleteAdding();
                m_Connection.Close();
                m_Connection.Dispose();
                m_TxQueue.Dispose();
            }
            catch
            {
            }
        }

        public UInt64 NewNodeNotification(byte newNodeId)
        {
            if (newNodeId + 1 > MaxSupportedNodeCount)
            {
                OnError( $"Node id must be in the range of [0,{MaxSupportedNodeCount - 1}]");
            }
            else
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
                msgHeader.Flags |= m_ExtraHdrFlags;

                if (msgHeader.DestinationIDs == 0)
                {
                    if (msgHeader.Flags.HasFlag(MessageHeader.EFlag.Broadcast))
                        msgHeader.DestinationIDs = AllNodesMask;
                    else
                    {
                        OnError("Cannot PublishMessage with not destination nodes selected: " + msgHeader.MessageType);
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

                SendMessage(ref msg);
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
                msgHeader.Flags |= m_ExtraHdrFlags;

                if (msgHeader.DestinationIDs == 0)
                {
                    if (msgHeader.Flags.HasFlag(MessageHeader.EFlag.Broadcast))
                        msgHeader.DestinationIDs = AllNodesMask;
                    else
                    {
                        OnError("Cannot PublishMessage with not destination nodes selected: " + msgHeader.MessageType);
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

                SendMessage(ref msg);

                if (msgHeader.Flags.HasFlag(MessageHeader.EFlag.LoopBackToSender))
                {
                    var rxmsg = new Message() { header = msgHeader, payload = msg.payload };
                    m_RxQueue.Enqueue(rxmsg);
                    RxWait.Set();
                }
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
                if (header.OriginID != LocalNodeID && (header.DestinationIDs &= LocalNodeIDMask) == LocalNodeIDMask)
                {
                    if(m_ExtensiveLogging)
                        Debug.Log("Rx msg: " + header.MessageType);
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

                                    if (pendingAck.m_MissingAcks != 0)
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

                if (!m_CTS.IsCancellationRequested)
                    m_Connection.BeginReceive(ReceiveMessage, null);
            }
            catch (ObjectDisposedException)
            {

            }
            catch (Exception e)
            {
                // Trigger some sort of error
                Debug.LogException(e);
                OnError($"Async RxMessage failed with: {e.Message}");
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
                Flags = MessageHeader.EFlag.DoesNotRequireAck | m_ExtraHdrFlags,
                OffsetToPayload = (UInt16)Marshal.SizeOf<MessageHeader>()
            };

            var buffer = ack.ToByteArray();
            var ackMsg = new Message()
            {
                ts = m_Clock.Elapsed,
                header = ack,
                payload = buffer
            };

            SendMessage(ref ackMsg);
        }

        private void SendMessage(ref Message msg)
        {
            if (!msg.header.Flags.HasFlag(MessageHeader.EFlag.DoesNotRequireAck) &&
                !msg.header.Flags.HasFlag(MessageHeader.EFlag.Resending))
            {
                var pendingAck = new PendingAck
                {
                    message = msg,
                    ts = m_Clock.Elapsed,
                    m_MissingAcks = (msg.header.DestinationIDs & AllNodesMask) & ~LocalNodeIDMask
                };

                lock (m_TxQueuePendingAcks)
                    m_TxQueuePendingAcks.Add(pendingAck);
            }

            m_Connection.SendAsync(msg.payload, msg.payload.Length, m_TxEndPoint);
            Interlocked.Increment(ref m_TotalSentCount);

            if (m_ExtensiveLogging)
                Debug.Log("Tx msg: " + msg.header.MessageType);
        }

        private void ResendDroppedMsgs(CancellationToken ctk)
        {
            try
            {
                int[] expired = new int[1000];
                while (!ctk.IsCancellationRequested)
                {
                    int expiredCount = 0;
                    var tsNow = m_Clock.Elapsed;
                    lock (m_TxQueuePendingAcks)
                    {
                        for (var i = 0; i < m_TxQueuePendingAcks.Count; i++)
                        {
                            if ((tsNow - m_TxQueuePendingAcks[i].ts) >= m_AcceptedAckDelay)
                            {
                                if(expiredCount == expired.Length)
                                    Debug.LogError("To many pending acks timing out!!!");
                                else
                                {
                                    expired[expiredCount++] = i;
                                }
                            }
                        }
                    }

                    // These acks are late in coming, so re-sending messages
                    for (var i = 0; i < expiredCount; i++)
                    {
                        PendingAck expiredAck;
                        lock(m_TxQueuePendingAcks)
                            expiredAck = m_TxQueuePendingAcks[expired[i]]; 

                        expiredAck.message.header.DestinationIDs = expiredAck.m_MissingAcks;

                        if (tsNow - expiredAck.message.ts > m_MessageAckTimeout)
                        {
                            OnError( $"Msg could not be delivered: {expiredAck.message.header.MessageType}, destinations: {expiredAck.message.header.DestinationIDs}");
                            lock(m_TxQueuePendingAcks)
                                m_TxQueuePendingAcks.RemoveAt(expired[i]);
                        }
                        else
                        {
                            expiredAck.message.header.Flags |= MessageHeader.EFlag.Resending;
                            expiredAck.message.header.StoreInBuffer(expiredAck.message.payload);
                            expiredAck.ts = m_Clock.Elapsed;
                            m_TotalResendCount++;
                            m_TxQueuePendingAcks[expired[i]] = expiredAck; // updates entry (struct)
                            SendMessage(ref expiredAck.message);
                        }
                    }
                }

                Thread.Sleep(1);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
