using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.EmitterStateMachine;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    internal class RemoteNodeComContext
    {
        public byte ID { get; set; }
        public NodeRole Role { get; set; }
    }

    class EmitterNode : ClusterNode
    {
        public int TotalExpectedRemoteNodesCount { get; set; }
        public bool RepeatersDelayed { get; set; }

        List<RemoteNodeComContext> m_RemoteNodes = new();
        public override bool HasHardwareSync
        {
            get => m_CurrentState is EmitterSynchronization {HasHardwareSync: true};
            set
            {
                if (m_CurrentState is EmitterSynchronization emitter)
                {
                    emitter.HasHardwareSync = value;
                }
            }
        }

        public IReadOnlyList<RemoteNodeComContext> RemoteNodes => m_RemoteNodes;

        public struct Config
        {
            public bool headlessEmitter;
            public bool repeatersDelayed;
            public int repeaterCount;
            public UDPAgent.Config udpAgentConfig;
            public bool enableHardwareSync;
        }

        public EmitterNode(Config config)
            : base(config.udpAgentConfig)
        {
            m_CurrentState = config.enableHardwareSync
                ? HardwareSyncInitState.Create(this)
                : new WaitingForAllClients(this);
            RepeatersDelayed = config.repeatersDelayed;
            TotalExpectedRemoteNodesCount = config.repeaterCount;
        }

        public override void Start()
        {
            base.Start();
            m_CurrentState.EnterState(null);
        }

        public int FindNodeByID(byte nodeId)
        {
            for (var i = 0; i < RemoteNodes.Count; i++)
            {
                if (RemoteNodes[i].ID == nodeId)
                    return i;
            }

            return -1;
        }

        public void RegisterNode(RemoteNodeComContext nodeCtx)
        {
            var nodeIndex = FindNodeByID(nodeCtx.ID);
            if (nodeIndex == -1)
            {
                if (UdpAgent.AllNodesMask != UdpAgent.NewNodeNotification(nodeCtx.ID))
                {
                    m_RemoteNodes.Add(nodeCtx);
                    return;
                }
            }

            // Since we are using UDP, it' possible that a node might attempt to register twice.
            // but it's also possible that a node crashed and is rebooting.
            // in both cases we just ignore it.
            ClusterDebug.LogWarning($"Node {nodeCtx.ID} is attempting to re-register. Request is ignored and role remains set to {RemoteNodes[nodeIndex].Role}.");
        }

        public void UnRegisterNode(byte NodeId)
        {
            for (var i = 0; i < m_RemoteNodes.Count; ++i)
            {
                var node = m_RemoteNodes[i];
                if (node.ID == NodeId)
                {
                    m_RemoteNodes.RemoveAt(i);
                    UdpAgent.AllNodesMask = UdpAgent.AllNodesMask.UnsetBit(NodeId);
                    TotalExpectedRemoteNodesCount--;
                    break; //No Duplicates
                }
            }
        }
    }
}
