using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay.SlaveStateMachine
{
    internal class SynchronizeFrame : SlaveState, ISlaveNodeSyncState
    {
        enum EStage
        {
            WaitingOnGoFromMaster,
            ReadyToProcessFrame,
        }

        private EStage m_Stage;

        TimeSpan m_TsOfStage;

        public SlaveReciever m_SlaveReceiver;
        public UDPAgent NetworkAgent => LocalNode.UdpAgent;

        ProfilerMarker m_MarkerDoFrame = new ProfilerMarker("SynchronizeFrame::DoFrame");
        ProfilerMarker m_MarkerWaitingOnGoFromMaster = new ProfilerMarker("WaitingOnGoFromMaster");
        ProfilerMarker m_MarkerReadyToProcessFrame = new ProfilerMarker("ReadyToProcessFrame");
        public override bool ReadyToProceed => m_Stage == EStage.ReadyToProcessFrame;
        public ulong MasterNodeIdMask => LocalNode.MasterNodeIdMask;

        public override string GetDebugString()
        {
            return $"{base.GetDebugString()} / {m_Stage} : {CurrentFrameID}, {m_SlaveReceiver.LastReportedFrameDone}, {m_SlaveReceiver.LastRxFrameStart}, {m_SlaveReceiver.RxCount}, {m_SlaveReceiver.TxCount}" +
            $"\r\nNetwork: {m_SlaveReceiver.NetworkingOverheadAverage * 1000:000.0}";
        }
        //-------------------------------------------------

        public override void InitState()
        {
            if (!ClusterSync.TryGetInstance(out var clusterSync))
                return;

            m_Stage = EStage.WaitingOnGoFromMaster;
            m_TsOfStage = m_Time.Elapsed;
            m_Cancellation = new CancellationTokenSource();

            m_SlaveReceiver = new SlaveReciever(this, clusterSync.maxFrameNetworkByteBufferSize, clusterSync.maxRpcByteBufferSize);
        }

        protected override NodeState DoFrame(bool newFrame)
        {
            using (m_MarkerDoFrame.Auto())
            {
                // Debug.Log($"Stage: {m_Stage}, Frame: {LocalNode.CurrentFrameID}");
                switch (m_Stage)
                {
                    case EStage.WaitingOnGoFromMaster:
                    {
                        using (m_MarkerWaitingOnGoFromMaster.Auto())
                        {
                            m_SlaveReceiver.PumpMsg(CurrentFrameID);
                            
                            if ((m_Time.Elapsed - m_TsOfStage) > MaxTimeOut)
                            {
                                PendingStateChange =
                                    new FatalError(
                                        $"Have not received GO from Master after {MaxTimeOut.TotalMilliseconds}ms.");

                            }

                            // If we just processed the StartFrame message, then the Stage is now set to ReadyToProcessFrame.
                            // This will un-block the player loop to process the frame and next time this method is called (DoFrame)
                            // We will have actually processed a frame and be ready to inform master and wait for next frame start.
                            break;
                        }
                    }

                    case EStage.ReadyToProcessFrame:
                    {
                        if (newFrame)
                            using (m_MarkerReadyToProcessFrame.Auto())
                                m_SlaveReceiver.SignalFrameDone(CurrentFrameID);
                        break;
                    }
                }
                return this;
            }
        }

        public void OnUnhandledNetworkMessage(MessageHeader msgHeader) => base.ProcessUnhandledMessage(msgHeader);

        public void OnNonMatchingFrame(byte originID, ulong frameNumber)
        {
            PendingStateChange =
                new FatalError(
                    $"Received a message from node {originID} about a starting frame {frameNumber}, when we are at {CurrentFrameID} (stage: {m_Stage})");
        }

        public void OnPumpedMsg()
        {
            m_Stage = EStage.ReadyToProcessFrame;
            m_TsOfStage = m_Time.Elapsed;
        }

        public void OnPublishingMsg()
        {
            m_Stage = EStage.WaitingOnGoFromMaster;
            m_TsOfStage = m_Time.Elapsed;
        }

    }
}