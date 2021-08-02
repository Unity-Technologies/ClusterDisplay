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

        private MasterNode testClusterNode = null;

        public override void InitState()
        {
            m_Cancellation = new CancellationTokenSource();
            testClusterNode = LocalNode;
            m_Task = Task.Run(() => ProcessMessages(m_Cancellation.Token), m_Cancellation.Token);
        }

        protected override NodeState DoFrame(bool frameAdvance)
        {
            bool timeOut = m_Time.Elapsed > MaxTimeOut;
            if (testClusterNode.m_RemoteNodes.Count == testClusterNode.TotalExpectedRemoteNodesCount || timeOut )
            {
                // OnTimeout continue with the current set of nodes
                if (timeOut)
                {
                    Debug.LogError($"WaitingForAllClients timed out after {MaxTimeOut.TotalMilliseconds}ms: Expected {testClusterNode.TotalExpectedRemoteNodesCount}, continuing with { testClusterNode.m_RemoteNodes.Count} nodes ");
                    testClusterNode.TotalExpectedRemoteNodesCount = testClusterNode.m_RemoteNodes.Count;
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
            try
            {
                do
                {
                    // Wait for a client to announce itself
                    if (testClusterNode.UdpAgent.RxWait.WaitOne(1000))
                    {
                        // Consume messages
                        while (testClusterNode.UdpAgent.NextAvailableRxMsg(out var header, out var payload))
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

                } while ( (testClusterNode.m_RemoteNodes.Count < testClusterNode.TotalExpectedRemoteNodesCount) && !ctk.IsCancellationRequested);
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
            testClusterNode.RegisterNode(commCtx);
        }

        private void SendResponse(MessageHeader rxHeader)
        {
            var response = new MessageHeader()
            {
                MessageType = EMessageType.WelcomeSlave,
                DestinationIDs = (UInt64)1 << rxHeader.OriginID,
            };

            testClusterNode.UdpAgent.PublishMessage(response);
        }

        public override string GetDebugString()
        {
            return $"{base.GetDebugString()} : {testClusterNode.m_RemoteNodes.Count}";
        }

    }

}