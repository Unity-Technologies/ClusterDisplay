using System;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public readonly struct LaunchInfo
    {
        const int k_DefaultTxPort = 25690;
        const int k_DefaultRxPort = 25689;
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
        public readonly string PlayerDir;

        public readonly bool ClearRegistry;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public readonly string ExtraArgs;

        public readonly bool UseDeprecatedArgNames;

        public LaunchInfo(string playerDir,
            int id,
            int numRepeaters,
            bool clearRegistry = false,
            int handshakeTimeout = 10000,
            int commTimeout = 5000,
            string extraArgs = null,
            bool useDeprecatedArgNames = false)
        {
            PlayerDir = playerDir;
            NodeID = id;
            NumRepeaters = numRepeaters;
            HandshakeTimeoutMilliseconds = handshakeTimeout;
            TimeoutMilliseconds = commTimeout;

            MulticastAddress = k_DefaultAddress;
            TxPort = k_DefaultTxPort;
            RxPort = k_DefaultRxPort;
            ClearRegistry = clearRegistry;
            ExtraArgs = extraArgs;
            UseDeprecatedArgNames = useDeprecatedArgNames;
        }
    }
}
