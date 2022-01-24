using System;
using System.Net;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public readonly struct LaunchInfo
    {
        static readonly byte[] k_DefaultAddress = {224, 0, 1, 0};
        public readonly int NodeID;
        public readonly int NumRepeaters;
        public readonly int HandshakeTimeoutMilliseconds;
        public readonly int TimeoutMilliseconds;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly byte[] MulticastAddress;

        /// <summary>
        /// Port to which ACK messages are sent
        /// </summary>
        public readonly int TxPort;

        /// <summary>
        /// Port for receiving sync messages
        /// </summary>
        public readonly int RxPort;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.PathMaxLength)]
        public readonly string ProjectDir;

        public LaunchInfo(string projectDir, int id, int numRepeaters, int handshakeTimeout = 10000, int commTimeout = 5000)
        {
            ProjectDir = projectDir;
            NodeID = id;
            NumRepeaters = numRepeaters;
            HandshakeTimeoutMilliseconds = handshakeTimeout;
            TimeoutMilliseconds = commTimeout;

            MulticastAddress = k_DefaultAddress;
            TxPort = 25690;
            RxPort = 25689;
        }
    }
}
