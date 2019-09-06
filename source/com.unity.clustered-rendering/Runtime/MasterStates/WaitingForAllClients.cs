using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.ClusterRendering.MasterStateMachine
{

    internal class WaitingForAllClients : MasterState
    {
        public override bool ReadyToProceed => false;

        public WaitingForAllClients(MasterNode node) : base(node)
        {
        }

        public override void InitState()
        {
            m_Cancellation = new CancellationTokenSource();
            m_Task = Task.Run(() => ProcessMessages(m_Cancellation.Token), m_Cancellation.Token);
        }

        protected override BaseState DoFrame(bool frameAdvance)
        {
            if (m_Node.m_RemoteNodes.Count == m_Node.TotalExpectedRemoteNodesCount)
            {
                var newState = new SynchronizeFrame(m_Node);
                return newState.EnterState(this);
            }

            return this;
        }

        private void ProcessMessages(CancellationToken ctk)
        {
            try
            {
                do
                {
                    // Wait for a client to announce itself
                    if (m_Node.UdpAgent.RxWait.WaitOne(1000))
                    {
                        // Consume messages
                        while (m_Node.UdpAgent.NextAvailableRxMsg(out var header, out var payload))
                        {
                            if (header.MessageType == EMessageType.HelloMaster)
                            {
                                RegisterNewSlaveNode(header);
                                SendResponse(header);
                            }
                            else
                            {
                                ProcessUnhandledMessage(header);
                            }
                        }
                    }

                } while ( (m_Node.m_RemoteNodes.Count < m_Node.TotalExpectedRemoteNodesCount) && !ctk.IsCancellationRequested);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var err = new FatalError() {Message = e.Message};
                m_AsyncStateChange = err;
            }
        }

        private void RegisterNewSlaveNode(MessageHeader header)
        {
            Debug.Log("Slave node saying hello: " + header.OriginID);

            var commCtx = new RemoteNodeComContext
            {
                ID = header.OriginID,
                Role = header.NodeRole,
            };
            m_Node.RegisterNode(commCtx);
        }

        private void SendResponse(MessageHeader rxHeader)
        {
            var response = new MessageHeader()
            {
                MessageType = EMessageType.WelcomeSlave,
                DestinationIDs = (UInt64)1 << rxHeader.OriginID,
                NodeRole = ENodeRole.Master,
            };

            m_Node.UdpAgent.PublishMessage(response);
        }
    }

}