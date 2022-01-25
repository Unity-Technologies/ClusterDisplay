using System;
using System.Net;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    public enum NodeStatus : byte
    {
        Ready,
        SyncFiles,
        Running,
        Canceled,
        Error
    }
    
    /// <summary>
    /// A struct that contains information about a cluster node.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public readonly struct NodeInfo : IEquatable<NodeInfo>
    {
        public readonly NodeStatus Status;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.LogMaxLength)]
        public readonly string LogMessage;
        
        public NodeInfo(NodeStatus status, string logMessage = null)
        {
            Status = status;
            LogMessage = logMessage;
        }

        public bool Equals(NodeInfo other)
        {
            return Status == other.Status;
        }

        public override bool Equals(object obj)
        {
            return obj is NodeInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int) Status;
        }

        public static bool operator ==(NodeInfo left, NodeInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NodeInfo left, NodeInfo right)
        {
            return !left.Equals(right);
        }
    }
}
