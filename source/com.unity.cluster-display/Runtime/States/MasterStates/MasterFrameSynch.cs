using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;

namespace Unity.ClusterDisplay.MasterStateMachine
{
    internal class SynchronizeFrame : MasterState, IMasterNodeSyncState
    {
        enum EStage
        {
            StepOneFrameOnInitialization,
            ReadyToSignalStartNewFrame,
            WaitingOnFramesDoneMsgs,
            ProcessFrame,
        }

        private Int64 m_WaitingOnNodes; // bit mask of node id's that we are waiting on to say they are ready for work.
        private EStage m_Stage;
        private TimeSpan m_TsOfStage;

        private MasterEmitter m_MasterEmitter;

        public override bool ReadyToProceed => m_Stage == EStage.StepOneFrameOnInitialization || m_Stage == EStage.ProcessFrame;

        public UDPAgent NetworkAgent => LocalNode.UdpAgent;

        ProfilerMarker m_MarkerDoFrame = new ProfilerMarker("SynchronizeFrame::DoFrame");
        ProfilerMarker m_MarkerReadyToSignalStartNewFrame = new ProfilerMarker("ReadyToSignalStartNewFrame");
        ProfilerMarker m_MarkerWaitingOnFramesDoneMsgs = new ProfilerMarker("WaitingOnFramesDoneMsgs");
        ProfilerMarker m_MarkerProcessFrame = new ProfilerMarker("ProcessFrame");
        ProfilerMarker m_MarkerPublishState = new ProfilerMarker("PublishState");

        public override string GetDebugString()
        {
            return $"{base.GetDebugString()} / {(EStage)m_Stage} : {m_WaitingOnNodes}";
        }

        public override void InitState()
        {
            if (!ClusterSync.TryGetInstance(out var clusterSync))
                return;

            base.InitState();

            m_MasterEmitter = new MasterEmitter(this, clusterSync.maxFrameNetworkByteBufferSize, clusterSync.maxRpcByteBufferSize);
            m_Stage = EStage.StepOneFrameOnInitialization;

            m_TsOfStage = m_Time.Elapsed;
            m_WaitingOnNodes = 0;

            RPCEmitter.AllowWrites = true;
        }

        public override void OnEndFrame()
        {
            // Debug.Log($"Ended Frame: {LocalNode.CurrentFrameID}");
            if (m_Stage == EStage.StepOneFrameOnInitialization)
            {
                m_Stage = EStage.ReadyToSignalStartNewFrame;
                return;
            }

            base.OnEndFrame();
        }

        protected override NodeState DoFrame(bool newFrame)
        {
            if (LocalNode.TotalExpectedRemoteNodesCount == 0)
            {
                PendingStateChange = new FatalError("No Clients found. Exiting Cluster.");
                return this;
            }

            using (m_MarkerDoFrame.Auto())
            {
                if (newFrame)
                    m_MasterEmitter.GatherFrameState(CurrentFrameID);

                // Debug.Log($"Stage: {m_Stage}, Frame: {LocalNode.CurrentFrameID}");
                switch ((EStage) m_Stage)
                {
                    case EStage.ReadyToSignalStartNewFrame:
                    {
                        using (m_MarkerReadyToSignalStartNewFrame.Auto())
                        {
                            if (!m_MasterEmitter.ValidRawStateData) // 1st frame only
                                m_MasterEmitter.GatherFrameState(CurrentFrameID);

                            if (m_MasterEmitter.ValidRawStateData)
                            {
                                using (m_MarkerPublishState.Auto())
                                    m_MasterEmitter.PublishCurrentState(PreviousFrameID);

                                m_WaitingOnNodes = (Int64) (LocalNode.UdpAgent.AllNodesMask & ~LocalNode.NodeIDMask);
                                m_Stage = EStage.ProcessFrame;
                            }
                            else
                                Debug.LogError("State buffer is empty!");

                            break;
                        }
                    }

                    case EStage.ProcessFrame:
                    {
                        using (m_MarkerProcessFrame.Auto())
                        {
                            Debug.Assert(newFrame, "this should always be on a new frame.");
                            m_Stage = EStage.WaitingOnFramesDoneMsgs;
                            m_TsOfStage = m_Time.Elapsed;
                            break;
                        }
                    }

                    case EStage.WaitingOnFramesDoneMsgs:
                    {
                        using (m_MarkerWaitingOnFramesDoneMsgs.Auto())
                        {
                            PumpMessages();

                            if ((m_Time.Elapsed - m_TsOfStage) > MaxTimeOut)
                            {
                                KickLateClients();
                                BecomeReadyToSignalStartNewFrame();
                            }

                            if ((m_Time.Elapsed - m_TsOfStage).TotalSeconds > 5)
                            {
                                Debug.Assert(m_WaitingOnNodes != 0,
                                    "Have been waiting on slave nodes 'frame done' for more than 5 seconds!");
                                // One or more clients failed to respond in time!
                                Debug.LogError("The following slaves are late reporting back: " + m_WaitingOnNodes);
                            }

                            break;
                        }
                    }
                }

                return this;
            }
        }

        private void PumpMessages()
        {
            while (LocalNode.UdpAgent.NextAvailableRxMsg(out var msgHdr, out var outBuffer))
            {
                switch (msgHdr.MessageType)
                {
                    case EMessageType.FrameDone:
                    {
                        Debug.Assert(m_Stage != EStage.ReadyToSignalStartNewFrame,
                            "Master: received FrameDone msg while not in 'ReadyToSignalStartNewFrame' stage!");

                        var respMsg = FrameDone.FromByteArray(outBuffer, msgHdr.OffsetToPayload);

                        if (respMsg.FrameNumber == CurrentFrameID - 1)
                        {
                            var maskOut = ~((Int64) 1 << msgHdr.OriginID);
                            do
                            {
                                var orgMask = m_WaitingOnNodes;
                                Interlocked.CompareExchange(ref m_WaitingOnNodes, orgMask & maskOut, orgMask);
                            } while ((m_WaitingOnNodes & ((Int64) 1 << msgHdr.OriginID)) != 0);
                        }
                        else
                            PendingStateChange = new FatalError( $"Received a message from node {msgHdr.OriginID} about a completed Past frame {respMsg.FrameNumber}, when we are at {CurrentFrameID}" );

                        break;
                    }

                    default:
                    {
                        ProcessUnhandledMessage(msgHdr);
                        break;
                    }
                }
            }
            
            BecomeReadyToSignalStartNewFrame();
        }

        private void BecomeReadyToSignalStartNewFrame()
        {
            if (m_WaitingOnNodes == 0)
            {
                m_Stage = EStage.ReadyToSignalStartNewFrame;
                m_TsOfStage = m_Time.Elapsed;
            }
        }

        private void KickLateClients()
        {
            Debug.LogError(
                $"The following slaves are late reporting back:{m_WaitingOnNodes} after {MaxTimeOut.TotalMilliseconds}ms. Continuing without them.");
            for (byte id = 0; id < sizeof(UInt64); ++id)
            {
                if ((1 << id & m_WaitingOnNodes) != 0)
                {
                    Debug.LogError($"Unregistering node {id}");
                    LocalNode.UnRegisterNode(id);
                }
            }

            m_WaitingOnNodes = 0;
        }
    }
}
