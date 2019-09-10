using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.ClusterRendering.MasterStateMachine
{

    internal class WaitingForAllClients : MasterState
    {
        public override bool ReadyToProceed => false;

        public override void InitState()
        {
            m_Cancellation = new CancellationTokenSource();
            m_Task = Task.Run(() => ProcessMessages(m_Cancellation.Token), m_Cancellation.Token);
        }

        protected override NodeState DoFrame(bool frameAdvance)
        {
            if (LocalNode.m_RemoteNodes.Count == LocalNode.TotalExpectedRemoteNodesCount)
            {
                var newState = new SynchronizeFrame();
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
                    if (LocalNode.UdpAgent.RxWait.WaitOne(1000))
                    {
                        // Consume messages
                        while (LocalNode.UdpAgent.NextAvailableRxMsg(out var header, out var payload))
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

                } while ( (LocalNode.m_RemoteNodes.Count < LocalNode.TotalExpectedRemoteNodesCount) && !ctk.IsCancellationRequested);
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
            LocalNode.RegisterNode(commCtx);
        }

        private void SendResponse(MessageHeader rxHeader)
        {
            var response = new MessageHeader()
            {
                MessageType = EMessageType.WelcomeSlave,
                DestinationIDs = (UInt64)1 << rxHeader.OriginID,
                NodeRole = ENodeRole.Master,
            };

            LocalNode.UdpAgent.PublishMessage(response);
        }

        public override string GetDebugString()
        {
            return $"{base.GetDebugString()} : {LocalNode.m_RemoteNodes.Count}";
        }

    }

}