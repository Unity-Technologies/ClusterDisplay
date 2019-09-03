using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.ClusterRendering.MasterStateMachine
{
    internal class SynchronizeFrame : MasterState
    {
        enum EStage
        {
            ReadyToStartNewFrame,
            WaitingOnFramesDoneMsgs,
            ProcessFrame,
        }

        private UInt64 m_WaitingOnNodes; // bit mask of node id's that we are waiting on to say they are ready for work.
        private UInt64 m_CurrentFrameID;
        private EStage m_Stage;
        private TimeSpan m_TsOfStage;
        public SynchronizeFrame(MasterNode node) : base(node)
        {
        }

        public override void InitState()
        {
            m_Stage = EStage.ReadyToStartNewFrame;
            m_TsOfStage = m_Time.Elapsed;
            m_WaitingOnNodes = 0;
            m_CurrentFrameID = UInt64.MaxValue;
            for (int i = 0; i < m_Node.m_RemoteNodes.Count; i++)
            {
                m_Node.m_RemoteNodes[i].readyToProcessFrameID = 0;
            }

            m_Cancellation = new CancellationTokenSource();
            m_Task = Task.Run(() => Execute(m_Cancellation.Token), m_Cancellation.Token);
        }
        
        protected override BaseState DoFrame(bool frameAdvance)
        {
            switch (m_Stage)
            {
                case EStage.ReadyToStartNewFrame:
                {
                    // Time to start the current frame
                    var msgHdr = new MessageHeader()
                    {
                        MessageType = EMessageType.StartFrame,
                        Flags = MessageHeader.EFlag.Broadcast
                    };

                    var outBuffer = new byte[Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<AdvanceFrame>()];
                    var msg = new AdvanceFrame()
                    {
                        FrameNumber = ++m_CurrentFrameID
                    };
                    msg.StoreInBuffer(outBuffer, Marshal.SizeOf<MessageHeader>());

                    m_WaitingOnNodes = m_Node.UdpAgent.AllNodesMask & ~m_Node.NodeIDMask;
                    m_Stage = EStage.ProcessFrame;

                    m_Node.UdpAgent.PublishMessage(msgHdr, outBuffer);
                    break;
                }

                case EStage.ProcessFrame:
                {
                    if (frameAdvance)
                    {
                        m_Stage = EStage.WaitingOnFramesDoneMsgs;
                        m_TsOfStage = m_Time.Elapsed;
                    }

                        break;
                }

                case EStage.WaitingOnFramesDoneMsgs:
                {
                    if ((m_Time.Elapsed - m_TsOfStage).TotalSeconds > 5)
                    {
                        Debug.Assert(m_WaitingOnNodes != 0);
                        // One or more clients failed to respond in time!
                        Debug.LogError("The following slaves are late reporting back: " + m_WaitingOnNodes);
                    }
                    break;
                }
            }

            return this;
        }

        private void Execute(CancellationToken ctk)
        {
            try
            {
                while (!ctk.IsCancellationRequested)
                {
                    while (m_Node.UdpAgent.NextAvailableRxMsg(out var msgHdr, out var outBuffer))
                    {
                        if (msgHdr.MessageType == EMessageType.FrameDone)
                        {
                            Debug.Assert(m_Stage == EStage.WaitingOnFramesDoneMsgs);

                            var respMsg = FrameDone.FromByteArray(outBuffer, msgHdr.OffsetToPayload);
                            if (respMsg.FrameNumber == m_CurrentFrameID)
                            {
                                m_WaitingOnNodes &= ~((UInt64) 1 << msgHdr.OriginID);
                                Debug.Log("Slave Done processing frame " + respMsg.FrameNumber);
                            }
                            else
                                Debug.Log($"Received a message from node {msgHdr.OriginID} about a completed Past frame { respMsg.FrameNumber }, when we are at {m_CurrentFrameID}");
                        }
                    }

                    if (m_WaitingOnNodes == 0 && m_Stage == EStage.WaitingOnFramesDoneMsgs)
                    {
                        m_Stage = EStage.ReadyToStartNewFrame;
                        m_TsOfStage = m_Time.Elapsed;
                    }

                    m_Node.UdpAgent.RxWait.WaitOne(500);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public override bool ReadyToProceed => m_Stage == EStage.ProcessFrame;

    }
}
