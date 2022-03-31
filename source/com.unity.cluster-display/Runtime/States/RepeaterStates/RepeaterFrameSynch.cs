using System;
using System.Threading;
using Unity.ClusterDisplay.Utils;
using Unity.Profiling;
using UnityEngine;

namespace Unity.ClusterDisplay.RepeaterStateMachine
{
    internal class RepeaterSynchronization : RepeaterState, IRepeaterNodeSyncState
    {
        internal enum EStage
        {
            WaitingOnEmitterFrameData,
            ReadyToProceed,
            WaitForEmitterACK,
            EnteredNextFrame,
        }

        private EStage m_Stage;
        internal EStage Stage
        {
            get => m_Stage;
            set
            {
                ClusterDebug.Log($"(Frame: {CurrentFrameID}): Repeater entering stage: {value}");
                m_Stage = value;
            }
        }

        TimeSpan m_TsOfStage;

        public RepeaterStateReader m_RepeaterReceiver;
        public UDPAgent NetworkAgent => LocalNode.UdpAgent;

        ProfilerMarker m_MarkerDoFrame = new ProfilerMarker("SynchronizeFrame::DoFrame");
        ProfilerMarker m_MarkerWaitingOnGoFromEmitter = new ProfilerMarker("WaitingOnGoFromEmitter");
        ProfilerMarker m_MarkerReadyToProcessFrame = new ProfilerMarker("ReadyToProcessFrame");
        public override bool ReadyToProceed => Stage == EStage.ReadyToProceed;
        
        public BitVector EmitterNodeIdMask => LocalNode.EmitterNodeIdMask;
        
        public bool HasHardwareSync { get; set; }
        
        public RepeaterSynchronization(IClusterSyncState clusterSync) : base(clusterSync)
        {
        }

        public override string GetDebugString()
        {
            return $"{base.GetDebugString()} / {Stage}:\r\n\t\tLast Frame Reported Done: {m_RepeaterReceiver.LastReportedFrameDone}" +
                $"\r\n\t\tLast Frame Received: {m_RepeaterReceiver.LastRxFrameStart}" +
                $"\r\n\t\tReceive Count: {m_RepeaterReceiver.RxCount}" +
                $"\r\n\t\tSend Count: {m_RepeaterReceiver.TxCount}" +
                $"\r\n\t\tNetwork Overhead Average: {m_RepeaterReceiver.NetworkingOverheadAverage * 1000} ms";
        }
        //-------------------------------------------------

        public override void InitState()
        {
            Stage = EStage.WaitingOnEmitterFrameData;

            m_TsOfStage = m_Time.Elapsed;
            m_Cancellation = new CancellationTokenSource();

            m_RepeaterReceiver = new RepeaterStateReader(this);
        }

        protected override NodeState DoFrame(bool newFrame)
        {
            using (m_MarkerDoFrame.Auto())
            {
                // ClusterDebug.Log($"(Frame: {CurrentFrameID}): Current repeater stage: {Stage}");
                
                switch (Stage)
                {
                    case EStage.EnteredNextFrame:
                    {
                        OnEnteredNextFrame();
                    } break;
                    
                    case EStage.WaitForEmitterACK:
                    {
                        OnWaitForEmitterACK();
                    } break;
                        
                    case EStage.WaitingOnEmitterFrameData:
                    {
                        OnWaitingOnEmitterFrameData();
                    } break;

                    case EStage.ReadyToProceed:
                    {
                        ProceededToNextFrame(newFrame);
                    } break;
                }
                return this;
            }
        }

        private void OnEnteredNextFrame()
        {
            using (m_MarkerReadyToProcessFrame.Auto())
                m_RepeaterReceiver.SignalEnteringNextFrame(CurrentFrameID);

            Stage = EStage.WaitForEmitterACK;
            m_TsOfStage = m_Time.Elapsed;
        }
        
        private void OnWaitForEmitterACK ()
        {
            if (!HasHardwareSync && LocalNode.UdpAgent.HasPendingAcks)
            {
                // ClusterDebug.Log($"(Frame: {CurrentFrameID}): Waiting for ACK from emitter.");
                return;
            }
            
            Stage = EStage.WaitingOnEmitterFrameData;
            m_TsOfStage = m_Time.Elapsed;
        }

        private void OnWaitingOnEmitterFrameData()
        {
            using (m_MarkerWaitingOnGoFromEmitter.Auto())
            {
                m_RepeaterReceiver.PumpMsg(CurrentFrameID);
                
                // If we just processed the StartFrame message, then the Stage is now set to ReadyToProcessFrame.
                // This will un-block the player loop to process the frame and next time this method is called (DoFrame)
                // We will have actually processed a frame and be ready to inform emitter and wait for next frame start.
                if ((m_Time.Elapsed - m_TsOfStage) <= MaxTimeOut)
                    return;

                PendingStateChange =
                    new FatalError(clusterSync,
                        $"Have not received GO from Emitter after {MaxTimeOut.TotalMilliseconds}ms.");
            }
        }

        private void ProceededToNextFrame(bool newFrame)
        {
            Stage = EStage.EnteredNextFrame;
            m_TsOfStage = m_Time.Elapsed;
        }

        public override void OnEndFrame()
        {
            ClusterDebug.Log($"(Frame: {CurrentFrameID}): Repeater ended the frame.");
            base.OnEndFrame();
        }

        public void OnUnhandledNetworkMessage(MessageHeader msgHeader) => base.ProcessUnhandledMessage(msgHeader);

        public void OnNonMatchingFrame(byte originID, ulong frameNumber)
        {
            PendingStateChange =
                new FatalError(clusterSync,
                    $"Received a message from node {originID} about a starting frame {frameNumber}, when we are at {CurrentFrameID} (stage: {Stage})");
        }

        public void OnReceivedEmitterFrameData()
        {
            Stage = EStage.ReadyToProceed;
            m_TsOfStage = m_Time.Elapsed;
        }
    }
}