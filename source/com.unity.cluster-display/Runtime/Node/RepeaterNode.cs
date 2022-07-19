using System.Collections.Generic;
using Unity.ClusterDisplay.RepeaterStateMachine;

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
            SetInitialState(Config.EnableHardwareSync ?
                HardwareSyncInitState.Create(this) : new RegisterWithEmitterState(this));
        }

        /// <summary>
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        public static IReadOnlyCollection<MessageType> ReceiveMessageTypes => s_ReceiveMessageTypes;

        /// <summary>
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        static MessageType[] s_ReceiveMessageTypes = {MessageType.RepeaterRegistered,
            MessageType.FrameData, MessageType.EmitterWaitingToStartFrame};
    }
}
