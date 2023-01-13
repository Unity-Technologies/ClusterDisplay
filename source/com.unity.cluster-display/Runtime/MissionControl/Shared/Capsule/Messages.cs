using System;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    public static class MessagesId
    {
        public static readonly Guid Land = Guid.Parse("4CAC1688-72E9-48D4-9B5F-FECA1EDE162E");
        public static readonly Guid CapsuleStatus = Guid.Parse("06E5C6DC-435B-45A9-9836-E2972F5CDD10");
        public static readonly Guid ChangeClusterTopology = Guid.Parse("18362F16-CD14-4D30-9C93-0821EACC3A28");
    }

    /// <summary>
    /// Information associated to a <see cref="MessagesId.Land"/> message (asking the ClusterDisplay application to
    /// exit).
    /// </summary>
    public struct LandMessage
    {
    }

    /// <summary>
    /// Response to a <see cref="MessagesId.Land"/>.
    /// </summary>
    public struct LandResponse
    {
    }

    /// <summary>
    /// Start of the information associated to a <see cref="MessagesId.ChangeClusterTopology"/> message (asking the
    /// nodes to change to meet the new topology).  This is the header of the message followed by number of
    /// <see cref="ChangeClusterTopologyEntry"/>.
    /// </summary>
    public struct ChangeClusterTopologyMessageHeader
    {
        /// <summary>
        /// How many <see cref="ChangeClusterTopologyEntry"/> follow this
        /// <see cref="ChangeClusterTopologyMessageHeader"/>.  This array of entries describe the new state of the
        /// cluster, so a node not present in it indicate that the node is not part of the cluster anymore and should
        /// quit.
        /// </summary>
        public byte EntriesCount;
    }

    /// <summary>
    /// Describe the new role of a node in the cluster.
    /// </summary>
    public struct ChangeClusterTopologyEntry
    {
        /// <summary>
        /// NodeId of the node this entry describes.
        /// </summary>
        public byte NodeId;
        /// <summary>
        /// Role of the node.
        /// </summary>
        public NodeRole NodeRole;
        /// <summary>
        /// RenderNodeId of the node (might be different from NodeId).
        /// </summary>
        public byte RenderNodeId;
    }

    /// <summary>
    /// Response of a <see cref="MessagesId.ChangeClusterTopology"/> message.
    /// </summary>
    public struct ChangeClusterTopologyResponse
    {
    }

    /// <summary>
    /// Message sent from the capsule to capcom to inform of a change in status of the capsule.
    /// </summary>
    public struct CapsuleStatusMessage
    {
        public byte NodeId;
        public NodeRole NodeRole;
        public byte RenderNodeId;
    }

    /// <summary>
    /// Response to a <see cref="CapsuleStatusMessage"/>.
    /// </summary>
    public struct CapsuleStatusResponse
    {

    }
}
