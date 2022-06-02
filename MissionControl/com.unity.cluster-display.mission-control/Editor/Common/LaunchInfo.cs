using System;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public readonly struct LaunchInfo
    {
        const int k_DefaultPort = 25690;
        static readonly byte[] k_DefaultAddress = {224, 0, 1, 0};
        public readonly int NodeID;
        public readonly int NumRepeaters;
        public readonly int HandshakeTimeoutMilliseconds;
        public readonly int TimeoutMilliseconds;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly byte[] MulticastAddress;

        public readonly int Port;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.PathMaxLength)]
        public readonly string PlayerDir;

        public readonly bool ClearRegistry;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.MaxArgLength)]
        public readonly string ExtraArgs;

        public LaunchInfo(string playerDir,
            int id,
            int numRepeaters,
            bool clearRegistry = false,
            int handshakeTimeout = 10000,
            int commTimeout = 5000,
            string extraArgs = null,
            byte[] multicastAddress = null,
            int port = k_DefaultPort)
        {
            PlayerDir = playerDir;
            NodeID = id;
            NumRepeaters = numRepeaters;
            HandshakeTimeoutMilliseconds = handshakeTimeout;
            TimeoutMilliseconds = commTimeout;

            MulticastAddress = multicastAddress ?? k_DefaultAddress;
            Port = port;
            ClearRegistry = clearRegistry;
            ExtraArgs = extraArgs;
        }
    }
}
