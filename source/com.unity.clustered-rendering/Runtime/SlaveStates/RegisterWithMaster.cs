using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterRendering.SlaveStateMachine
{
    internal class RegisterWithMaster : SlaveState
    {
        private bool m_MasterFound;
        public override bool ReadToProceedWithFrame => false;

        private Stopwatch m_Timer;
        private TimeSpan m_LastSend;

        public RegisterWithMaster(SlavedNode node) : base(node)
        {
            m_Timer = new Stopwatch();
            m_Timer.Start();
        }

        public override void InitState()
        {
            m_Cancellation = new CancellationTokenSource();
            m_Task = Task.Run(() => Execute(m_Cancellation.Token), m_Cancellation.Token);
        }

        protected override BaseState DoFrame( bool frameAdvance)
        {
            if (m_MasterFound)
            {
                var nextState = new SynchronizeFrame(m_Node);
                nextState.EnterState(this);
                return nextState;
            }

            return this;
        }

        private void Execute(CancellationToken ctk)
        {
            try
            {
                while (!ctk.IsCancellationRequested && !m_MasterFound)
                {
                    // Periodically broadcast presence
                    if (m_Timer.Elapsed - m_LastSend > TimeSpan.FromSeconds(1))
                    {
                        var header = new MessageHeader()
                        {
                            MessageType = EMessageType.HelloMaster,
                            DestinationIDs = UInt64.MaxValue, // Shout it out! make sure to also use DoesNotRequireAck
                            NodeRole = ENodeRole.Slave,
                            Flags = MessageHeader.EFlag.Broadcast | MessageHeader.EFlag.DoesNotRequireAck
                        };
                        m_Node.UdpAgent.PublishMessage(header);

                        m_LastSend = m_Timer.Elapsed;
                    }

                    // Wait for a response
                    if (m_Node.UdpAgent.RxWait.WaitOne(1000))
                    {
                        // Consume messages
                        while (m_Node.UdpAgent.NextAvailableRxMsg(out var header, out var payload))
                        {
                            if (header.MessageType == EMessageType.WelcomeSlave)
                            {
                                if ((header.DestinationIDs & m_Node.NodeIDMask) == m_Node.NodeIDMask)
                                {
                                    Debug.Log("Accepted by server");
                                    m_MasterFound = true;
                                    m_Node.MasterNodeId = header.OriginID;
                                    m_Node.UdpAgent.NewNodeNotification(m_Node.MasterNodeId);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception e)
            {
                var err = new FatalError() { Message = e.Message};
                m_AsyncError = err;
            }
        }

    }

}