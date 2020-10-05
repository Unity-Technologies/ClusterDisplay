using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    internal abstract class NodeState
    {
        protected Stopwatch m_Time;
        protected CancellationTokenSource m_Cancellation;
        protected Task m_Task;
        public NodeState PendingStateChange { get; set; }
        public static bool Debugging { get; set; }
        public TimeSpan MaxTimeOut = new TimeSpan(0,0,0,0,1000 * 60 * 5);

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
                    var shutdown = new Shutdown();
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
                    PendingStateChange = new Shutdown();
                    break;
                }

                case EMessageType.HelloMaster:
                    break;

                case EMessageType.WelcomeSlave:
                    break;

                default:
                {
                    throw new Exception("Unexpected network message received: ");
                }
            }
        }

        protected virtual void ExitState(NodeState newState)
        {
            Debug.Log("Exiting State:" + GetType().Name);

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

    // SlaveState state -------------------------------------------------------- 
    internal abstract class SlaveState : NodeState
    {
        protected SlavedNode LocalNode => (SlavedNode)ClusterSync.Instance.LocalNode;
    }

    // MasterState state -------------------------------------------------------- 
    internal abstract class MasterState : NodeState
    {
        protected MasterNode LocalNode => (MasterNode)ClusterSync.Instance.LocalNode;
    }

    // Shutdown state -------------------------------------------------------- 
    internal class Shutdown : NodeState
    {
    }

    // FatalError state -------------------------------------------------------- 
    internal class FatalError : NodeState
    {
        public FatalError(string msg)
        {
            Message = msg;
        }

        protected override void ExitState(NodeState newState)
        {
            base.ExitState(newState);
            Debug.LogError(Message);
        }

        public string Message { get;  }
    }

}