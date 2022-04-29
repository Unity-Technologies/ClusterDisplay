using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public static class ClusterDisplayState
    {
        public class IsEmitterMarker : Attribute {}

        /// <summary>
        /// This property returns true if this running instance is a emitter node, this is set to true or false in ClusterSync.
        /// </summary>
        [IsEmitterMarker]
        public static bool IsEmitter => ClusterSync.Instance.state.IsEmitter;

        /// <summary>
        /// This property returns true if this running instance is a emitter node AND is headless.
        /// </summary>
        public static bool EmitterIsHeadless => ClusterSync.Instance.state.EmitterIsHeadless;
        
        /// <summary>
        /// This property returns true if this running instance is a repeater node, this is set to true or false in ClusterSync.
        /// </summary>
        public static bool IsRepeater => ClusterSync.Instance.state.IsRepeater;

        /// <summary>
        /// Enables or disables the Cluster Display Synchronization. Beware that once the logic is disabled, it cannot be reenabled without restarting the application.
        /// </summary>
        public static bool IsClusterLogicEnabled => ClusterSync.Instance.state.IsClusterLogicEnabled;

        /// <summary>
        /// Getter that returns if there exists a ClusterSync instance and the synchronization has been enabled.
        /// </summary>
        public static bool IsActive => ClusterSync.Instance.state.IsActive;

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// </summary>
        public static bool IsTerminated => ClusterSync.Instance.state.IsTerminated;

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// </summary>
        public static ulong Frame => ClusterSync.Instance.state.Frame;

        public static ushort NodeID => ClusterSync.Instance.state.NodeID;
    }
}
