using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.ClusterRendering.SlaveStateMachine
{
    internal class SynchronizeFrame : SlaveState
    {

        enum EStage
        {
            WaitingOnGoFromMaster,
            ReadyToProcessFrame,
            FrameDone,
        }

        private UInt64 m_CurrentFrameID;
        private EStage m_Stage;

        public SynchronizeFrame(SlavedNode node) : base(node)
        {
        }

        public override void InitState()
        {
            m_CurrentFrameID = 0;
            m_Stage = EStage.WaitingOnGoFromMaster;

            m_Cancellation = new CancellationTokenSource();
            m_Task = Task.Run(() => Execute(m_Cancellation.Token), m_Cancellation.Token);
        }

        protected override BaseState DoFrame(bool frameAdvance)
        {
            switch (m_Stage)
            {
                case EStage.WaitingOnGoFromMaster:
                {
                    // As a slave node, we will wait for ever
                    return this;
                }
                case EStage.ReadyToProcessFrame:
                {
                    if (frameAdvance)
                    {
                        Debug.Log("Slave processed frame " + m_CurrentFrameID);

                        Debug.Log("Done processing frame " + m_CurrentFrameID);
                        var msgHdr = new MessageHeader()
                        {
                            MessageType = EMessageType.FrameDone,
                            DestinationIDs = m_Node.MasterNodeIdMask,
                        };

                        var outBuffer = new byte[Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<FrameDone>()];
                        var msg = new FrameDone()
                        {
                            FrameNumber = m_CurrentFrameID
                        };
                        msg.StoreInBuffer(outBuffer, Marshal.SizeOf<MessageHeader>());

                        m_Node.UdpAgent.PublishMessage(msgHdr, outBuffer);

                        m_CurrentFrameID++;

                        m_Stage = EStage.WaitingOnGoFromMaster;
                    }
                    return this;
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
                        if (msgHdr.MessageType == EMessageType.StartFrame)
                        {
                            Debug.Assert( m_Stage == EStage.WaitingOnGoFromMaster );
                            var respMsg = AdvanceFrame.FromByteArray(outBuffer, msgHdr.OffsetToPayload);
                            if (respMsg.FrameNumber == m_CurrentFrameID)
                            {
                                Debug.Assert(m_Stage == EStage.WaitingOnGoFromMaster);
                                m_Stage = EStage.ReadyToProcessFrame;
                            }
                            else
                                Debug.Log($"Received a message from node {msgHdr.OriginID} about a starting frame {respMsg.FrameNumber}, when we are at {m_CurrentFrameID}");
                        }
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

        public override bool ReadyToProceed => m_Stage == EStage.ReadyToProcessFrame;
    }
}