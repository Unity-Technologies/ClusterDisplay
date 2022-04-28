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
        public override bool ReadyToProceed => false;
        public override bool ReadyForNextFrame => false;
        
        public static NodeState Create(ClusterNode node, bool hasQuadroSync)
        {
            return hasQuadroSync
                ? new QuadroSyncInitState(node)
                : new HardwareSyncInitState(node);
        }

        protected HardwareSyncInitState(ClusterNode localNode)
            : base(localNode) { }

        protected override NodeState DoFrame(bool newFrame)
        {
            return LocalNode is EmitterNode
                ? new WaitingForAllClients(LocalNode)
                : new RegisterWithEmitter(LocalNode);
        }

    }
}
