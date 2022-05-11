using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay.EmitterStateMachine
{
    class WaitingForAllClients : NodeState<EmitterNode>
    {
        // We should never proceed out of the while loop since we are immediately going to
        // enter the EmitterSynchronization state before exiting the loop.
        public override bool ReadyToProceed => false;
        public override bool ReadyForNextFrame => false;

        public WaitingForAllClients(EmitterNode node)
            : base(node)
        {
            if (node.HandshakeTimeout != default)
                MaxTimeOut = node.HandshakeTimeout;
        }

        protected override void InitState()
        {
        }

        protected override NodeState DoFrame (bool frameAdvance)
        {
            bool timeOut = m_Time.Elapsed > MaxTimeOut;
            if (LocalNode.RemoteNodes.Count == LocalNode.TotalExpectedRemoteNodesCount || timeOut)
            {
                // OnTimeout continue with the current set of nodes
                if (timeOut)
                {
                    ClusterDebug.LogError($"WaitingForAllClients timed out after {MaxTimeOut.TotalMilliseconds}ms: Expected {LocalNode.TotalExpectedRemoteNodesCount}, continuing with {LocalNode.RemoteNodes.Count} nodes ");
                    LocalNode.TotalExpectedRemoteNodesCount = LocalNode.RemoteNodes.Count;
                }

                return new EmitterSynchronization(LocalNode);
            }

            ProcessMessages();
            return this;
        }

        private void ProcessMessages()
        {
            try
            {
                // Wait for a client to announce itself
                // Consume messages
                while (LocalNode.UdpAgent.NextAvailableRxMsg(out var header, out var payload, 1000))
                {
                    if (header.MessageType == EMessageType.HelloEmitter)
                    {
                        var roleInfo = payload.LoadStruct<RolePublication>(header.OffsetToPayload);

                        RegisterRemoteNode(header, roleInfo);
                        SendResponse(header);
                    }
                    else
                    {
                        ProcessUnhandledMessage(header);
                    }
                }
            }

            catch (Exception e)
            {
                ClusterDebug.LogException(e);
                var err = new FatalError($"Error occured while processing nodes discovery: {e.Message}");
                PendingStateChange = err;
            }
        }

        private void RegisterRemoteNode(MessageHeader header, RolePublication roleInfo)
        {
            ClusterDebug.Log("Discovered remote node: " + header.OriginID);

            var commCtx = new RemoteNodeComContext {ID = header.OriginID, Role = roleInfo.NodeRole,};
            LocalNode.RegisterNode(commCtx);
        }

        private void SendResponse(MessageHeader rxHeader)
        {
            var header = new MessageHeader() {MessageType = EMessageType.WelcomeRepeater, DestinationIDs = BitVector.FromIndex(rxHeader.OriginID),};

            LocalNode.UdpAgent.PublishMessage(header);
        }

        public override string GetDebugString()
        {
            return $"{base.GetDebugString()}:\r\n\t\tExpected Node Count: {LocalNode.RemoteNodes.Count}";
        }
    }
}
