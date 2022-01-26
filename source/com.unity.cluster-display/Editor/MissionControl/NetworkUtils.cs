using System;
using System.Net;
using System.Net.Sockets;

namespace Unity.ClusterDisplay.MissionControl
{
    static class NetworkUtils
    {
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
