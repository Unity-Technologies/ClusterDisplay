using System.Collections.Generic;
using Unity.ClusterDisplay.RepeaterStateMachine;
#if !UNITY_EDITOR
using UnityEngine;
#endif

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Class for the objects representing a repeater cluster node (a node receiving its state an emitter node).
    /// </summary>
    class RepeaterNode : ClusterNode
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">Node's configuration.</param>
        /// <param name="udpAgent">Object through which we will perform communication with other nodes in the cluster.
        /// Must support reception of <see cref="ReceiveMessageTypes"/>.</param>
        public RepeaterNode(ClusterNodeConfig config, IUdpAgent udpAgent)
            : base(config, udpAgent)
        {
            SetInitialState(Config.Fence is FrameSyncFence.Hardware ?
                HardwareSyncInitState.Create(this) : new RegisterWithEmitterState(this));
        }

        /// <summary>
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        public static IReadOnlyCollection<MessageType> ReceiveMessageTypes => s_ReceiveMessageTypes;

        /// <summary>
        /// Overridable method called to trigger quit of the application / game
        /// </summary>
        public virtual void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        static MessageType[] s_ReceiveMessageTypes = {MessageType.RepeaterRegistered,
            MessageType.FrameData, MessageType.EmitterWaitingToStartFrame, MessageType.PropagateQuit};
    }
}
