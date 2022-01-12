using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    public delegate bool AccumulateFrameDataDelegate(NativeArray<byte> buffer, ref int endPos);

    internal abstract class NodeState
    {
        protected Stopwatch m_Time;
        protected CancellationTokenSource m_Cancellation;
        protected Task m_Task;
        public NodeState PendingStateChange { get; set; }
        public static bool Debugging { get; set; }
        public TimeSpan MaxTimeOut = new TimeSpan(0,0,0,30,0);

        protected internal IClusterSyncState clusterSync;
        
        public NodeState(IClusterSyncState clusterSync) =>
            this.clusterSync = clusterSync;

        public virtual bool ReadyToProceed => true;

        public NodeState EnterState(NodeState oldState)
        {
            if (oldState != this)
            {
                oldState?.ExitState(this);
                m_Time = new Stopwatch();
                m_Time.Start();

                InitState();
            }
            return this;
        }

        public virtual void InitState()
        {
            m_Cancellation = new CancellationTokenSource();
        }

        protected virtual NodeState DoFrame( bool newFrame)
        {
            return this;
        }

        public virtual void OnEndFrame() {}

        public NodeState ProcessFrame( bool newFrame)
        {
            var res = DoFrame(newFrame);
            if (res != this)
                return res;

            // RemoveMe
            if (Debugging)
            {
                if(newFrame)
                    m_Time.Restart();

                if (m_Time.ElapsedMilliseconds > MaxTimeOut.Milliseconds)
                {
                    var shutdown = new Shutdown(clusterSync);
                    return shutdown.EnterState(this);
                }
            }
            
            if (PendingStateChange != null)
                return PendingStateChange.EnterState(this);

            return this;
        }

        protected void ProcessUnhandledMessage( MessageHeader msgHeader )
        {
            switch (msgHeader.MessageType)
            {
                case EMessageType.GlobalShutdownRequest:
                {
                    PendingStateChange = new Shutdown(clusterSync);
                    break;
                }

                case EMessageType.HelloEmitter:
                    break;

                case EMessageType.WelcomeRepeater:
                    break;

                default:
                {
                    throw new Exception("Unexpected network message received: ");
                }
            }
        }

        protected virtual void ExitState(NodeState newState)
        {
            ClusterDebug.Log("Exiting State:" + GetType().Name);

            if (m_Task != null)
            {
                try
                {
                    m_Cancellation.Cancel(); // it's expected/assumed/required that a task is accompanied by a cancellation source
                    m_Task.Wait(m_Cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"\n{nameof(OperationCanceledException)} thrown\n");
                }
                finally
                {
                    m_Cancellation.Dispose();
                }
            }
        }

        public virtual string GetDebugString()
        {
            return GetType().Name;
        }
    }

    // RepeaterState state -------------------------------------------------------- 
    internal abstract class RepeaterState : NodeState
    {
        public RepeaterState(IClusterSyncState clusterSync) : base(clusterSync) {}

        protected ulong CurrentFrameID => clusterSync.CurrentFrameID;

        protected RepeaterNode LocalNode => (RepeaterNode)clusterSync.LocalNode;
    }

    // EmitterState state -------------------------------------------------------- 
    internal abstract class EmitterState : NodeState
    {
        public EmitterState(IClusterSyncState clusterSync) : base(clusterSync) {}
        protected ulong PreviousFrameID => CurrentFrameID - 1;
        protected ulong CurrentFrameID => clusterSync.CurrentFrameID;
        protected EmitterNode LocalNode => (EmitterNode)clusterSync.LocalNode;
    }

    // Shutdown state -------------------------------------------------------- 
    internal class Shutdown : NodeState
    {
        public Shutdown(IClusterSyncState clusterSync) : base(clusterSync) {}
    }

    // FatalError state -------------------------------------------------------- 
    internal class FatalError : NodeState
    {
        public FatalError(IClusterSyncState clusterSync, string msg) : base(clusterSync)
        {
            Message = msg;
        }

        protected override void ExitState(NodeState newState)
        {
            base.ExitState(newState);
            ClusterDebug.LogError(Message);
        }

        public string Message { get;  }
    }

}