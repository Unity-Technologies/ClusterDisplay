using System;
using System.Net;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    public enum MessageType : byte
    {
        Discovery,
        NodeStatus,
        Launch,
        Kill
    }


    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public readonly struct KillInfo
    {
        
    }
    
    /// <summary>
    /// A header that describes a message: the type and the end point
    /// of the source.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public readonly struct MessageHeader
    {
        public readonly MessageType Type;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.MaxHostNameLength)]
        public readonly string HostName;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        
        public readonly byte[] AddressBytes;
        public readonly int Port;
        
        public MessageHeader(MessageType type, string hostName, IPEndPoint endPoint)
        {
            Type = type;
            HostName = hostName;
            AddressBytes = endPoint.Address.GetAddressBytes();
            Port = endPoint.Port;
        }

        public IPAddress Address => new(AddressBytes);

        public IPEndPoint EndPoint => new(Address, Port);

        public override string ToString() => $"[{HostName}, {EndPoint}]";
    }
}