using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.ClusterDisplay.MasterStateMachine
{
    internal class WaitingForAllClients : MasterState
    {
        public override bool ReadyToProceed => false;
        public AccumulateFrameDataDelegate m_AccumulateFrameDataDelegate;

        public override void InitState()
        {
            m_Cancellation = new CancellationTokenSource();
            m_Task = Task.Run(() => ProcessMessages(m_Cancellation.Token), m_Cancellation.Token);
        }

        protected override NodeState DoFrame(bool frameAdvance)
        {
            bool timeOut = m_Time.Elapsed > MaxTimeOut;
            if (LocalNode.m_RemoteNodes.Count == LocalNode.TotalExpectedRemoteNodesCount || timeOut )
            {
                // OnTimeout continue with the current set of nodes
                if (timeOut)
                {
                    Debug.LogError($"WaitingForAllClients timed out after {MaxTimeOut.TotalMilliseconds}ms: Expected {LocalNode.TotalExpectedRemoteNodesCount}, continuing with { LocalNode.m_RemoteNodes.Count} nodes ");
                    LocalNode.TotalExpectedRemoteNodesCount = LocalNode.m_RemoteNodes.Count;
                }

                var newState = new SynchronizeFrame {
                    MaxTimeOut = ClusterParams.CommunicationTimeout,
                };

                return newState.EnterState(this);
            }
            
            return this;
        }

        private void ProcessMessages(CancellationToken ctk)
        {
            Debug.Log("PLZ FOR ALL CLIENTS.");
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
                                var roleInfo = RolePublication.FromByteArray(payload, header.OffsetToPayload);

                                RegisterRemoteNode(header, roleInfo);
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
                var err = new FatalError( $"Error occured while processing nodes discovery: {e.Message}");
                PendingStateChange = err;
            }
        }

        private void RegisterRemoteNode(MessageHeader header, RolePublication roleInfo )
        {
            Debug.Log("Discovered remote node: " + header.OriginID );

            var commCtx = new RemoteNodeComContext
            {
                ID = header.OriginID,
                Role = roleInfo.NodeRole,
            };
            LocalNode.RegisterNode(commCtx);
        }

        private void SendResponse(MessageHeader rxHeader)
        {
            var response = new MessageHeader()
            {
                MessageType = EMessageType.WelcomeSlave,
                DestinationIDs = (UInt64)1 << rxHeader.OriginID,
            };

            LocalNode.UdpAgent.PublishMessage(response);
        }

        public override string GetDebugString()
        {
            return $"{base.GetDebugString()} : {LocalNode.m_RemoteNodes.Count}";
        }

    }

}