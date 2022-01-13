using System;
using System.Net;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    enum NodeStatus : byte
    {
        Ready,
        SyncFiles,
        Running,
        Error
    }
    
    /// <summary>
    /// A struct that contains information about a server instance.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    readonly struct NodeInfo
    {
        public readonly NodeStatus Status;
        public NodeInfo(NodeStatus status)
        {
            Status = status;
        }
    }
}
