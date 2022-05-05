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
        public class IsEmitterMarker : Attribute {}

        /// <summary>
        /// This property returns true if this running instance is a emitter node, this is set to true or false in ClusterSync.
        /// </summary>
        [IsEmitterMarker]
        public static bool IsEmitter => ServiceLocator.Get<IClusterSyncState>().IsEmitter;

        /// <summary>
        /// This property returns true if this running instance is a emitter node AND is headless.
        /// </summary>
        public static bool EmitterIsHeadless => ServiceLocator.Get<IClusterSyncState>().EmitterIsHeadless;

        /// <summary>
        /// This property returns true if this running instance is a repeater node, this is set to true or false in ClusterSync.
        /// </summary>
        public static bool IsRepeater => ServiceLocator.Get<IClusterSyncState>().IsRepeater;

        /// <summary>
        /// Enables or disables the Cluster Display Synchronization. Beware that once the logic is disabled, it cannot be reenabled without restarting the application.
        /// </summary>
        public static bool IsClusterLogicEnabled => ServiceLocator.Get<IClusterSyncState>().IsClusterLogicEnabled;

        /// <summary>
        /// Getter that returns if there exists a ClusterSync instance and the synchronization has been enabled.
        /// </summary>
        public static bool IsActive => ServiceLocator.TryGet<IClusterSyncState>(out var clusterSync) && clusterSync.IsClusterLogicEnabled;

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// </summary>
        public static bool IsTerminated => ServiceLocator.Get<IClusterSyncState>().IsTerminated;

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// </summary>
        public static ulong Frame => ServiceLocator.Get<IClusterSyncState>().Frame;

        public static ushort NodeID => ServiceLocator.Get<IClusterSyncState>().NodeID;
    }
}
