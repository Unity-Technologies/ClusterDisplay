﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Tests
{
    static class NetworkingUtils
    {
        public static readonly int headerSize = Marshal.SizeOf<MessageHeader>();

        public static ulong ToMask(this byte id) => 1UL << id;

        public static ulong ToMask(this IEnumerable<byte> ids) => ids.Aggregate(0UL, (mask, id) => mask | ToMask((byte) id));

        public static async ValueTask<(MessageHeader header, T contents)> ReceiveMessage<T>(this UDPAgent agent, int timeout = 1000) where T : unmanaged
        {
            return await Task.Run(() =>
            {
                if (agent.RxWait.WaitOne(timeout * 1000) && agent.NextAvailableRxMsg(out var header, out var outBuffer))
                {
                    return (header, outBuffer.LoadStruct<T>(headerSize));
                }

                return default;
            });
        }

        public static async ValueTask<(MessageHeader header, T contents)> ReceiveMessage<T>(this UdpClient agent, int timeoutMilliseconds = -1) where T : unmanaged
        {
            if (timeoutMilliseconds > 0)
            {
                var delay = Task.Run(async () =>
                {
                    await Task.Delay(timeoutMilliseconds);
                    return default(UdpReceiveResult);
                });
                var receive = agent.ReceiveAsync();
                var completedTask = await Task.WhenAny(delay, receive);
                var result = await completedTask;
                if (result.Buffer == null)
                {
                    throw new TimeoutException("The read operation timed out.");
                }

                return (result.Buffer.LoadStruct<MessageHeader>(), result.Buffer.LoadStruct<T>(headerSize));
            }
            else
            {
                var result = await agent.ReceiveAsync();
                return (result.Buffer.LoadStruct<MessageHeader>(), result.Buffer.LoadStruct<T>(headerSize));
            }
        }
        
        public static (MessageHeader header, byte[] rawMsg) GenerateMessage<T>(byte originId,
            IEnumerable<byte> destinations,
            EMessageType messageType,
            T contents,
            MessageHeader.EFlag flags = MessageHeader.EFlag.None,
            int zeroPadding = 0) where T : unmanaged
        {
            // Generate and send message
            var header = new MessageHeader
            {
                MessageType = messageType,
                DestinationIDs = destinations.ToMask(),
                OriginID = originId,
                PayloadSize = (ushort) Marshal.SizeOf<T>(),
                OffsetToPayload = (ushort) headerSize,
                Flags = flags
            };

            var bufferLen = headerSize + Marshal.SizeOf<T>() + zeroPadding;
            var buffer = new byte[bufferLen];
            

            header.StoreInBuffer(buffer);
            contents.StoreInBuffer(buffer, headerSize);

            return (header, buffer);
        }

        public static (MessageHeader header, byte[] rawMsg) GenerateTestMessage(byte originId, IEnumerable<byte> destinations)
        {
            return GenerateMessage(originId,
                destinations,
                EMessageType.EnterNextFrame,
                new RepeaterEnteredNextFrame
                {
                    FrameNumber = 1
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
            var ackBuffer = new byte[headerSize];
            var ackHeader = new MessageHeader
            {
                MessageType = EMessageType.AckMsgRx,
                DestinationIDs = 1UL << agentId,
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
            return NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(nic => nic.OperationalStatus == OperationalStatus.Up);
        }

    }
}
