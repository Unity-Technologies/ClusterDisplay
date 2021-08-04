using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.MasterStateMachine;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    internal class RemoteNodeComContext
    {
        public byte ID { get; set; }
        public ENodeRole Role { get; set; }
    }

    internal class MasterNode : ClusterNode
    {
        public List<RemoteNodeComContext> m_RemoteNodes = new List<RemoteNodeComContext>();
        public int TotalExpectedRemoteNodesCount { get; set; }

        public MasterNode(byte nodeId, int slaveCount, string ip, int rxport,int txport, int timeOut, int maxMTUSize, string adapterName) : base(nodeId, ip, rxport, txport, timeOut, maxMTUSize, adapterName)
        {
            TotalExpectedRemoteNodesCount = slaveCount;
        }

        public override bool Start()
        {
            if (!base.Start())
                return false;

            m_CurrentState = new WaitingForAllClients {
                MaxTimeOut = ClusterParams.RegisterTimeout };// 15 sec waiting for clients

            m_CurrentState.EnterState(null);

            return true;
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
            Debug.LogWarning($"Node {nodeCtx.ID} is attempting to re-register. Request is ignored and role remains set to {m_RemoteNodes[nodeIndex].Role}.");
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