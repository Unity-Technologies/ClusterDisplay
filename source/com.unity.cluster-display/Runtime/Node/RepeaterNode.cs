using System;
using Unity.ClusterDisplay.RepeaterStateMachine;
using Unity.ClusterDisplay.Utils;

namespace Unity.ClusterDisplay
{
    class RepeaterNode : ClusterNode
    {
        public new struct Config
        {
            public ClusterNode.Config MainConfig;
            public bool EnableHardwareSync;
        }

        public byte EmitterNodeId { get; set; }
        public BitVector EmitterNodeIdMask => BitVector.FromIndex(EmitterNodeId);

        public RepeaterNode(Config config)
            : base(config.MainConfig)
        {
            m_CurrentState = config.EnableHardwareSync
                ? HardwareSyncInitState.Create(this)
                : new RegisterWithEmitter(this);
        }

        public override void Start()
        {
            base.Start();
            m_CurrentState.EnterState(null);
        }
    }
}
