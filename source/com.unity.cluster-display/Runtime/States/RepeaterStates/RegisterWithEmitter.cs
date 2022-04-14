using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.ClusterDisplay.Utils;

namespace Unity.ClusterDisplay.RepeaterStateMachine
{
    internal class RegisterWithEmitter : RepeaterState
    {
        private bool m_EmitterFound;
        private Stopwatch m_Timer;
        private TimeSpan m_LastSend;

        public RegisterWithEmitter(IClusterSyncState clusterSync) : base(clusterSync)
        {
            m_Timer = new Stopwatch();
            m_Timer.Start();
        }

        public override void InitState()
        {
            m_Cancellation = new CancellationTokenSource();
        }

        protected override NodeState DoFrame (bool frameAdvance)
        {
            if (m_EmitterFound)
            {
                var nextState = new RepeaterSynchronization(clusterSync) { MaxTimeOut = this.MaxTimeOut };
                nextState.EnterState(this);
                return nextState;
            }

            ProcessMessages(m_Cancellation.Token);
            return this;
        }
        
        public override bool ReadyToProceed => false;

        private void ProcessMessages(CancellationToken ctk)
        {
            try
            {
                if (ctk.IsCancellationRequested)
                    return;

                // Periodically broadcast presence
                if (m_Timer.Elapsed - m_LastSend > TimeSpan.FromSeconds(1))
                {
                    var header = new MessageHeader()
                    {
                        MessageType = EMessageType.HelloEmitter,
                        DestinationIDs = BitVector.Ones, // Shout it out! make sure to also use DoesNotRequireAck
                        Flags = MessageHeader.EFlag.Broadcast | MessageHeader.EFlag.DoesNotRequireAck
                    };

                    var roleInfo = new RolePublication { NodeRole = ENodeRole.Repeater };
                    var payload = NetworkingHelpers.AllocateMessageWithPayload<RolePublication>();
                    roleInfo.StoreInBuffer(payload, Marshal.SizeOf<MessageHeader>());

                    LocalNode.UdpAgent.PublishMessage(header, payload);
                    m_LastSend = m_Timer.Elapsed;
                }

                // Wait for a response
                // Consume messages
                while (LocalNode.UdpAgent.NextAvailableRxMsg(out var header, out _))
                {
                    if (header.MessageType == EMessageType.WelcomeRepeater)
                    {
                        if (header.DestinationIDs[LocalNode.NodeID])
                        {
                            ClusterDebug.Log("Accepted by emitter: " + header.OriginID);
                            m_EmitterFound = true;
                            LocalNode.EmitterNodeId = header.OriginID;
                            LocalNode.UdpAgent.NewNodeNotification(LocalNode.EmitterNodeId);

                            return;
                        }
                    }

                    else
                        ProcessUnhandledMessage(header);
                }

                if (m_Timer.Elapsed > MaxTimeOut)
                {
                    throw new Exception($"Emitter not found after {MaxTimeOut.TotalMilliseconds}ms.");
                }
            }

            catch (OperationCanceledException)
            {

            }

            catch (Exception e)
            {
                var err = new FatalError( clusterSync, $"Error occured while registering with emitter node: {e.Message}");
                PendingStateChange = err;
            }
        }

    }

}
