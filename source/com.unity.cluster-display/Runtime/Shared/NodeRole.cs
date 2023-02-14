namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Enum containing the Cluster node roles.
    /// </summary>
    public enum NodeRole
    {
        /// <summary>
        /// The node does not have an assigned role (not operating as part of a cluster).
        /// </summary>
        Unassigned,
        /// <summary>
        /// The source node that broadcasts synchronization data.
        /// </summary>
        Emitter,
        /// <summary>
        /// The client nodes that receive synchronization data.
        /// </summary>
        Repeater,
        /// <summary>
        /// The nodes that are running like a repeater but that are not producing any valuable content, they are keeping
        /// their state up to date waiting for another node to fail so that they can take their place.
        /// </summary>
        Backup
    }
}
