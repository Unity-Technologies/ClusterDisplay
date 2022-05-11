using System;
using System.Threading;
using Unity.ClusterDisplay.Utils;
using Unity.Profiling;
using UnityEngine;

namespace Unity.ClusterDisplay.EmitterStateMachine
{
    class EmitterSynchronization : NodeState<EmitterNode>
    {
        public enum EStage
        {
            WaitOnRepeatersNextFrame,
            EmitLastFrameData,
            WaitForRepeatersToACK,
            ReadyToProceed,
        }

        /// <summary>
        /// Bit mask of node id's that we are waiting on to say they are ready for work.
        /// </summary>
        /// <remarks>
        /// Used only when there is a software sync at the beginning of each frame (no hardware sync).
        /// </remarks>
        BitVector m_WaitingOnNodes;

        private EStage m_Stage;

        public EStage Stage
        {
            get => m_Stage;
            set
            {
                ClusterDebug.Log($"(Frame: {LocalNode.CurrentFrameID}): Emitter entering stage: {value}");
                m_Stage = value;
            }
        }

        private TimeSpan m_TsOfStage;

        private EmitterStateWriter m_Emitter;

        public bool HasHardwareSync { get; set; }

        public override bool ReadyToProceed => Stage is EStage.WaitForRepeatersToACK or EStage.ReadyToProceed;
        public override bool ReadyForNextFrame => Stage == EStage.ReadyToProceed;

        public UDPAgent NetworkAgent => LocalNode.UdpAgent;

        ProfilerMarker m_MarkerDoFrame = new ProfilerMarker("SynchronizeFrame::DoFrame");
        ProfilerMarker m_MarkerReadyToSignalStartNewFrame = new ProfilerMarker("ReadyToSignalStartNewFrame");
        ProfilerMarker m_MarkerWaitingOnFramesDoneMsgs = new ProfilerMarker("WaitingOnFramesDoneMsgs");
        ProfilerMarker m_MarkerProcessFrame = new ProfilerMarker("ProcessFrame");
        ProfilerMarker m_MarkerPublishState = new ProfilerMarker("PublishState");

        public EmitterSynchronization(EmitterNode node)
            : base(node)
        {
            if (node.CommunicationTimeout != default)
                MaxTimeOut = node.CommunicationTimeout;
        }

        protected override void ExitState()
        {
            base.ExitState();
            m_Emitter?.Dispose();
        }

        public override string GetDebugString() =>
            $"{base.GetDebugString()} / {(EStage) Stage}:\r\n\t\tWaiting on Nodes: {m_WaitingOnNodes}";

        protected override void InitState()
        {
            base.InitState();

            m_Emitter = new EmitterStateWriter(LocalNode.RepeatersDelayed);

            // If the repeaters are delayed, then we can have the emitter step one frame by setting our initial
            // stage to "ReadyToProceed". Otherwise, we need to wait for the repeaters to enter their first frame.
            Stage = LocalNode.RepeatersDelayed ? EStage.ReadyToProceed : EStage.WaitOnRepeatersNextFrame;

            // If the repeaters were not delayed, we need to fill the frame data buffer for the repeater when
            // enter this state for the first time.
            if (Stage == EStage.WaitOnRepeatersNextFrame)
                m_Emitter.GatherFrameState();
            else m_Emitter.GatherPreFrameState();

            m_TsOfStage = m_Time.Elapsed;
            m_WaitingOnNodes = new BitVector();
        }

        public override void OnEndFrame()
        {
            ClusterDebug.Log($"(Frame: {LocalNode.CurrentFrameID}): Emitter ended frame.");
            base.OnEndFrame();
        }

        protected override NodeState DoFrame(bool newFrame)
        {
            if (LocalNode.TotalExpectedRemoteNodesCount == 0)
            {
                PendingStateChange = new FatalError("No Clients found. Exiting Cluster.");
                return this;
            }

            // ClusterDebug.Log($"(Frame: {LocalNode.CurrentFrameID}): Current emitter stage: {Stage}");

            using (m_MarkerDoFrame.Auto())
            {
                switch (Stage)
                {
                    case EStage.WaitOnRepeatersNextFrame:
                        OnWaitOnRepeatersNextFrame();
                        break;
                    case EStage.EmitLastFrameData:
                        OnEmitFrameData();
                        break;
                    case EStage.ReadyToProceed:
                        ProceededToNextFrame(newFrame);
                        break;
                    // It is possible that we will land on here if there are multiple ClusterSyncs registered with ClusterSyncLooper.
                    case EStage.WaitForRepeatersToACK:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return this;
            }
        }

        protected override void DoLateFrame()
        {
            base.DoLateFrame();
            OnWaitForRepeatersToACK();
        }

        private void OnWaitOnRepeatersNextFrame()
        {
            if (HasHardwareSync)
            {
                // When we have hardware sync, we can assume that by this point all nodes have finished presenting
                // the previous frame and is ready to start a new frame.
                Stage = EStage.EmitLastFrameData;
                m_TsOfStage = m_Time.Elapsed;
                return;
            }

            using (m_MarkerWaitingOnFramesDoneMsgs.Auto())
            {
                // Otherwise, wait for nodes to report in.
                PumpMessages();

                if (m_Time.Elapsed - m_TsOfStage >= MaxTimeOut)
                {
                    KickLateStarts();
                }
            }
        }

        void OnWaitForRepeatersToACK()
        {
            var ready = true;
            if (NetworkAgent.HasPendingAcks)
            {
                ready = false;
                if (m_Time.Elapsed - m_TsOfStage >= MaxTimeOut)
                {
                    KickUnresponsiveNodes();
                    ready = true;
                }
            }

            if (ready)
            {
                Stage = EStage.ReadyToProceed;
                m_TsOfStage = m_Time.Elapsed;
            }
        }

        private void OnEmitFrameData()
        {
            m_Emitter.GatherFrameState();

            // If the repeaters were delayed by one frame, then we need to send emitter's data from the
            // last frame. That last frame data is sent with this previous frame number for validation
            // by the repeater.
            var previousFrameID = LocalNode.RepeatersDelayed ? LocalNode.CurrentFrameID - 1 : LocalNode.CurrentFrameID;

            using (m_MarkerPublishState.Auto())
                m_Emitter.PublishCurrentState(LocalNode.UdpAgent, previousFrameID);

            Stage = EStage.WaitForRepeatersToACK;
            m_TsOfStage = m_Time.Elapsed;

            // These flags track the EnteredNextFrame messages for the following frame,
            // not the LastFrameData acks. This logic could be made clearer.
            m_WaitingOnNodes = NetworkAgent.AllNodesMask.UnsetBit(LocalNode.NodeID);
        }

        private void ProceededToNextFrame(bool newFrame)
        {
            using (m_MarkerProcessFrame.Auto())
            {
                Stage = EStage.WaitOnRepeatersNextFrame;
                m_TsOfStage = m_Time.Elapsed;
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

            ClusterDebug.Log($"(Frame: {LocalNode.CurrentFrameID}): All nodes entered new frame.");
            Stage = EStage.EmitLastFrameData;
            m_TsOfStage = m_Time.Elapsed;
        }

        private void RepeaterEnteredNextFrame(MessageHeader msgHdr, byte[] outBuffer)
        {
            ClusterDebug.Assert(Stage != EStage.EmitLastFrameData, $"(Frame: {LocalNode.CurrentFrameID}): Emitter received {nameof(ClusterDisplay.RepeaterEnteredNextFrame)} msg while not in: {EStage.EmitLastFrameData} stage!");

            var respMsg = outBuffer.LoadStruct<RepeaterEnteredNextFrame>(msgHdr.OffsetToPayload);
            ClusterDebug.Log($"(Sequence ID: {msgHdr.SequenceID}, Frame: {respMsg.FrameNumber}): Received: {nameof(ClusterDisplay.RepeaterEnteredNextFrame)} message.");

            // Repeater nodes will send FrameDone messages one frame behind if -delayRepeaters is defined as a command argument, since the emitter
            // will always be rendering 1 frame ahead. Therefore we just need to subtract the emitter's current frame by one to verify that we are
            // still in sync with the repeaters.
            ulong currentFrame =
                LocalNode.RepeatersDelayed
                    ? LocalNode.CurrentFrameID - 1
                    : LocalNode.CurrentFrameID;

            if (respMsg.FrameNumber != currentFrame) // Validate that were frame matching as expected.
            {
                ClusterDebug.LogWarning($"Message of type: {msgHdr.MessageType} with sequence ID: {msgHdr.SequenceID} is for frame: {respMsg.FrameNumber} when we are expecting {nameof(RepeaterEnteredNextFrame)} events from the previous frame: {currentFrame}. Any of the following could have occurred:\n\t1. We already interpreted the message, but an ACK was never sent to the repeater.\n\t2. We already interpreted the message, but our ACK never reached the repeater.\n\t3. We some how never received this message. Yet we proceeded to the next frame anyways.");
                return;
            }

            m_WaitingOnNodes = m_WaitingOnNodes.UnsetBit(msgHdr.OriginID);
        }

        /// <summary>
        /// Kick nodes that are late to start their frames when we're not using hardware sync.
        /// </summary>
        void KickLateStarts()
        {
            ClusterDebug.LogError($"(Frame: {LocalNode.CurrentFrameID}): The following repeaters are late reporting back: {m_WaitingOnNodes} after {MaxTimeOut.TotalMilliseconds}ms. Continuing without them.");
            for (byte id = 0; id < BitVector.Length; ++id)
            {
                if (m_WaitingOnNodes[id])
                {
                    ClusterDebug.LogError($"(Frame: {LocalNode.CurrentFrameID}): Unregistering node {id}");
                    LocalNode.UnRegisterNode(id);
                }
            }
        }

        /// <summary>
        /// Kick nodes that are not acknowledging receipt of the frame data message.
        /// </summary>
        void KickUnresponsiveNodes()
        {
            foreach (var pendingAck in NetworkAgent.PendingAcks)
            {
                ClusterDebug.LogError($"(Frame: {LocalNode.CurrentFrameID}): node {pendingAck.nodeId} has not reported back after {MaxTimeOut.TotalMilliseconds}ms. Continuing without it.");
                LocalNode.UnRegisterNode(pendingAck.nodeId);
            }
        }
    }
}
