using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay.RepeaterStateMachine
{
    internal class RegisterWithEmitter : RepeaterState
    {
        private bool m_EmitterFound;
        private Stopwatch m_Timer;
        private TimeSpan m_LastSend;
        
        public RegisterWithEmitter(IClusterSyncState clusterSync, RepeaterNode node) : base(clusterSync)
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
            if (m_EmitterFound)
            {
                var nextState = new RepeaterSynchronization(clusterSync){MaxTimeOut = ClusterParams.CommunicationTimeout};
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
                while (!ctk.IsCancellationRequested && !m_EmitterFound)
                {
                    // Periodically broadcast presence
                    if (m_Timer.Elapsed - m_LastSend > TimeSpan.FromSeconds(1))
                    {
                        var header = new MessageHeader()
                        {
                            MessageType = EMessageType.HelloEmitter,
                            DestinationIDs = UInt64.MaxValue, // Shout it out! make sure to also use DoesNotRequireAck
                            Flags = MessageHeader.EFlag.Broadcast | MessageHeader.EFlag.DoesNotRequireAck
                        };

                        var roleInfo = new RolePublication { NodeRole = ENodeRole.Repeater };
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
                            if (header.MessageType == EMessageType.WelcomeRepeater)
                            {
                                if ((header.DestinationIDs & LocalNode.NodeIDMask) == LocalNode.NodeIDMask)
                                {
                                    ClusterDebug.Log("Accepted by emitter: " + header.OriginID);
                                    m_EmitterFound = true;
                                    LocalNode.EmitterNodeId = header.OriginID;
                                    LocalNode.UdpAgent.NewNodeNotification(LocalNode.EmitterNodeId);
                                    
                                    var config = IBlittable<ClusterRuntimeConfig>.FromByteArray(payload, header.OffsetToPayload);
                                    clusterSync.OnReceivedClusterRuntimeConfig(config);
                                    return;
                                }
                            }
                            
                            else
                                ProcessUnhandledMessage(header);
                        }
                    }

                    if (m_Timer.Elapsed > MaxTimeOut)
                    {
                        throw new Exception($"Emitter not found after {MaxTimeOut.TotalMilliseconds}ms.");
                    }
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