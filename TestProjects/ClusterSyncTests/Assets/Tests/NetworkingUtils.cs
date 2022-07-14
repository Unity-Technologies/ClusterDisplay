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
        public static NetworkInterface SelectNic()
        {
            // Assume that the first operational interface is capable of multicast.
            // This is similar to the logic that UdpAgent uses to select an interface when none is specified,
            // so it should pick the same interface as the UdpClients that we're using in this test
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .FirstOrDefault(nic => nic.OperationalStatus == OperationalStatus.Up && nic.SupportsMulticast);
        }
    }
}
