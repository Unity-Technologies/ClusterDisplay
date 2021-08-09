using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay.RepeaterStateMachine
{
    internal class SynchronizeFrame : RepeaterState, IRepeaterNodeSyncState
    {
        enum EStage
        {
            WaitingOnGoFromEmitter,
            ReadyToProcessFrame,
        }

        private EStage m_Stage;

        TimeSpan m_TsOfStage;

        public RepeaterParser m_RepeaterReceiver;
        public UDPAgent NetworkAgent => LocalNode.UdpAgent;

        ProfilerMarker m_MarkerDoFrame = new ProfilerMarker("SynchronizeFrame::DoFrame");
        ProfilerMarker m_MarkerWaitingOnGoFromEmitter = new ProfilerMarker("WaitingOnGoFromEmitter");
        ProfilerMarker m_MarkerReadyToProcessFrame = new ProfilerMarker("ReadyToProcessFrame");
        public override bool ReadyToProceed => m_Stage == EStage.ReadyToProcessFrame;
        public ulong EmitterNodeIdMask => LocalNode.EmitterNodeIdMask;

        public override string GetDebugString()
        {
            return $"{base.GetDebugString()} / {m_Stage} : {CurrentFrameID}, {m_RepeaterReceiver.LastReportedFrameDone}, {m_RepeaterReceiver.LastRxFrameStart}, {m_RepeaterReceiver.RxCount}, {m_RepeaterReceiver.TxCount}" +
            $"\r\nNetwork: {m_RepeaterReceiver.NetworkingOverheadAverage * 1000:000.0}";
        }
        //-------------------------------------------------

        public override void InitState()
        {
            if (!ClusterSync.TryGetInstance(out var clusterSync))
                return;

            m_Stage = EStage.WaitingOnGoFromEmitter;
            m_TsOfStage = m_Time.Elapsed;
            m_Cancellation = new CancellationTokenSource();

            m_RepeaterReceiver = new RepeaterParser(this, clusterSync.Resources.NetworkPayloadLimits);
        }

        protected override NodeState DoFrame(bool newFrame)
        {
            using (m_MarkerDoFrame.Auto())
            {
                // Debug.Log($"Stage: {m_Stage}, Frame: {LocalNode.CurrentFrameID}");
                switch (m_Stage)
                {
                    case EStage.WaitingOnGoFromEmitter:
                    {
                        using (m_MarkerWaitingOnGoFromEmitter.Auto())
                        {
                            m_RepeaterReceiver.PumpMsg(CurrentFrameID);
                            
                            if ((m_Time.Elapsed - m_TsOfStage) > MaxTimeOut)
                            {
                                PendingStateChange =
                                    new FatalError(
                                        $"Have not received GO from Emitter after {MaxTimeOut.TotalMilliseconds}ms.");

                            }

                            // If we just processed the StartFrame message, then the Stage is now set to ReadyToProcessFrame.
                            // This will un-block the player loop to process the frame and next time this method is called (DoFrame)
                            // We will have actually processed a frame and be ready to inform emitter and wait for next frame start.
                            break;
                        }
                    }

                    case EStage.ReadyToProcessFrame:
                    {
                        if (newFrame)
                            using (m_MarkerReadyToProcessFrame.Auto())
                                m_RepeaterReceiver.SignalFrameDone(CurrentFrameID);
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
            m_Stage = EStage.WaitingOnGoFromEmitter;
            m_TsOfStage = m_Time.Elapsed;
        }

    }
}