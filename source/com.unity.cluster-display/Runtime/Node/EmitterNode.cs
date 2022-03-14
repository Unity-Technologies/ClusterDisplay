using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.EmitterStateMachine;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    internal class RemoteNodeComContext
    {
        public byte ID { get; set; }
        public ENodeRole Role { get; set; }
    }

    internal class EmitterNode : ClusterNode
    {
        public List<RemoteNodeComContext> m_RemoteNodes = new List<RemoteNodeComContext>();
        public int TotalExpectedRemoteNodesCount { get; set; }
        public bool RepeatersDelayed { get; set; }

        public struct Config
        {
            public bool headlessEmitter;
            public bool repeatersDelayed;
            public int repeaterCount;
            public UDPAgent.Config udpAgentConfig;
        }

        public EmitterNode(
            IClusterSyncState clusterSync, 
            Config config)
            : base(clusterSync, config.udpAgentConfig)
        {
            m_CurrentState = new WaitingForAllClients(clusterSync, config.headlessEmitter) {
                MaxTimeOut = ClusterParams.RegisterTimeout };// 15 sec waiting for clients
            RepeatersDelayed = config.repeatersDelayed;
            TotalExpectedRemoteNodesCount = config.repeaterCount;
            m_CurrentState.EnterState(null);
        }

        public int FindNodeByID(byte nodeId)
        {
            for (var i = 0; i < m_RemoteNodes.Count; i++)
            {
                if (m_RemoteNodes[i].ID == nodeId)
                    return i;
            }

            return -1;
        }

        public void RegisterNode(RemoteNodeComContext nodeCtx )
        {
            var nodeIndex = FindNodeByID(nodeCtx.ID);
            if (nodeIndex == -1)
            {
                if (m_UDPAgent.AllNodesMask != m_UDPAgent.NewNodeNotification(nodeCtx.ID))
                {
                    m_RemoteNodes.Add(nodeCtx);
                    return;
                }
            }

            // Since we are using UDP, it' possible that a node might attempt to register twice.
            // but it's also possible that a node crashed and is rebooting.
            // in both cases we just ignore it.
            ClusterDebug.LogWarning($"Node {nodeCtx.ID} is attempting to re-register. Request is ignored and role remains set to {m_RemoteNodes[nodeIndex].Role}.");
        }

        public void UnRegisterNode(byte NodeId)
        {
            for (var i = 0;i<m_RemoteNodes.Count;++i)
            {
                var node = m_RemoteNodes[i];
                if (node.ID == NodeId)
                {
                    m_RemoteNodes.RemoveAt(i);
                    UdpAgent.AllNodesMask = UdpAgent.AllNodesMask & ~(UInt64) (1<<NodeId);
                    TotalExpectedRemoteNodesCount--;
                    break; //No Duplicates
                }
            }
        }
    }
}