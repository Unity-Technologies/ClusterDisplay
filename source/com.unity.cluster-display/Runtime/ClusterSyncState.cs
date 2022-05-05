using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public interface IClusterSyncState
    {
        bool IsEmitter { get; }
        bool EmitterIsHeadless { get; }
        bool IsRepeater { get; }
        bool IsClusterLogicEnabled { get; }
        bool IsTerminated { get; }
        ulong Frame { get; }
        ushort NodeID { get; }

        string GetDiagnostics();
    }

    partial class ClusterSync : IClusterSyncState
    {
        public bool IsEmitter { get; private set; }

        public bool EmitterIsHeadless { get; private set; }

        public bool IsRepeater { get; private set; }

        public bool IsClusterLogicEnabled { get; private set; }

        public bool IsTerminated { get; private set; }
        public ulong Frame => LocalNode.CurrentFrameID;

        public ushort NodeID => LocalNode.NodeID;
    }
}
