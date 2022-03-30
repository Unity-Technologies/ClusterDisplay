using System;
using System.Threading;
using Unity.ClusterDisplay.Utils;
using Unity.Profiling;
using UnityEngine;

namespace Unity.ClusterDisplay.EmitterStateMachine
{
    internal class EmitterSynchronization : EmitterState, IEmitterNodeSyncState
    {
        internal enum EStage
        {
            WaitOneFrame,
            WaitOnRepeatersNextFrame,
            EmitLastFrameData,
            WaitForRepeatersToACK,
            ReadyToProceed,
        }

        // Bit mask of node id's that we are waiting on to say they are ready for work.
        BitVector m_WaitingOnNodes;

        private EStage m_Stage;
        internal EStage Stage
        {
            get => m_Stage;
            set
            {
                ClusterDebug.Log($"(Frame: {CurrentFrameID}): Emitter entering stage: {value}");
                m_Stage = value;
            }
        }
        
        private TimeSpan m_TsOfStage;
        private TimeSpan m_FrameDoneTimeout = new TimeSpan(0, 0, 0, 10, 0);

        private EmitterStateWriter m_Emitter;

        public override bool ReadyToProceed => Stage == EStage.ReadyToProceed;

        public UDPAgent NetworkAgent => LocalNode.UdpAgent;

        ProfilerMarker m_MarkerDoFrame = new ProfilerMarker("SynchronizeFrame::DoFrame");
        ProfilerMarker m_MarkerReadyToSignalStartNewFrame = new ProfilerMarker("ReadyToSignalStartNewFrame");
        ProfilerMarker m_MarkerWaitingOnFramesDoneMsgs = new ProfilerMarker("WaitingOnFramesDoneMsgs");
        ProfilerMarker m_MarkerProcessFrame = new ProfilerMarker("ProcessFrame");
        ProfilerMarker m_MarkerPublishState = new ProfilerMarker("PublishState");
        
        public EmitterSynchronization(IClusterSyncState clusterSync) : base(clusterSync) {}

        protected override void ExitState(NodeState newState)
        {
            base.ExitState(newState);
            m_Emitter?.Dispose();   
        }
        public override string GetDebugString() =>
            $"{base.GetDebugString()} / {(EStage) Stage}:\r\n\t\tWaiting on Nodes: {m_WaitingOnNodes}";

        public override void InitState()
        {
            base.InitState();

            m_Emitter = new EmitterStateWriter(this, LocalNode.RepeatersDelayed);
            

            // If the repeaters are delayed, then we can have the emitter step one frame by setting our initial
            // stage to "ReadyToProceed". Otherwise, we need to wait for the repeaters to enter their first frame.
            Stage = LocalNode.RepeatersDelayed ? 
                EStage.ReadyToProceed : 
                EStage.WaitOnRepeatersNextFrame;

            // If the repeaters were not delayed, we need to fill the frame data buffer for the repeater when
            // enter this state for the first time.
            if (Stage == EStage.WaitOnRepeatersNextFrame)
                m_Emitter.GatherFrameState(CurrentFrameID);
            else m_Emitter.GatherPreFrameState();

            m_TsOfStage = m_Time.Elapsed;
            m_WaitingOnNodes = new BitVector();
        }

        public override void OnEndFrame()
        {
            ClusterDebug.Log($"(Frame: {CurrentFrameID}): Emitter ended frame.");
            base.OnEndFrame();
        }

        protected override NodeState DoFrame(bool newFrame)
        {
            if (LocalNode.TotalExpectedRemoteNodesCount == 0)
            {
                PendingStateChange = new FatalError(clusterSync, "No Clients found. Exiting Cluster.");
                return this;
            }
            
            // ClusterDebug.Log($"(Frame: {CurrentFrameID}): Current emitter stage: {Stage}");

            using (m_MarkerDoFrame.Auto())
            {
                switch ((EStage)Stage)
                {
                    case EStage.WaitOnRepeatersNextFrame:
                    {
                        OnWaitOnRepeatersNextFrame();
                    } break;

                    case EStage.EmitLastFrameData:
                    {
                        OnEmitFrameData();
                    } break;
                    
                    case EStage.WaitForRepeatersToACK:
                    {
                        OnWaitForRepeatersToACK(EStage.ReadyToProceed);
                    } break;

                    case EStage.ReadyToProceed:
                    {
                        ProceededToNextFrame(newFrame);
                    } break;
                }

                return this;
            }
        }

        private void OnWaitOnRepeatersNextFrame()
        {
            using (m_MarkerWaitingOnFramesDoneMsgs.Auto())
            {
                PumpMessages();

                if ((m_Time.Elapsed - m_TsOfStage) > MaxTimeOut)
                    ClusterDebug.Assert(m_WaitingOnNodes.Any(), $"(Frame: {CurrentFrameID}): Have been waiting on repeaters nodes for: {nameof(ClusterDisplay.RepeaterEnteredNextFrame)} for more than {MaxTimeOut.Seconds} seconds!");

                if ((m_Time.Elapsed - m_TsOfStage) >= m_FrameDoneTimeout)
                {
                    ClusterDebug.LogError($"(Frame: {CurrentFrameID}): The following repeaters are late reporting back: {m_WaitingOnNodes}");
                    KickLateClients();
                }
            }
        }

        private void OnWaitForRepeatersToACK (EStage nextStage)
        {
            if (LocalNode.UdpAgent.AcksPending)
            {
                // ClusterDebug.Log($"(Frame: {CurrentFrameID}): Waiting for all repeaters to ACK.");
                return;
            }
            
            Stage = nextStage;
            m_TsOfStage = m_Time.Elapsed;
        }

        private void OnEmitFrameData()
        {
            m_Emitter.GatherFrameState(CurrentFrameID);

            ClusterDebug.Assert(m_Emitter.ValidRawStateData, $"(Frame: {CurrentFrameID}): State buffer is empty!");
            
            using (m_MarkerPublishState.Auto())
                m_Emitter.PublishCurrentState(PreviousFrameID);
            
            Stage = EStage.WaitForRepeatersToACK;
            m_TsOfStage = m_Time.Elapsed;
        }

        private void ProceededToNextFrame(bool newFrame)
        {
            using (m_MarkerProcessFrame.Auto())
            {
                Stage = EStage.WaitOnRepeatersNextFrame;
                m_TsOfStage = m_Time.Elapsed;
                m_WaitingOnNodes = LocalNode.UdpAgent.AllNodesMask.UnsetBit(LocalNode.NodeID);
            }
        }

        private void PumpMessages()
        {
            while (LocalNode.UdpAgent.NextAvailableRxMsg(out var msgHdr, out var outBuffer))
            {
                if (msgHdr.MessageType == EMessageType.EnterNextFrame)
                {
                    RepeaterEnteredNextFrame(msgHdr, outBuffer);
                    continue;
                }
                
                ProcessUnhandledMessage(msgHdr);
            }
            
            if (m_WaitingOnNodes.Any())
                return;
            
            ClusterDebug.Log($"(Frame: {CurrentFrameID}): All nodes entered new frame.");
            Stage = EStage.EmitLastFrameData;
            m_TsOfStage = m_Time.Elapsed;
        }

        private void RepeaterEnteredNextFrame(MessageHeader msgHdr, byte[] outBuffer)
        {
            ClusterDebug.Assert(Stage != EStage.EmitLastFrameData, $"(Frame: {CurrentFrameID}): Emitter received {nameof(ClusterDisplay.RepeaterEnteredNextFrame)} msg while not in: {EStage.EmitLastFrameData} stage!");

            var respMsg = outBuffer.LoadStruct<RepeaterEnteredNextFrame>(msgHdr.OffsetToPayload);
            ClusterDebug.Log($"(Sequence ID: {msgHdr.SequenceID}, Frame: {respMsg.FrameNumber}): Received: {nameof(ClusterDisplay.RepeaterEnteredNextFrame)} message.");

            // Repeater nodes will send FrameDone messages one frame behind if -delayRepeaters is defined as a command argument, since the emitter
            // will always be rendering 1 frame ahead. Therefore we just need to subtract the emitter's current frame by one to verify that we are
            // still in sync with the repeaters.
            ulong currentFrame = 
                LocalNode.RepeatersDelayed ? 
                    CurrentFrameID - 1 : // The emitter is one frame ahead.
                    CurrentFrameID;

            if (respMsg.FrameNumber != currentFrame) // Validate that were frame matching as expected.
            {
                ClusterDebug.LogWarning( $"Message of type: {msgHdr.MessageType} with sequence ID: {msgHdr.SequenceID} is for frame: {respMsg.FrameNumber} when we are expecting {nameof(RepeaterEnteredNextFrame)} events from the previous frame: {currentFrame}. Any of the following could have occurred:\n\t1. We already interpreted the message, but an ACK was never sent to the repeater.\n\t2. We already interpreted the message, but our ACK never reached the repeater.\n\t3. We some how never received this message. Yet we proceeded to the next frame anyways.");
                return;
            }
            
            m_WaitingOnNodes = m_WaitingOnNodes.UnsetBit(msgHdr.OriginID);
        }

        private void KickLateClients()
        {
            ClusterDebug.LogError($"(Frame: {CurrentFrameID}): The following repeaters are late reporting back: {m_WaitingOnNodes} after {MaxTimeOut.TotalMilliseconds}ms. Continuing without them.");
            for (byte id = 0; id < BitVector.Length; ++id)
            {
                if (m_WaitingOnNodes[id])
                {
                    ClusterDebug.LogError($"(Frame: {CurrentFrameID}): Unregistering node {id}");
                    LocalNode.UnRegisterNode(id);
                }
            }
        }
    }
}
