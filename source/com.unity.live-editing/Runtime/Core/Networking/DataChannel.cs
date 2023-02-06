using System;
using System.Buffers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.LiveEditing.LowLevel.Networking
{
    /// <summary>
    /// Packet metadata. Contains information necessary to read a packet from a stream.
    /// </summary>
    /// <remarks>
    /// For internal use only; required for passing messages using <see cref="DataChannel"/>.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    readonly struct PacketHeader
    {
        internal const int k_Revision = 0;
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
            return new(channelId, MessageType.IdAssignment, 0);
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
        /// <summary>
        /// A message containing a binary blob.
        /// </summary>
        Binary,
        /// <summary>
        /// A signal to assign the ID of a <see cref="DataChannel"/>.
        /// The <see cref="PacketHeader.ChannelId"/> field contains the ID we
        /// wish to use.
        /// </summary>
        IdAssignment
    }

    /// <summary>
    /// Low-level primitive for sending and receiving packets over a given socket.
    /// </summary>
    /// <remarks>
    /// A packet consists of a header and an optional byte array (the payload).
    /// </remarks>
    class DataChannel : IDisposable
    {
        public delegate void PacketReceivedHandler(in PacketHeader header, ReadOnlySpan<byte> payload);

        const int k_SendQueueMaxLength = 32;
        const int k_ReceiveBufferSize = 1024 * 64; // 64k should be enough for everybody ;)
        readonly Socket m_Socket;
        readonly Task m_ReceiveTask;
        readonly Task m_SendTask;

        BlockingQueue<QueuedPacket> m_OutgoingPackets;
        readonly CancellationTokenSource m_CancellationTokenSource;
        readonly TaskCompletionSource<int> m_IdAssignmentTaskSource = new();
        public string Name { get; set; }
        ArrayPool<byte> m_PacketQueueDataPool = ArrayPool<byte>.Create();

        /// <summary>
        /// Application-specific identifier.
        /// </summary>
        /// <remarks>
        /// The ID gets attached to all packets send using this channel. The interpretation
        /// of the ID depends on the application. For example <see cref="TcpMessageServer"/> uses
        /// it to route messages.<br/>
        /// To set the ID, use the <see cref="EnqueueIdChange"/> method to coordinate the change
        /// on both ends of the channel.
        /// </remarks>
        public int Id { get; private set; } = -1;

        /// <summary>
        /// Returns whether this connection has stopped sending and receiving packets.
        /// </summary>
        public bool HasStopped => m_ReceiveTask.IsCompleted || m_SendTask.IsCompleted;
        public Exception CurrentException { get; private set; }

        /// <summary>
        /// Creates a new channel on the specified socket.
        /// </summary>
        /// <param name="socket">The socket to use.</param>
        /// <param name="cancellationToken">Token used to cancel async reads and writes.</param>
        /// <param name="receivedHandler">Callback for handling the arrival of data.</param>
        /// <remarks>
        /// <p>The caller is responsible for creating the socket in a usable state such that it is able to send and
        /// receive data. <see cref="DataChannel"/> takes ownership of the <see cref="Socket"/>
        /// (i.e. may call <see cref="Socket.Dispose"/>).</p>
        /// </remarks>
        public DataChannel(Socket socket, CancellationToken cancellationToken, PacketReceivedHandler receivedHandler)
        {
            m_CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            m_Socket = socket;

            m_OutgoingPackets = new BlockingQueue<QueuedPacket>(k_SendQueueMaxLength);
            cancellationToken.Register(m_OutgoingPackets.CompleteAdding);
            m_ReceiveTask = ReceivePacketsAsync(receivedHandler, m_CancellationTokenSource.Token);
            m_SendTask = SendPacketsInQueueAsync(m_CancellationTokenSource.Token);
            Task.WhenAny(m_ReceiveTask, m_SendTask).ContinueWith(_ =>
            {
                m_CancellationTokenSource.Cancel();
            });
        }

        public async ValueTask<int> WaitForIdAssignment()
        {
            return await m_IdAssignmentTaskSource.Task;
        }

        /// <summary>
        /// Assign an ID to this channel.
        /// </summary>
        /// <param name="newId"></param>
        /// <remarks>
        /// Currently, you can only perform this operation once. TODO: Fix this limitation.
        /// </remarks>
        public void EnqueueIdChange(int newId)
        {
            Id = newId;
            var header = PacketHeader.CreateIdAssignment(newId);
            m_OutgoingPackets.TryEnqueue(CreateQueuePacket(in header, ReadOnlySpan<byte>.Empty));
            m_IdAssignmentTaskSource.SetResult(Id);
        }

        public void EnqueueSend(in PacketHeader header, ReadOnlySpan<byte> payload) =>
            m_OutgoingPackets.TryEnqueue(CreateQueuePacket(in header, payload));

        readonly struct QueuedPacket
        {
            public readonly PacketHeader Header;
            public readonly byte[] PayloadData;

            public QueuedPacket(in PacketHeader header, byte[] data)
            {
                Header = header;
                PayloadData = data;
            }
        }

        QueuedPacket CreateQueuePacket(in PacketHeader header, ReadOnlySpan<byte> data)
        {
            var dataCopy = m_PacketQueueDataPool.Rent(header.PayLoadSize);
            data[..header.PayLoadSize].CopyTo(dataCopy);
            return new QueuedPacket(in header, dataCopy);
        }

        void ReleaseQueuePacket(ref QueuedPacket packet)
        {
            m_PacketQueueDataPool.Return(packet.PayloadData);
        }

        Task SendPacketsInQueueAsync(CancellationToken token)
        {
            return Task.Run(() =>
            {
                Profiler.BeginSample(nameof(SendPacketsInQueueAsync));
                token.Register(m_OutgoingPackets.CompleteAdding);
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (m_OutgoingPackets.TryDequeue(out var packet, Timeout.InfiniteTimeSpan))
                        {
                            m_Socket.SendPacket(packet.Header, packet.PayloadData);
                            ReleaseQueuePacket(ref packet);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Don't bubble up cancellations
                }
                catch (Exception ex)
                {
                    CurrentException = ex;
                }
                finally
                {
                    Profiler.EndSample();
                }
            }, token);
        }

        Task ReceivePacketsAsync(PacketReceivedHandler receivedHandler, CancellationToken token)
        {
            return Task.Run(() =>
            {
                Profiler.BeginSample(nameof(ReceivePacketsAsync));
                Span<byte> payload = stackalloc byte[k_ReceiveBufferSize];
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var header = m_Socket.ReadPacket(payload);
                        if (header.MessageType is MessageType.IdAssignment)
                        {
                            Id = header.ChannelId;
                            Debug.Log($"Channel received ID assignment: {Id}");
                            m_IdAssignmentTaskSource.SetResult(Id);
                            continue;
                        }

                        receivedHandler(in header, payload[..header.PayLoadSize]);
                    }
                    catch (OperationCanceledException)
                    {
                        // Don't need to bubble up cancellations
                    }
                    catch (SocketException ex) when (ex.ErrorCode == (int)SocketError.Disconnecting)
                    {
                        // The other end has initiated a disconnection.
                        m_Socket.Shutdown(SocketShutdown.Both);
                        break;
                    }
                    catch (Exception ex)
                    {
                        CurrentException = ex;
                        break;
                    }
                }

                Profiler.EndSample();
            }, token);
        }

        /// <summary>
        /// Initiate a clean shutdown of the socket connection.
        /// </summary>
        /// <remarks>
        /// The socket will remain alive until the other end has acknowledged with its own
        /// shutdown signal.
        /// </remarks>
        public void StartShutdown()
        {
            m_Socket?.Shutdown(SocketShutdown.Send);
        }

        public void Dispose()
        {
            m_Socket?.Dispose();
            m_OutgoingPackets?.CompleteAdding();
            m_CancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// Extension methods for sending and receiving discrete packets over a socket.
    /// </summary>
    static class PacketTransportExtensions
    {
        static readonly int k_HeaderSize = Marshal.SizeOf<PacketHeader>();

        public static PacketHeader ReadPacket(this Socket socket, Span<byte> payloadDataOut)
        {
            var bytesRead = 0;

            // Use the Receive() overload that takes a byte[] because it doesn't implicitly allocate additional garbage.
            var headerDataBuffer = ArrayPool<byte>.Shared.Rent(k_HeaderSize);
            PacketHeader headerOut;
            try
            {
                while (bytesRead < k_HeaderSize)
                {
                    var result = socket.Receive(headerDataBuffer,
                        bytesRead,
                        k_HeaderSize - bytesRead,
                        SocketFlags.None);

                    if (result > 0)
                    {
                        bytesRead += result;
                    }
                    else
                    {
                        // We received a 0-byte packet which means the other end wishes to disconnect.
                        throw new SocketException((int)SocketError.Disconnecting);
                    }
                }

                headerOut = MemoryMarshal.Read<PacketHeader>(headerDataBuffer);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerDataBuffer);
            }

            if (headerOut.Revision != PacketHeader.k_Revision)
            {
                throw new InvalidCastException("Unknown message header revision.");
            }

            Debug.Assert(payloadDataOut.Length >= headerOut.PayLoadSize);
            bytesRead = 0;

            var payloadReceiveBuffer = ArrayPool<byte>.Shared.Rent(headerOut.PayLoadSize);
            try
            {
                while (bytesRead < headerOut.PayLoadSize)
                {
                    var result = socket.Receive(payloadReceiveBuffer,
                        bytesRead,
                        headerOut.PayLoadSize - bytesRead,
                        SocketFlags.None);

                    if (result > 0)
                    {
                        bytesRead += result;
                    }
                    else
                    {
                        // We received a 0-byte packet which means the other end has disconnected.
                        throw new SocketException((int)SocketError.Disconnecting);
                    }
                }

                payloadReceiveBuffer.CopyTo(payloadDataOut);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(payloadReceiveBuffer);
            }

            return headerOut;
        }

        public static void SendPacket(this Socket socket, in PacketHeader header, ReadOnlySpan<byte> payload)
        {
            // Create a buffer big enough for both the header and the payload
            var headerSize = Marshal.SizeOf<PacketHeader>();
            var temp = ArrayPool<byte>.Shared.Rent(header.PayLoadSize + headerSize);

            // Write the header and the payload into the temporary buffer
            MemoryMarshal.Write(temp, ref MemoryExtensions.AsRef(header));
            payload[..header.PayLoadSize].CopyTo(temp.AsSpan()[headerSize..]);
            try
            {
                // Use the Send() overload that takes a byte[] because it doesn't implicitly allocate additional garbage.
                socket.Send(temp, 0, headerSize + header.PayLoadSize, SocketFlags.None);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }
    }
}
