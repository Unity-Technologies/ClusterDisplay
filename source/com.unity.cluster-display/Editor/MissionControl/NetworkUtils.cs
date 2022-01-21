using System;
using System.Collections.Generic;
using System.Linq;
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
            {MessageType.SyncProject, typeof(ProjectSyncInfo)},
            {MessageType.Kill, typeof(KillInfo)}
        };

        static readonly Dictionary<Type, MessageType> k_DataTypeToMessage = k_MessageToDataType
            .ToDictionary(p => p.Value, p => p.Key);

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
            if (!k_DataTypeToMessage.TryGetValue(typeof(T), out var messageType))
            {
                return 0;
            }

            var header = new MessageHeader(messageType, hostName, endPoint);
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
