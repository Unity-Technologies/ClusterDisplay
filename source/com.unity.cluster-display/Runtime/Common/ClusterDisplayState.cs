using System;
using Unity.ClusterDisplay.Utils;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Convenience class for accessing <see cref="IClusterSyncState"/> properties. Values are obtained
    /// from the currently active <see cref="ClusterSync"/> from the <see cref="ServiceLocator"/>.
    /// </summary>
    /// <remarks>
    /// With the exception of <see cref="GetIsActive"/>, properties will throw exceptions if there
    /// is no active instance (registered with <see cref="ServiceLocator"/>).
    /// Consider using <code>ServiceLocator.TryGet&lt;IClusterSyncState&gt;()</code> instead if you are unsure.
    /// </remarks>
    public static class ClusterDisplayState
    {
        /// <summary>
        /// Get the role of the current node (whether it is the emitter or the repeater).
        /// </summary>
        /// <returns>
        /// Either NodeRole.Emitter or NodeRole.Repeater if cluster synchronization is available. Otherwise this will return NodeRole.Unassigned if the cluster is not running.
        /// </returns>
        public static NodeRole GetNodeRole() => ServiceLocator.TryGet<IClusterSyncState>(out var state) ? state.NodeRole : NodeRole.Unassigned;

        /// <summary>
        /// This property returns true if this running instance is a emitter node, this is set to true or false in ClusterSync
        /// </summary>
        /// <returns>
        /// True if the cluster synchronization is available, otherwise it will return false.
        /// </returns>
        [Obsolete("The property is deprecated. Use NodeRole instead")]
        public static bool IsEmitter => ServiceLocator.Get<IClusterSyncState>().NodeRole is NodeRole.Emitter;

        /// <summary>
        /// This property returns true if this running instance is a emitter node AND is headless.
        /// </summary>
        /// <returns>
        /// True if the cluster synchronization is available, otherwise it will return false.
        /// </returns>
        public static bool TryGetEmitterIsHeadless(out bool emitterIsHeadless)
        {
            if (!ServiceLocator.TryGet<IClusterSyncState>(out var state))
            {
                emitterIsHeadless = false;
                return false;
            }

            emitterIsHeadless = state.EmitterIsHeadless;
            return true;
        }

        /// <summary>
        /// This property returns true if this running instance is a repeater node, this is set to true or false in ClusterSync.
        /// </summary>
        /// <returns>
        /// True if the cluster synchronization is available, otherwise it will return false.
        /// </returns>
        [Obsolete("The property is deprecated. Use NodeRole instead")]
        public static bool IsRepeater => ServiceLocator.Get<IClusterSyncState>().NodeRole is NodeRole.Repeater;

        /// <summary>
        /// Getter that indicates whether cluster display logic is enabled and ready to connect, in contrast to <see cref="GetIsActive"/> which indicates that the cluster is connected and operating.
        /// </summary>
        public static bool GetIsClusterLogicEnabled () => ServiceLocator.TryGet<IClusterSyncState>(out var state) && state.IsClusterLogicEnabled;

        /// <summary>
        /// Getter that returns true if there exists a ClusterSync instance and the synchronization has been enabled.
        /// </summary>
        public static bool GetIsActive () => ServiceLocator.TryGet<IClusterSyncState>(out var clusterSync) && clusterSync.IsClusterLogicEnabled;

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// </summary>
        /// <returns>
        /// True if the cluster synchronization is available, otherwise it will return false.
        /// </returns>
        /// <param name="isTerminated">Boolean output that indicates whether the cluster was terminated or not.</param>
        public static bool TryGetIsTerminated (out bool isTerminated)
        {
            if (!ServiceLocator.TryGet<IClusterSyncState>(out var state))
            {
                isTerminated = true; // Describing to the user isTerminated == true if ClusterSync is not available.
                return false;
            }

            isTerminated = state.IsTerminated;
            return true;
        }

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// This will return 0 if the cluster is not running.
        /// </summary>
        /// <returns>
        /// True if the cluster synchronization is available, otherwise it will return false.
        /// </returns>
        /// <param name="frame">
        /// Outputs the current frame Id.
        /// </param>
        public static bool TryGetFrameId(out ulong frameId)
        {
            if (!ServiceLocator.TryGet<IClusterSyncState>(out var state))
            {
                frameId = 0;
                return false;
            }

            frameId = state.Frame;
            return true;
        }

        /// <summary>
        /// You can use this ID to identify which unique instance is executing.
        /// </summary>
        /// <returns>
        /// True if the cluster synchronization is available, otherwise it will return false.
        /// </returns>
        /// <param name="frame">
        /// Outputs the current node id, which will be 0 if cluster synchornization is not available.
        /// </param>
        public static bool TryGetRuntimeNodeId (out ushort nodeId)
        {
            if (!ServiceLocator.TryGet<IClusterSyncState>(out var state))
            {
                nodeId = 0;
                return false;
            }

            nodeId = state.NodeID;
            return true;
        }
    }
}

