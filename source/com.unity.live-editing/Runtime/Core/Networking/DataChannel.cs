using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.LiveEditing.LowLevel.Networking
{
    /// <summary>
    /// Packet metadata. Contains information necessary to read a packet from a stream.
    /// </summary>
    /// <remarks>
    /// For internal use.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    readonly struct PacketHeader
    {
        const int k_Revision = 0;
        public readonly int Revision;
        public readonly MessageType MessageType;
        public readonly int ChannelId;
        public readonly int PayLoadSize;
        // TODO: add extra metadata here

        public static PacketHeader CreateForBinaryBlob(int channelId, int payLoadSize)
        {
            return new(channelId, MessageType.Binary, payLoadSize);
        }

        public static PacketHeader CreateIdAssignment(int channelId)
        {
            return new(-1, MessageType.IdAssignment, sizeof(int));
        }

        PacketHeader(int channelId, MessageType type, int payLoadSize)
        {
            Revision = k_Revision;
            ChannelId = channelId;
            PayLoadSize = payLoadSize;
            MessageType = type;
        }
    }

    enum MessageType : int
    {
        Binary,
        IdAssignment
    }

    /// <summary>
    /// Low-level primitive for sending and receiving packets (each packet consists of a header and a byte array)
    /// over a given socket.
    /// </summary>
    class DataChannel : IDisposable
    {
        public delegate void PacketReceivedHandler(in PacketHeader header, ReadOnlySpan<byte> payload);

        const int k_SendQueueMaxLength = 32;
        const int k_ReceiveBufferSize = 1024 * 64; // 64k should be enough for everybody ;)
        readonly Socket m_Socket;
        readonly Task m_ReceiveTask;
        readonly Task m_SendTask;

        BlockingCollection<QueuedPacket> m_OutgoingPackets; // TODO: replace with non-allocating implementation
        readonly CancellationTokenSource m_CancellationTokenSource;
        readonly TaskCompletionSource<int> m_IdAssignmentTaskSource = new();

        public int Id { get; private set; } = -1;

        // TODO report status as enum
        public bool IsStopped => m_ReceiveTask.IsCompleted || m_SendTask.IsCompleted;
        public Exception CurrentException { get; private set; }

        /// <summary>
        /// Creates a new channel on the specified socket.
        /// </summary>
        /// <param name="socket">The socket to use.</param>
        /// <param name="cancellationToken">Token used to cancel async reads and writes.</param>
        /// <param name="receivedHandler">Callback for handling the arrival of data.</param>
        /// <remarks>
        /// The caller is responsible for creating the socket in a usable state such that it is able to send and
        /// receive data.
        /// </remarks>
        public DataChannel(Socket socket, CancellationToken cancellationToken, PacketReceivedHandler receivedHandler) {
            m_CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            m_Socket = socket;
            m_OutgoingPackets = new BlockingCollection<QueuedPacket>(k_SendQueueMaxLength);
            m_ReceiveTask = ReceivePacketsAsync(receivedHandler, m_CancellationTokenSource.Token);
            m_SendTask = SendPacketsInQueueAsync(m_CancellationTokenSource.Token);
        }

        public async ValueTask<int> WaitForIdAssignment()
        {
            return await m_IdAssignmentTaskSource.Task;
        }

        public void EnqueueIdChange(int newId)
        {
            Id = newId;
            var header = PacketHeader.CreateIdAssignment(newId);
            m_OutgoingPackets.Add(QueuedPacket.Create(header, ref newId));
            m_IdAssignmentTaskSource.SetResult(Id);
        }

        public void EnqueueSend(in PacketHeader header, ReadOnlySpan<byte> payload) =>
            m_OutgoingPackets.Add(new QueuedPacket(header, payload), m_CancellationTokenSource.Token);

        readonly struct QueuedPacket : IDisposable
        {
            public readonly PacketHeader Header;
            public readonly byte[] PayloadData;

            public QueuedPacket(in PacketHeader header, ReadOnlySpan<byte> data)
            {
                Header = header;
                PayloadData = ArrayPool<byte>.Shared.Rent(header.PayLoadSize);
                data[..header.PayLoadSize].CopyTo(PayloadData);
            }

            public static QueuedPacket Create<T>(in PacketHeader header, ref T value) where T : unmanaged
            {
                Debug.Assert(Marshal.SizeOf<T>() == header.PayLoadSize);
                return new QueuedPacket(header, MemoryMarshal.CreateReadOnlySpan(ref value, 1).AsBytes());
            }

            public void Dispose()
            {
                ArrayPool<byte>.Shared.Return(PayloadData);
            }
        }
        Task SendPacketsInQueueAsync(CancellationToken token)
        {
            return Task.Run(() =>
            {
                try
                {
                    while (m_OutgoingPackets.TryTake(out var packet, Timeout.Infinite, token))
                    {
                        m_Socket.SendPacket(packet.Header, packet.PayloadData);
                        packet.Dispose();
                    }

                    Debug.LogError("Whoops. Shouldn't happen.");
                }
                catch (OperationCanceledException)
                {
                    // Don't bubble up cancellations
                }
                catch (Exception ex)
                {
                    CurrentException = ex;
                }
            }, token).ContinueWith(_ =>
            {
                Debug.Log($"{nameof(SendPacketsInQueueAsync)} exited");
            });
        }

        Task ReceivePacketsAsync(PacketReceivedHandler receivedHandler, CancellationToken token)
        {
            return Task.Run(() =>
            {
                Span<byte> payload = stackalloc byte[k_ReceiveBufferSize];
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var header = m_Socket.ReadPacket(payload);
                        if (header.MessageType is MessageType.IdAssignment)
                        {
                            Id = MemoryMarshal.Read<int>(payload);
                            Debug.Log($"Channel received and ID of {Id}");
                            m_IdAssignmentTaskSource.SetResult(Id);
                            continue;
                        }

                        // Debug.Log($"Datachannel received packet [{header.PayLoadSize}]");
                        receivedHandler(in header, payload[..header.PayLoadSize]);
                    }
                    catch (OperationCanceledException)
                    {
                        // Don't need to bubble up cancellations
                    }
                    catch (Exception ex)
                    {
                        CurrentException = ex;
                        break;
                    }
                }
            }, token).ContinueWith(_ =>
            {
                Debug.Log($"{nameof(ReceivePacketsAsync)} exited");
            }, token);
        }

        public void Dispose()
        {
            m_Socket?.Dispose();
            if (m_OutgoingPackets != null)
            {
                while (m_OutgoingPackets.TryTake(out var packet))
                {
                    packet.Dispose();
                }
            }
            m_OutgoingPackets?.Dispose();
            m_OutgoingPackets = null;
            m_CancellationTokenSource?.Dispose();
        }
    }

    static class PacketTransport
    {
        static readonly int k_HeaderSize = Marshal.SizeOf<PacketHeader>();

        public static PacketHeader ReadPacket(this Socket socket, Span<byte> payloadDataOut)
        {
            var bytesRead = 0;
            Span<byte> headerDataBuffer = stackalloc byte[k_HeaderSize];
            while (bytesRead < k_HeaderSize)
            {
                bytesRead += socket.Receive(headerDataBuffer[bytesRead..k_HeaderSize]);
            }

            var header = MemoryMarshal.Read<PacketHeader>(headerDataBuffer);
            bytesRead = 0;
            while (bytesRead < header.PayLoadSize)
            {
                bytesRead += socket.Receive(payloadDataOut[bytesRead..header.PayLoadSize]);
            }

            return header;
        }

        public static void SendPacket(this Socket socket, in PacketHeader header, ReadOnlySpan<byte> payload)
        {
            socket.Send(MemoryExtensions.CreateReadOnlySpan(in header).AsBytes());
            socket.Send(payload[..header.PayLoadSize], SocketFlags.None);
        }
    }
}
