using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterRendering.SlaveStateMachine
{
    internal class RegisterWithMaster : SlaveState
    {
        private bool m_MasterFound;
        private Stopwatch m_Timer;
        private TimeSpan m_LastSend;

        public RegisterWithMaster(SlavedNode node)
        {
            m_Timer = new Stopwatch();
            m_Timer.Start();
        }

        public override void InitState()
        {
            m_Cancellation = new CancellationTokenSource();
            m_Task = Task.Run(() => ProcessMessages(m_Cancellation.Token), m_Cancellation.Token);
        }

        protected override NodeState DoFrame( bool frameAdvance)
        {
            if (m_MasterFound)
            {
                var nextState = new SynchronizeFrame();
                nextState.EnterState(this);
                return nextState;
            }

            return this;
        }

        public override bool ReadyToProceed => false;

        private void ProcessMessages(CancellationToken ctk)
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
                            Flags = MessageHeader.EFlag.Broadcast | MessageHeader.EFlag.DoesNotRequireAck
                        };

                        var roleInfo = new RolePublication { NodeRole = ENodeRole.Slave };
                        var payload = NetworkingHelpers.AllocateMessageWithPayload<RolePublication>();
                        roleInfo.StoreInBuffer(payload, Marshal.SizeOf<MessageHeader>());

                        LocalNode.UdpAgent.PublishMessage(header, payload);

                        m_LastSend = m_Timer.Elapsed;
                    }

                    // Wait for a response
                    if (LocalNode.UdpAgent.RxWait.WaitOne(1000))
                    {
                        // Consume messages
                        while (LocalNode.UdpAgent.NextAvailableRxMsg(out var header, out var payload))
                        {
                            if (header.MessageType == EMessageType.WelcomeSlave)
                            {
                                if ((header.DestinationIDs & LocalNode.NodeIDMask) == LocalNode.NodeIDMask)
                                {
                                    Debug.Log("Accepted by master: " + header.OriginID);
                                    m_MasterFound = true;
                                    LocalNode.MasterNodeId = header.OriginID;
                                    LocalNode.UdpAgent.NewNodeNotification(LocalNode.MasterNodeId);
                                    return;
                                }
                            }
                            else
                                ProcessUnhandledMessage(header);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception e)
            {
                var err = new FatalError( $"Error occured while registering with master node: {e.Message}");
                PendingStateChange = err;
            }
        }

    }

}