using System.Net;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    [StructLayout(LayoutKind.Sequential, Pack=0)]
    public readonly struct ServerInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly byte[] Address;
        
        public readonly int Port;
    }
}
