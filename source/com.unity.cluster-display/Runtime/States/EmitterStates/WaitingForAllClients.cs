﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.ClusterDisplay.EmitterStateMachine
{
    internal class WaitingForAllClients : EmitterState
    {
        private readonly bool k_HeadlessEmitter;
        public override bool ReadyToProceed => false;
        public AccumulateFrameDataDelegate m_AccumulateFrameDataDelegate;

        public WaitingForAllClients(IClusterSyncState clusterSync, bool headlessEmitter) : base(clusterSync) =>
            k_HeadlessEmitter = headlessEmitter;

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
                    ClusterDebug.LogError($"WaitingForAllClients timed out after {MaxTimeOut.TotalMilliseconds}ms: Expected {LocalNode.TotalExpectedRemoteNodesCount}, continuing with { LocalNode.m_RemoteNodes.Count} nodes ");
                    LocalNode.TotalExpectedRemoteNodesCount = LocalNode.m_RemoteNodes.Count;
                }

                var newState = new EmitterSynchronization(clusterSync) {
                    MaxTimeOut = ClusterParams.CommunicationTimeout,
                };

                return newState.EnterState(this);
            }
            
            return this;
        }

        private void EmitRuntimeConfig ()
        {
            var header = new MessageHeader()
            {
                MessageType = EMessageType.HelloEmitter,
                DestinationIDs = UInt64.MaxValue, // Shout it out! make sure to also use DoesNotRequireAck
                Flags = MessageHeader.EFlag.Broadcast | MessageHeader.EFlag.DoesNotRequireAck
            };

            var roleInfo = new ClusterRuntimeConfig { headlessEmitter = true };
            var payload = NetworkingHelpers.AllocateMessageWithPayload<RolePublication>();
            roleInfo.StoreInBuffer(payload, Marshal.SizeOf<MessageHeader>());
            LocalNode.UdpAgent.PublishMessage(header, payload);
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
                            if (header.MessageType == EMessageType.HelloEmitter)
                            {
                                var roleInfo = IBlittable<RolePublication>.FromByteArray(payload, header.OffsetToPayload);

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
                ClusterDebug.LogException(e);
                var err = new FatalError( clusterSync, $"Error occured while processing nodes discovery: {e.Message}");
                PendingStateChange = err;
            }
        }

        private void RegisterRemoteNode(MessageHeader header, RolePublication roleInfo )
        {
            ClusterDebug.Log("Discovered remote node: " + header.OriginID );

            var commCtx = new RemoteNodeComContext
            {
                ID = header.OriginID,
                Role = roleInfo.NodeRole,
            };
            LocalNode.RegisterNode(commCtx);
        }

        private void SendResponse(MessageHeader rxHeader)
        {
            var header = new MessageHeader()
            {
                MessageType = EMessageType.WelcomeRepeater,
                DestinationIDs = (UInt64)1 << rxHeader.OriginID,
            };

            var roleInfo = new ClusterRuntimeConfig { headlessEmitter = k_HeadlessEmitter };
            var payload = NetworkingHelpers.AllocateMessageWithPayload<ClusterRuntimeConfig>();
            roleInfo.StoreInBuffer(payload, Marshal.SizeOf<MessageHeader>());
            
            LocalNode.UdpAgent.PublishMessage(header, payload);
        }

        public override string GetDebugString()
        {
            return $"{base.GetDebugString()} : {LocalNode.m_RemoteNodes.Count}";
        }
    }

}