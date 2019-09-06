using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterRendering
{
    internal abstract class BaseState
    {
        protected Stopwatch m_Time;
        protected CancellationTokenSource m_Cancellation;
        protected Task m_Task;
        protected BaseState m_AsyncStateChange;
        public static bool Debugging { get; set; }
        public static int MaxTimeOut = 1000 * 60 * 1;

        public virtual bool ReadyToProceed => true;

        protected BaseState()
        {
        }

        public BaseState EnterState(BaseState oldState)
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
            
        }

        protected virtual BaseState DoFrame( bool frameAdvance )
        {
            return this;
        }

        public BaseState ProcessFrame( bool frameAdvance )
        {
            var res = DoFrame(frameAdvance);
            if (res != this)
                return res;

            if (Debugging)
            {
                if( frameAdvance )
                    m_Time.Restart();

                if (m_Time.ElapsedMilliseconds > MaxTimeOut)
                {
                    var shutdown = new Shutdown();
                    return shutdown.EnterState(this);
                }
            }
            
            if (m_AsyncStateChange != null)
                return m_AsyncStateChange.EnterState(this);

            return this;
        }

        protected void ProcessUnhandledMessage( MessageHeader msgHeader )
        {
            switch (msgHeader.MessageType)
            {
                case EMessageType.GlobalShutdownRequest:
                {
                    m_AsyncStateChange = new Shutdown();
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

        protected virtual void ExitState(BaseState newState)
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
    }

    // Shutdown state -------------------------------------------------------- 
    internal class Shutdown : BaseState
    {
    }

    // FatalError state -------------------------------------------------------- 
    internal class FatalError : BaseState
    {
        public string Message { get; set; }
    }

}