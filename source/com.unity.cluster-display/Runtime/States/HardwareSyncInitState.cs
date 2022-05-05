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

        public static NodeState Create(ClusterNode node)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return new QuadroSyncInitState(node);
#else
            return new HardwareSyncInitState(node);
#endif
        }

        protected HardwareSyncInitState(ClusterNode localNode)
            : base(localNode)
        {
            ClusterDebug.LogWarning("Hardware synchronization is not available in this environment");
        }

        protected override NodeState DoFrame(bool newFrame) =>
            LocalNode switch {
                EmitterNode emitterNode => new WaitingForAllClients(emitterNode),
                RepeaterNode repeaterNode => new RegisterWithEmitter(repeaterNode),
                _ => throw new ArgumentOutOfRangeException(nameof(LocalNode))
            };
    }
}
