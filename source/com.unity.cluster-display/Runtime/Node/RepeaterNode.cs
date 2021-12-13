﻿using System;
using Unity.ClusterDisplay.RepeaterStateMachine;

namespace Unity.ClusterDisplay
{
    internal class RepeaterNode : ClusterNode
    {
        public byte EmitterNodeId { get; set; }
        public UInt64 EmitterNodeIdMask => (UInt64) 1 << EmitterNodeId;

        public RepeaterNode(IClusterSyncState clusterSync, UDPAgent.Config config) : base(clusterSync, config) {}

        public override bool TryStart()
        {
            if (!base.TryStart())
                return false;

            m_CurrentState = new RegisterWithEmitter(clusterSync, this) {MaxTimeOut = ClusterParams.RegisterTimeout};
            m_CurrentState.EnterState(null);

            return true;
        }
    }
}