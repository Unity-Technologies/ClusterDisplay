using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    public static class NetworkUtils
    {
        static readonly Dictionary<MessageType, Type> k_MessageToDataType = new()
        {
            {MessageType.Discovery, typeof(ServerInfo)},
            {MessageType.NodeStatus, typeof(NodeInfo)},
            {MessageType.Launch, typeof(LaunchInfo)},
            {MessageType.Kill, typeof(KillInfo)}
        };

        public static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }

            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        public static int WriteMessage<T>(this byte[] buffer,
            string hostName,
            IPEndPoint endPoint,
            in T messageData) where T : struct
        {
            MessageType type;
            if (typeof(T) == typeof(ServerInfo))
            {
                type = MessageType.Discovery;
            }
            else if (typeof(T) == typeof(NodeInfo))
            {
                type = MessageType.NodeStatus;
            }
            else if (typeof(T) == typeof(LaunchInfo))
            {
                type = MessageType.Launch;
            }
            else if (typeof(T) == typeof(KillInfo))
            {
                type = MessageType.Kill;
            }
            else
            {
                throw new ArgumentException("No message type associated with data");
            }

            var header = new MessageHeader(type, hostName, endPoint);
            var offset = buffer.WriteStruct(header);
            offset += buffer.WriteStruct(messageData, offset);
            return offset;
        }

        static bool MatchesType<T>(MessageType type)
        {
            return k_MessageToDataType[type] == typeof(T);
        }

        public static (MessageHeader header, T message)? MatchMessage<T>(this byte[] buffer) where T : struct
        {
            if (buffer.Length == 0 || !MatchesType<T>((MessageType) buffer[0]))
            {
                return null;
            }

            return (buffer.ReadStruct<MessageHeader>(), buffer.ReadStruct<T>(Marshal.SizeOf<MessageHeader>()));
        }

        /// <summary>
        /// Looks up the interface used to communicate with the specified remote from the routing table.
        /// </summary>
        /// <param name="remoteEndPoint">The end point to connect to.</param>
        /// <returns>The IP address of the interface to use.</returns>
        public static IPAddress QueryRoutingInterface(IPEndPoint remoteEndPoint)
        {
            var address = remoteEndPoint.Serialize();

            var remoteAddrBytes = new byte[address.Size];
            var localAddrBytes = new byte[address.Size];

            for (var i = 0; i < address.Size; i++)
            {
                remoteAddrBytes[i] = address[i];
            }

            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.IOControl(IOControlCode.RoutingInterfaceQuery, remoteAddrBytes, localAddrBytes);

            for (var i = 0; i < address.Size; i++)
            {
                address[i] = localAddrBytes[i];
            }

            return ((IPEndPoint) remoteEndPoint.Create(address)).Address;
        }
    }
}
