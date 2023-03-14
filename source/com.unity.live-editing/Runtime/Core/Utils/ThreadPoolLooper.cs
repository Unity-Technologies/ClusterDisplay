using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.LiveEditing.LowLevel
{
    /// <summary>
    /// Looper that raises Update events from the thread pool.
    /// </summary>
    class ThreadPoolLooper : ILooper, IDisposable
    {
        public Action Update { get; set; }
        readonly Timer m_Timer;

        public ThreadPoolLooper(TimeSpan updateInterval)
        {
            m_Timer = new Timer(TimerTick, null, TimeSpan.Zero, updateInterval);
        }

        void TimerTick(object obj)
        {
            Update?.Invoke();
        }

        public void Dispose()
        {
            m_Timer.Dispose();
        }
    }
}
