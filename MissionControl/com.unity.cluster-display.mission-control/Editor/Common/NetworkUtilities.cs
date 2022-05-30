using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// A class containing methods for common networking related operations.
    /// </summary>
    static class NetworkUtilities
    {
        /// <summary>
        /// Get the online IPv4 addresses from all network interfaces in the system.
        /// </summary>
        /// <param name="includeLoopback">Include any addresses on the loopback interface.</param>
        /// <returns>A new array containing the available IP addresses.</returns>
        public static IEnumerable<IPAddress> GetIPAddresses(bool includeLoopback)
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!includeLoopback && networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                switch (networkInterface.OperationalStatus)
                {
                    case OperationalStatus.Up:
                        break;
                    case OperationalStatus.Unknown:
                        // On Linux the loopback interface reports as unknown status, so we get it anyways
                        if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                            break;
                        continue;
                    default:
                        continue;
                }

                foreach (var ip in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    var address = ip.Address;

                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        yield return address;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the interface used to communicate with the specified remote.
        /// </summary>
        /// <param name="remoteEndPoint">The end point to connect to.</param>
        /// <returns>The IP address of the interface to use.</returns>
        public static IPAddress GetRoutingInterface(IPEndPoint remoteEndPoint)
        {
            // The routing table is the most correct method to determine the interface to use, but
            // in case there is an error we call back to an alternate method that is less reliable.
            try
            {
                return QueryRoutingInterface(null, remoteEndPoint);
            }
            catch (Exception)
            {
                return FindClosestAddresses(remoteEndPoint).localAddress;
            }
        }

        /// <summary>
        /// Looks up the interface used to communicate with the specified remote from the routing table.
        /// </summary>
        /// <param name="socket">The socket used for the lookup query. If null, a temporary socket is used.</param>
        /// <param name="remoteEndPoint">The end point to connect to.</param>
        /// <returns>The IP address of the interface to use.</returns>
        public static IPAddress QueryRoutingInterface(Socket socket, IPEndPoint remoteEndPoint)
        {
            var address = remoteEndPoint.Serialize();

            var remoteAddrBytes = new byte[address.Size];
            var localAddrBytes = new byte[address.Size];

            for (var i = 0; i < address.Size; i++)
            {
                remoteAddrBytes[i] = address[i];
            }

            if (socket != null)
            {
                socket.IOControl(IOControlCode.RoutingInterfaceQuery, remoteAddrBytes, localAddrBytes);
            }
            else
            {
                using var tempSocket = CreateSocket(ProtocolType.Udp);
                tempSocket.IOControl(IOControlCode.RoutingInterfaceQuery, remoteAddrBytes, localAddrBytes);
            }

            for (var i = 0; i < address.Size; i++)
            {
                address[i] = localAddrBytes[i];
            }

            return ((IPEndPoint)remoteEndPoint.Create(address)).Address;
        }

        /// <summary>
        /// Finds the local IP address and remote IP address that share the largest prefix.
        /// </summary>
        /// <remarks>
        /// IP addresses that share a prefix are likely to be on the same subnet.
        /// </remarks>
        /// <param name="remoteEndPoints">The remote IP addresses to pick from.</param>
        /// <returns>A tuple containing the most similar local IP address and remote IP address pair, or
        /// IPAddress.Any and null if no suitable pair was found.</returns>
        public static (IPAddress localAddress, IPEndPoint remoteEndPoint) FindClosestAddresses(params IPEndPoint[] remoteEndPoints)
        {
            // only match local host exactly
            foreach (var remoteEndPoint in remoteEndPoints)
            {
                if (remoteEndPoint.Equals(IPAddress.Loopback))
                    return (IPAddress.Loopback, remoteEndPoint);
            }

            // find the most similar non-loopback interface address
            var bestMatchLength = 0;
            var bestLocalIP = IPAddress.Any;
            var bestRemote = default(IPEndPoint);

            foreach (var localIP in GetIPAddresses(false))
            {
                foreach (var remoteEndPoint in remoteEndPoints)
                {
                    var matchLength = CompareIPAddresses(localIP, remoteEndPoint.Address);

                    if (bestMatchLength < matchLength)
                    {
                        bestMatchLength = matchLength;
                        bestLocalIP = localIP;
                        bestRemote = remoteEndPoint;
                    }
                }
            }

            return (bestLocalIP, bestRemote);
        }

        /// <summary>
        /// Gets an IPv4 address as an integer.
        /// </summary>
        /// <param name="address">An IPv4 address to get the bits for.</param>
        /// <returns>The IP address bits.</returns>
        public static uint GetAddressBits(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException($"{address.AddressFamily} addresses are not supported!", nameof(address));

            var bytes = address.GetAddressBytes();

            System.Diagnostics.Debug.Assert(bytes.Length == 4); // Since address.AddressFamily == AddressFamily.InterNetwork
            uint bits = BitConverter.ToUInt32(bytes);

            // the address is given in big-endian (network order), so need to convert back to the platform endianness
            bits = (uint)IPAddress.NetworkToHostOrder((int)bits);

            return bits;
        }

        /// <summary>
        /// Counts the matching leading bits of two IPv4 addresses.
        /// </summary>
        /// <param name="a">The first IP address.</param>
        /// <param name="b">The second IP address.</param>
        /// <returns>The length of the shared prefix.</returns>
        public static int CompareIPAddresses(IPAddress a, IPAddress b)
        {
            var aBits = GetAddressBits(a);
            var bBits = GetAddressBits(b);

            var matchingBits = 0;
            do
            {
                var shift = 31 - matchingBits;

                if ((aBits >> shift) != (bBits >> shift))
                    break;

                matchingBits++;
            }
            while (matchingBits < 32);

            return matchingBits;
        }

        /// <summary>
        /// Creates a new socket.
        /// </summary>
        /// <param name="protocol">The protocol to use on the created socket.</param>
        /// <returns>The new socket instance.</returns>
        public static Socket CreateSocket(ProtocolType protocol)
        {
            SocketType type;

            switch (protocol)
            {
                case ProtocolType.Tcp:
                    type = SocketType.Stream;
                    break;
                case ProtocolType.Udp:
                    type = SocketType.Dgram;
                    break;
                default:
                    throw new ArgumentException("Only TCP or UDP are supported", nameof(protocol));
            }

            return new Socket(AddressFamily.InterNetwork, type, protocol);
        }
    }
}
