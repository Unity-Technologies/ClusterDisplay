using UnityEngine;

namespace Unity.ClusterDisplay
{
    public interface IClusterSyncState
    {
        string InstanceName { get; }
        NodeRole NodeRole { get; }
        bool EmitterIsHeadless { get; }
        bool IsClusterLogicEnabled { get; }
        bool IsTerminated { get; }
        ulong Frame { get; }
        /// <summary>
        /// Assigned node ID.
        /// </summary>
        byte NodeID { get; }
        /// <summary>
        /// Adjusted node ID for rendering.
        /// </summary>
        /// <remarks>
        /// This could differ from NodeID if not all nodes perform rendering (non-rendering nodes are skipped).
        /// </remarks>
        byte RenderNodeID { get; }
        bool RepeatersDelayedOneFrame { get; }

        string GetDiagnostics();
    }

    public partial class ClusterSync : IClusterSyncState
    {
        public NodeRole NodeRole { get; private set; }
        public bool EmitterIsHeadless { get; private set; }

        public bool IsClusterLogicEnabled { get; private set; }

        public bool IsTerminated { get; private set; }
        public ulong Frame => LocalNode.FrameIndex;

        public byte NodeID => LocalNode.Config.NodeId;
        public byte RenderNodeID { get; private set; }

        public bool RepeatersDelayedOneFrame { get; private set; }
    }
}
