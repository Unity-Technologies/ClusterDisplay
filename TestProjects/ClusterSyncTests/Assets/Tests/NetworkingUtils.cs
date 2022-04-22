using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay.Tests
{
    static class NetworkingUtils
    {
        static readonly int k_HeaderSize = Marshal.SizeOf<MessageHeader>();

        public const int receiveTimeout = 5000;
        
        public static BitVector ToMask(this IEnumerable<byte> ids) => ids.Aggregate(new BitVector(), (mask, id) => mask.SetBit(id));

        public static (MessageHeader header, T contents) ReceiveMessage<T>(this UDPAgent agent, int timeout = receiveTimeout) where T : unmanaged
        {
            return agent.NextAvailableRxMsg(out var header,
                out var outBuffer,
                timeout)
                ? (header, outBuffer.LoadStruct<T>(k_HeaderSize))
                : default;
        }

        public static (MessageHeader header, T contents, byte[] extraData) ReceiveMessageWithData<T>(this UDPAgent agent, int timeout = receiveTimeout) where T : unmanaged
        {
            if (agent.NextAvailableRxMsg(out var header, out var outBuffer, timeout))
            {
                var msgLength = k_HeaderSize + Marshal.SizeOf<T>();
                var extraData = new byte[outBuffer.Length - msgLength];

                Array.Copy(sourceArray: outBuffer,
                    sourceIndex: msgLength,
                    destinationArray: extraData,
                    destinationIndex: 0,
                    length: extraData.Length);

                return (header, outBuffer.LoadStruct<T>(k_HeaderSize), extraData);
            }

            return default;
        }

        public static async ValueTask<(MessageHeader header, T contents)> ReceiveMessageAsync<T>(this UDPAgent agent, int timeout = receiveTimeout) where T : unmanaged
        {
            return await Task.Run(() => agent.ReceiveMessage<T>(timeout));
        }

        public static async ValueTask<(MessageHeader header, T contents)> ReceiveMessageAsync<T>(this UdpClient client, int timeoutMilliseconds = -1) where T : unmanaged
        {
            if (timeoutMilliseconds > 0)
            {
                var delay = Task.Run(async () =>
                {
                    await Task.Delay(timeoutMilliseconds);
                    return default(UdpReceiveResult);
                });
                var receive = client.ReceiveAsync();
                var completedTask = await Task.WhenAny(delay, receive);
                var result = await completedTask;
                if (result.Buffer == null)
                {
                    throw new TimeoutException("The read operation timed out.");
                }

                return (result.Buffer.LoadStruct<MessageHeader>(), result.Buffer.LoadStruct<T>(k_HeaderSize));
            }
            else
            {
                var result = await client.ReceiveAsync();
                return (result.Buffer.LoadStruct<MessageHeader>(), result.Buffer.LoadStruct<T>(k_HeaderSize));
            }
        }

        public static (MessageHeader header, byte[] rawMsg) GenerateMessage<T>(byte originId,
            IEnumerable<byte> destinations,
            EMessageType messageType,
            T contents,
            MessageHeader.EFlag flags = MessageHeader.EFlag.None,
            byte[] extraData = null) where T : unmanaged
        {
            // Generate and send message
            var header = new MessageHeader
            {
                SequenceID = 10,
                MessageType = messageType,
                DestinationIDs = destinations.ToMask(),
                OriginID = originId,
                PayloadSize = (ushort) Marshal.SizeOf<T>(),
                OffsetToPayload = (ushort) k_HeaderSize,
                Flags = flags
            };

            var contentSize = Marshal.SizeOf<T>();
            var bufferLen = k_HeaderSize + contentSize + (extraData?.Length ?? 0);
            var buffer = new byte[bufferLen];

            header.StoreInBuffer(buffer);
            contents.StoreInBuffer(buffer, k_HeaderSize);
            extraData?.CopyTo(buffer, k_HeaderSize + contentSize);

            return (header, buffer);
        }

        public static (MessageHeader header, byte[] rawMsg) GenerateTestMessage(byte originId, IEnumerable<byte> destinations)
        {
            return GenerateMessage(originId,
                destinations,
                EMessageType.EnterNextFrame,
                new RepeaterEnteredNextFrame
                {
                    FrameNumber = 5
                });
        }

        /// <summary>
        /// Create a client that is able to listen to receive multicast messages
        /// </summary>
        /// <param name="multicastAddress">Multicast address to listen on</param>
        /// <param name="rxPort">Receive port number</param>
        /// <param name="localAddress">The IP address assigned to the interface that we'd like to use</param>
        /// <returns></returns>
        public static UdpClient CreateClient(string multicastAddress, int rxPort, IPAddress localAddress)
        {
            Debug.Log(localAddress);

            var client = new UdpClient();
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, localAddress.GetAddressBytes());

            client.Client.Bind(new IPEndPoint(IPAddress.Any, rxPort));
            client.JoinMulticastGroup(IPAddress.Parse(multicastAddress));
            return client;
        }

        public static async Task<int> SendAck(this UdpClient client, IPEndPoint agentEndPoint, byte agentId, byte originId)
        {
            var ackBuffer = new byte[k_HeaderSize];
            var ackHeader = new MessageHeader
            {
                MessageType = EMessageType.AckMsgRx,
                DestinationIDs = BitVector.FromIndex(agentId),
                OriginID = originId
            };

            ackHeader.StoreInBuffer(ackBuffer);
            return await client.SendAsync(ackBuffer, ackBuffer.Length, agentEndPoint);
        }

        public static NetworkInterface SelectNic()
        {
            // Assume that the first operational interface is capable of multicast.
            // This is similar to the logic that UDPAgent uses to select an interface when none is specified,
            // so it should pick the same interface as the UdpClients that we're using in this test
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .FirstOrDefault(nic => nic.OperationalStatus == OperationalStatus.Up && nic.SupportsMulticast);
        }
    }
}
