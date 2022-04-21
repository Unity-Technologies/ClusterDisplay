using System;
using Unity.ClusterDisplay.EmitterStateMachine;
using Unity.ClusterDisplay.RepeaterStateMachine;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Hardware synchronization state.
    /// </summary>
    /// <remarks>
    /// This state should be executed before the handshake.
    /// <see cref="HardwareSyncInitState"/> is a "null" state (does nothing).
    /// Derive from this class and return an instance of the child in
    /// <see cref="Create"/> to perform actual initialization of hardware.
    /// </remarks>
    class HardwareSyncInitState : NodeState
    {
        public static NodeState Create(IClusterSyncState clusterSyncState)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return CommandLineParser.disableQuadroSync.Value
                ? new HardwareSyncInitState(clusterSyncState)
                : new QuadroSyncInitState(clusterSyncState);
#else
            return new HardwareSyncInitState(clusterSyncState);
#endif
        }

        protected HardwareSyncInitState(IClusterSyncState clusterSync)
            : base(clusterSync) { }

        protected override NodeState DoFrame(bool newFrame)
        {
            return clusterSync.LocalNode is EmitterNode
                ? new WaitingForAllClients(clusterSync)
                : new RegisterWithEmitter(clusterSync);
        }
    }
}
