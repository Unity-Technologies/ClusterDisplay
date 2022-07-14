using System;
using System.Net;

namespace Unity.ClusterDisplay.Tests
{
    static class NodeTestUtils
    {
        public const int TestPort = 12345;
        public const string MulticastAddress = "224.0.1.0";
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        public static readonly string AdapterName = NetworkingUtils.SelectNic().Name;

        public static readonly UdpAgentConfig udpConfig = new()
        {
            MulticastIp = IPAddress.Parse(MulticastAddress),
            Port = TestPort,
            AdapterName = AdapterName
        };
    }
}
