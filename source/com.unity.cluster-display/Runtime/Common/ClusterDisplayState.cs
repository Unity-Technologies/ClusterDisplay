using System;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Convenience class for accessing <see cref="IClusterSyncState"/> properties. Values are obtained
    /// from the currently active <see cref="ClusterSync"/> from the <see cref="ServiceLocator"/>.
    /// </summary>
    /// <remarks>
    /// With the exception of <see cref="IsActive"/>, properties will throw exceptions if there
    /// is no instance registered with <see cref="ServiceLocator"/>. This class is intended to
    /// be used in conjunction with <see cref="ClusterDisplayManager"/> which automatically
    /// initializes and registers a <see cref="ClusterSync"/> instance.
    /// Consider using <code>ServiceLocator.TryGet&lt;IClusterSyncState&gt;()</code> instead if you are unsure.
    /// </remarks>
    public static class ClusterDisplayState
    {
        public class IsEmitterMarker : Attribute { }

        /// <summary>
        /// Get the role of the current node (whether it is the emitter or the repeater).
        /// This will return NodeRole.Unassigned if the cluster is not running.
        /// </summary>
        public static NodeRole NodeRole => ServiceLocator.TryGet<IClusterSyncState>(out var state) ? state.NodeRole : NodeRole.Unassigned;

        /// <summary>
        /// This property returns true if this running instance is a emitter node, this is set to true or false in ClusterSync
        /// This will return TRUE if the cluster is not running.
        /// </summary>
        [IsEmitterMarker]
        // [Obsolete("The property is deprecated. Use NodeRole instead")]
        public static bool IsEmitter => ServiceLocator.TryGet<IClusterSyncState>(out var state) ? state.NodeRole is NodeRole.Emitter : true;

        /// <summary>
        /// This property returns true if this running instance is a emitter node AND is headless.
        /// This will return FALSE by default if the cluster is not running.
        /// </summary>
        public static bool EmitterIsHeadless => ServiceLocator.TryGet<IClusterSyncState>(out var state) && state.EmitterIsHeadless;

        /// <summary>
        /// This property returns true if this running instance is a repeater node, this is set to true or false in ClusterSync.
        /// This will return FALSE if the cluster is not running.
        /// </summary>
        // [Obsolete("The property is deprecated. Use NodeRole instead")]
        public static bool IsRepeater => ServiceLocator.TryGet<IClusterSyncState>(out var state) ? state.NodeRole is NodeRole.Repeater : false;

        /// <summary>
        /// Enables or disables the Cluster Display Synchronization. Beware that once the logic is disabled, it cannot be reenabled without restarting the application.
        /// This will return FALSE by default if the cluster is not running.
        /// </summary>
        public static bool IsClusterLogicEnabled => ServiceLocator.TryGet<IClusterSyncState>(out var state) && state.IsClusterLogicEnabled;

        /// <summary>
        /// Getter that returns if there exists a ClusterSync instance and the synchronization has been enabled.
        /// </summary>
        public static bool IsActive => ServiceLocator.TryGet<IClusterSyncState>(out var clusterSync) && clusterSync.IsClusterLogicEnabled;

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// </summary>
        public static bool IsTerminated => ServiceLocator.TryGet<IClusterSyncState>(out var state) && state.IsTerminated;

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// This will return 0 if the cluster is not running.
        /// </summary>
        public static ulong Frame => ServiceLocator.TryGet<IClusterSyncState>(out var state) ? state.Frame : 0;

        /// <summary>
        /// You can use this ID to identify which unique instance is executing. 
        /// This will return 0 (emitter) if the cluster is not running.
        /// </summary>
        public static ushort NodeID => (ushort)(ServiceLocator.TryGet<IClusterSyncState>(out var state) ? state.NodeID : 0);
    }
}

