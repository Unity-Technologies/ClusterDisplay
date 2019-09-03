using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterRendering
{
    internal abstract class BaseState
    {
        protected Stopwatch m_Time;
        protected CancellationTokenSource m_Cancellation;
        protected Task m_Task;
        protected FatalError m_AsyncError;
        public static bool Debugging { get; set; }
        public static int MaxTimeOut = 1000 * 60 * 1;

        protected BaseState()
        {
        }

        public BaseState EnterState(BaseState oldState)
        {
            if (oldState != this)
            {
                Debug.Log( "Entering State:"  + GetType().Name );

                oldState?.ExitState(this);

                InitState();

                if (Debugging)
                {
                    m_Time = new Stopwatch();
                    m_Time.Start();
                }


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

            if (m_AsyncError != null)
                return m_AsyncError.EnterState(this);

            return this;
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

        public virtual bool ReadyToProceed
        {
            get { return true; }
        }
    }

    // Shutdown state -------------------------------------------------------- 
    internal class Shutdown : BaseState
    {
        public Shutdown()
        {
            Debug.Log("Shut down requested");
        }
      
    }

    // FatalError state -------------------------------------------------------- 
    internal class FatalError : BaseState
    {
        public string Message { get; set; }
    }
}