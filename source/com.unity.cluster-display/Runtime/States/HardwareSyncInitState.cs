using System;
using Unity.ClusterDisplay.EmitterStateMachine;
using Unity.ClusterDisplay.RepeaterStateMachine;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    class HardwareSyncInitState : NodeState
    {
        public static NodeState Create(IClusterSyncState clusterSyncState)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return new QuadroSyncInitState(clusterSyncState);
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
