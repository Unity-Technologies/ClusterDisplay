using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    class UDPAgent : IDisposable
    {
        public static int MaxSupportedNodeCount = BitVector.Length;

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
            public byte nodeId;
            public TimeSpan ts;
        }

        private IPAddress m_MulticastAddress;
        private int m_RxPort;
        private int m_TxPort;
        private int m_TotalResendCount;
        private int m_TotalSentCount;
        string m_AdapterName;

        private UInt64 m_NextMessageId;

        public BitVector AllNodesMask { get; set; }
        public byte LocalNodeID { get; private set; }
        public BitVector LocalNodeIDMask => BitVector.FromIndex(LocalNodeID);

        private UdpClient m_Connection;
        private IPEndPoint m_TxEndPoint;
        private IPEndPoint m_RxEndPoint;

        readonly ConcurrentQueue<Message> m_RxQueue;
        readonly ConcurrentDictionary<(ulong, byte), PendingAck> m_TxQueuePendingAcks;

        public bool AcksPending => m_TxQueuePendingAcks.Count > 0;

        private CancellationTokenSource m_CTS;

        private ConcurrentQueue<MessageHeader> m_DeadMessages;

        private Stopwatch m_ConnectionClock = new Stopwatch();
        private TimeSpan m_AcceptedAckDelay = new TimeSpan(0, 0, 0, 1, 000);
        private TimeSpan m_MessageAckTimeout;

        private MessageHeader.EFlag m_ExtraHdrFlags = MessageHeader.EFlag.None;

        public Action<string> OnError { get; set; }

        public NetworkingStats CurrentNetworkStats
        {
            get
            {
                var stats = new NetworkingStats()
                {
                    rxQueueSize = m_RxQueue?.Count ?? 0,
                    pendingAckQueueSize = m_TxQueuePendingAcks?.Count ?? 0,
                    failedMsgs = m_DeadMessages?.Count ?? 0,
                    totalResends = m_TotalResendCount,
                    msgsSent = m_TotalSentCount,
                };

                return stats;
            }
        }

        public AutoResetEvent RxWait { get; private set; }

        public struct Config
        {
            public byte nodeId;
            public string ip;
            public int rxPort;
            public int txPort;
            public int timeOut;
            public string adapterName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localNodeID"></param>
        /// <param name="ip"></param>
        /// <param name="rxPort"></param>
        /// <param name="txPort"></param>
        /// <param name="timeOut"></param>
        /// <param name="adapterName">Adapter name cannot be lo0 on OSX due to some obscure bug: https://github.com/dotnet/corefx/issues/25699#issuecomment-349263573 </param>
        public UDPAgent(Config config)
        {
            RxWait = new AutoResetEvent(false);
            LocalNodeID = config.nodeId;
            m_RxPort = config.rxPort;
            m_TxPort = config.txPort;
            m_MulticastAddress = IPAddress.Parse(config.ip);
            m_MessageAckTimeout = new TimeSpan(0, 0, 0, config.timeOut);
            m_AdapterName = config.adapterName;

            m_RxQueue = new ConcurrentQueue<Message>();

            m_TxQueuePendingAcks = new ConcurrentDictionary<(ulong, byte), PendingAck>();
            m_CTS = new CancellationTokenSource();
            m_DeadMessages = new ConcurrentQueue<MessageHeader>();
            Initialize();
        }

        ~UDPAgent() => Dispose();

        private bool SelectNetworkInterface(out NetworkInterface selectedNic)
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            List<NetworkInterface> upNics = new List<NetworkInterface>();
            selectedNic = null;

            for (var index = 0; index < nics.Length; index++)
            {
                var nic = nics[index];
                ClusterDebug.Log($"Polling interface: \"{nic.Name}\".");

                bool isExplicitNic = !string.IsNullOrEmpty(m_AdapterName) && nic.Name == m_AdapterName;
                bool isUp = nic.OperationalStatus == OperationalStatus.Up;

                if (!isUp)
                {
                    if (isExplicitNic)
                    {
                        ClusterDebug.LogError(
                            $"Unable to use explicit interface: \"{nic.Name}\", the interface is down. Attempting to use the next available interface.");
                    }

                    continue;
                }

                var ipProperties = nic.GetIPProperties();
                if (!ipProperties.MulticastAddresses.Any())
                {
                    continue;
                }

                if (!nic.SupportsMulticast)
                {
                    continue;
                }

                upNics.Add(nic);
                if (!isExplicitNic)
                    continue;

                selectedNic = nic;
                ClusterDebug.Log($"Selecting explicit interface: \"{selectedNic.Name}\".");
            }

            if (selectedNic == null)
            {
                if (upNics.Count == 0)
                {
                    ClusterDebug.LogError($"There are NO available interfaces to bind cluster display to.");
                    return false;
                }

                selectedNic = upNics[0];
                ClusterDebug.Log($"No explicit interface defined, defaulting to interface: \"{selectedNic.Name}\".");
            }

            return true;
        }

        void Initialize()
        {
            if (Application.isEditor)
                m_ExtraHdrFlags = MessageHeader.EFlag.SentFromEditorProcess;

            m_ConnectionClock.Start();
            m_TxEndPoint = new IPEndPoint(m_MulticastAddress, m_TxPort);
            m_RxEndPoint = new IPEndPoint(IPAddress.Any, m_RxPort);

            var conn = new UdpClient();
            conn.Client.SendBufferSize = ushort.MaxValue;
            conn.Client.ReceiveBufferSize = ushort.MaxValue;
            conn.Client.DontFragment = false;

            // Bind a particular NIC
            if (!SelectNetworkInterface(out var selectedNic))
            {
                throw new IOException("There are no available network interfaces that support Cluster Display");
            }

            ClusterDebug.Log($"Binding to interface: \"{selectedNic.Name}\".");

            foreach (var ip in selectedNic.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                Debug.Log(ip.Address);
                conn.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, ip.Address.GetAddressBytes());
            }

            conn.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            conn.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            conn.Client.Bind(m_RxEndPoint);

            conn.JoinMulticastGroup(m_MulticastAddress);
            m_Connection = conn;

            m_Connection.BeginReceive(ReceiveMessage, null);
            Task.Run(() => ResendDroppedMsgs(m_CTS.Token), m_CTS.Token);
        }

        public void Stop() => Dispose();

        public void Dispose()
        {
            ClusterDebug.Log($"Disposing {nameof(UDPAgent)}.");
            try
            {
                m_CTS.Cancel();

                m_Connection.Close();
                m_Connection.Dispose();

                m_CTS = null;
                m_Connection = null;
            }
            catch { }
        }

        public BitVector NewNodeNotification(byte newNodeId)
        {
            if (newNodeId + 1 > MaxSupportedNodeCount)
            {
                OnError($"Node id must be in the range of [0,{MaxSupportedNodeCount - 1}]");
            }
            else
            {
                AllNodesMask = AllNodesMask.SetBit(newNodeId);
            }

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

            header = default;
            payload = null;
            return false;
        }

        // msgRaw should have a blank space at start for header that gets written by this method.
        public bool PublishMessage(MessageHeader msgHeader, byte[] msgRaw)
        {
            msgHeader.m_Version = MessageHeader.CurrentVersion;
            msgHeader.OriginID = LocalNodeID;
            msgHeader.SequenceID = m_NextMessageId++;
            msgHeader.OffsetToPayload = (UInt16) Marshal.SizeOf<MessageHeader>();
            msgHeader.Flags |= m_ExtraHdrFlags;

            if (!msgHeader.DestinationIDs.Any())
            {
                if (msgHeader.Flags.HasFlag(MessageHeader.EFlag.Broadcast))
                    msgHeader.DestinationIDs = AllNodesMask;
                else
                {
                    OnError("Cannot PublishMessage with not destination nodes selected: " + msgHeader.MessageType);
                    return false;
                }
            }

            msgHeader.StoreInBuffer(msgRaw, 0);
            var msg = new Message()
            {
                ts = m_ConnectionClock.Elapsed,
                header = msgHeader,
                payload = msgRaw
            };

            SendMessage(ref msg);
            return true;
        }

        public bool PublishMessage(MessageHeader msgHeader)
        {
            try
            {
                msgHeader.m_Version = MessageHeader.CurrentVersion;
                msgHeader.OriginID = LocalNodeID;
                msgHeader.SequenceID = m_NextMessageId++;
                msgHeader.OffsetToPayload = (UInt16) Marshal.SizeOf<MessageHeader>();
                msgHeader.Flags |= m_ExtraHdrFlags;

                if (!msgHeader.DestinationIDs.Any())
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
                    ts = m_ConnectionClock.Elapsed,
                    header = msgHeader,
                    payload = rawMsg,
                };

                SendMessage(ref msg);

                if (msgHeader.Flags.HasFlag(MessageHeader.EFlag.LoopBackToSender))
                {
                    var rxmsg = new Message() {header = msgHeader, payload = msg.payload};
                    m_RxQueue.Enqueue(rxmsg);
                    RxWait.Set();
                }
            }

            catch (Exception e)
            {
                ClusterDebug.LogException(e);
                return false;
            }

            return true;
        }

        private void ReceiveMessage(IAsyncResult ar)
        {
            if (m_Connection == null) return;

            var receiveBytes = m_Connection.EndReceive(ar, ref m_RxEndPoint);

            var header = receiveBytes.LoadStruct<MessageHeader>();
            if (header.OriginID != LocalNodeID && header.DestinationIDs[LocalNodeID])
            {
                ClusterDebug.Log($"(Sequence ID: {header.SequenceID}): Received message of type: {header.MessageType}");

                // If we've received an ACK from some node
                if (header.MessageType == EMessageType.AckMsgRx)
                {
                    var key = (header.SequenceID, header.OriginID);
                    if (m_TxQueuePendingAcks.TryGetValue(key, out var pendingAck))
                    {
                        var roundTripTime = (m_ConnectionClock.Elapsed - pendingAck.ts);
                        ClusterDebug.Log($"Received ACK from node: {header.OriginID} with sequence ID: {header.SequenceID} with a round trip time of {roundTripTime.TotalMilliseconds} ms.");

                        m_TxQueuePendingAcks.Remove(key, out _);
                    }
                }

                else // If we've received some message that ss NOT an ACK from some node (emitter or repeater).
                {
                    var msg = new Message() {header = header, payload = receiveBytes};
                    m_RxQueue.Enqueue(msg);
                    RxWait.Set();

                    // Respond to the sending node with an ACK that we've received the message.
                    if (!header.Flags.HasFlag(MessageHeader.EFlag.DoesNotRequireAck))
                    {
                        SendMsgRxAck(ref header, msg.ts);
                    }
                }
            }

            if (!m_CTS.IsCancellationRequested)
                m_Connection.BeginReceive(ReceiveMessage, null);
        }

        private void SendMsgRxAck(ref MessageHeader rxHeader, TimeSpan ts)
        {
            var ack = new MessageHeader()
            {
                DestinationIDs = BitVector.FromIndex(rxHeader.OriginID),
                OriginID = LocalNodeID,
                MessageType = EMessageType.AckMsgRx,
                SequenceID = rxHeader.SequenceID,
                PayloadSize = 0,
                m_Version = MessageHeader.CurrentVersion,
                Flags = MessageHeader.EFlag.DoesNotRequireAck | m_ExtraHdrFlags,
                OffsetToPayload = (UInt16) Marshal.SizeOf<MessageHeader>()
            };

            var buffer = ack.ToByteArray();
            var ackMsg = new Message()
            {
                ts = ts,
                header = ack,
                payload = buffer
            };

            ClusterDebug.Log($"(Sending ACK from node: {ack.OriginID} to nodes: {ack.DestinationIDs} for message type: {rxHeader.MessageType}");
            SendMessage(ref ackMsg);
        }

        private void SendMessage(ref Message msg)
        {
            if (msg.payload.Length > ushort.MaxValue)
            {
                OnError($"Unable to send message of type: {msg.header.MessageType}, the message payload is larger then the MTU size: {ushort.MaxValue}");
                return;
            }

            ClusterDebug.Log($"(Sequence ID: {msg.header.SequenceID}): " +
                $"Sending message of type: {msg.header.MessageType} of size: {msg.payload.Length} " +
                $"to:  {m_TxEndPoint} " +
                $"with flags {msg.header.Flags}");

            if (!msg.header.Flags.HasFlag(MessageHeader.EFlag.DoesNotRequireAck) &&
                !msg.header.Flags.HasFlag(MessageHeader.EFlag.Resending))
            {
                var destinations = msg.header.DestinationIDs
                    .MaskBits(AllNodesMask)
                    .UnsetBit(LocalNodeID);
                for (var i = 0; i < BitVector.Length; i++)
                {
                    if (destinations[i])
                    {
                        var pendingAck = new PendingAck
                        {
                            message = msg,
                            ts = m_ConnectionClock.Elapsed,
                            nodeId = (byte) i
                        };
                        if (!m_TxQueuePendingAcks.TryAdd((msg.header.SequenceID, (byte) i), pendingAck))
                        {
                            ClusterDebug.Log($"Could not queue ack for {(msg.header.SequenceID, i)}. Is this a duplicate?");
                        }
                    }
                }
            }

            m_Connection.SendAsync(msg.payload, msg.payload.Length, m_TxEndPoint);
            Interlocked.Increment(ref m_TotalSentCount);

            if (m_ExtensiveLogging)
                ClusterDebug.Log("Tx msg: " + msg.header.MessageType);
        }

        // This runs on another thread and loops forever validating whether we need to resend a message
        // if we haven't received an ACK for that message within a period.
        private void ResendDroppedMsgs(CancellationToken ctk)
        {
            Span<(ulong, byte)> expired = stackalloc (ulong, byte)[1000];
            while (!ctk.IsCancellationRequested)
            {
                int expiredCount = 0;
                var tsNow = m_ConnectionClock.Elapsed;
                var allPendingAcks = m_TxQueuePendingAcks.Values;

                // Determine if we've timed on receiving any ACKs for messages we've sent previously.
                foreach (var pendingAck in allPendingAcks)
                {
                    if (tsNow - pendingAck.ts < m_AcceptedAckDelay)
                        continue;

                    if (expiredCount >= expired.Length)
                    {
                        ClusterDebug.LogError($"There are to many expired ACKs! Cannot queue pending ACK: (Sequence ID: {pendingAck.message.header.SequenceID}, Message Type: {pendingAck.message.header.MessageType})");
                        break;
                    }

                    ClusterDebug.LogWarning($"Never received ACK from node: {pendingAck.nodeId} for message: (Sequence ID: {pendingAck.message.header.SequenceID}, Message Type: {pendingAck.message.header.MessageType}), queuing message for resend.");
                    expired[expiredCount++] = (pendingAck.message.header.SequenceID, pendingAck.nodeId);
                }

                // These acks are late in coming, so re-sending messages
                if (expiredCount == 0)
                    continue;

                ClusterDebug.LogWarning($"Attempting to resend: {expiredCount} ACKs.");

                int resentACKs = 0;
                for (var i = 0; i < expiredCount; i++)
                {
                    if (!m_TxQueuePendingAcks.TryGetValue(expired[i], out var expiredAck))
                    {
                        continue;
                    }

                    if (tsNow - expiredAck.message.ts > m_MessageAckTimeout)
                    {
                        ClusterDebug.LogWarning(
                            $"Message of type: {expiredAck.message.header.MessageType} with sequence ID: " +
                            "{expiredAck.message.header.SequenceID} could not be delivered after multiple " +
                            "resends. Either the message was:" +
                            "\n\t1. Never received." +
                            "\n\t2. The receiver never responded with an ACK." +
                            "\n\t3. We never received the ACK and the packet was dropped.");

                        m_TxQueuePendingAcks.TryRemove(expired[i], out _);
                        continue;
                    }

                    // Update destination ID, flags, and timestamps in the header
                    var updatedAck = expiredAck;
                    var msg = updatedAck.message;
                    var header = msg.header;
                    header.DestinationIDs = BitVector.FromIndex(updatedAck.nodeId);
                    header.Flags |= MessageHeader.EFlag.Resending;
                    msg.header = header;
                    updatedAck.message = msg;
                    
                    updatedAck.ts = m_ConnectionClock.Elapsed;

                    // Need to overwrite the header data in the payload field also
                    updatedAck.message.header.StoreInBuffer(updatedAck.message.payload);
                    
                    if (m_TxQueuePendingAcks.TryUpdate(expired[i], updatedAck, expiredAck))
                    {
                        m_TotalResendCount++;
                        resentACKs++;

                        ClusterDebug.LogWarning($"Resending message of type: {expiredAck.message.header.MessageType} with sequence ID: {expiredAck.message.header.SequenceID}");
                        SendMessage(ref updatedAck.message);
                    }
                }

                ClusterDebug.LogWarning($"Remaining messages to resend: {expiredCount - resentACKs}");
            }
        }
    }
}
