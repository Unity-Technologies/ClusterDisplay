using System;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Convenience class for accessing <see cref="ClusterSync"/> properties. Values are obtained
    /// from the currently active <see cref="ClusterSync"/> from the <see cref="ServiceLocator"/>.
    /// </summary>
    public static class ClusterDisplayState
    {
        public class IsEmitterMarker : Attribute {}

        /// <summary>
        /// This property returns true if this running instance is a emitter node, this is set to true or false in ClusterSync.
        /// </summary>
        [IsEmitterMarker]
        public static bool IsEmitter => ServiceLocator.Get<ClusterSync>().StateAccessor.IsEmitter;

        /// <summary>
        /// This property returns true if this running instance is a emitter node AND is headless.
        /// </summary>
        public static bool EmitterIsHeadless => ServiceLocator.Get<ClusterSync>().StateAccessor.EmitterIsHeadless;

        /// <summary>
        /// This property returns true if this running instance is a repeater node, this is set to true or false in ClusterSync.
        /// </summary>
        public static bool IsRepeater => ServiceLocator.Get<ClusterSync>().StateAccessor.IsRepeater;

        /// <summary>
        /// Enables or disables the Cluster Display Synchronization. Beware that once the logic is disabled, it cannot be reenabled without restarting the application.
        /// </summary>
        public static bool IsClusterLogicEnabled => ServiceLocator.Get<ClusterSync>().StateAccessor.IsClusterLogicEnabled;

        /// <summary>
        /// Getter that returns if there exists a ClusterSync instance and the synchronization has been enabled.
        /// </summary>
        public static bool IsActive => ServiceLocator.Get<ClusterSync>().StateAccessor.IsActive;

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// </summary>
        public static bool IsTerminated => ServiceLocator.Get<ClusterSync>().StateAccessor.IsTerminated;

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// </summary>
        public static ulong Frame => ServiceLocator.Get<ClusterSync>().StateAccessor.Frame;

        public static ushort NodeID => ServiceLocator.Get<ClusterSync>().StateAccessor.NodeID;
    }
}
