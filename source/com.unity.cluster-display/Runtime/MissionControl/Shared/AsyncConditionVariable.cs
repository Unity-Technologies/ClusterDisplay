using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// ConditionVariable pattern for <see cref="Task"/>s.
    /// </summary>
    public class AsyncConditionVariable
    {
        /// <summary>
        /// A <see cref="Task"/> that is completed once the <see cref="AsyncConditionVariable"/> is signaled.
        /// </summary>
        public Task SignaledTask
        {
            get
            {
                lock (m_Lock)
                {
                    m_TaskCompletionSource ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                    return m_TaskCompletionSource.Task;
                }
            }
        }

        /// <summary>
        /// Signal the <see cref="AsyncConditionVariable"/> so that any code waiting on the task in
        /// <see cref="SignaledTask"/> is executed.
        /// </summary>
        public void Signal()
        {
            lock (m_Lock)
            {
                if (m_TaskCompletionSource != null && !m_TaskCompletionSource.Task.IsCanceled)
                {
#if UNITY_64
                    m_TaskCompletionSource.SetResult(true);
#else
                    m_TaskCompletionSource.SetResult();
#endif
                    m_TaskCompletionSource = null;
                }
            }
        }

        /// <summary>
        /// Set the state of the last <see cref="Task"/> returned by <see cref="SignaledTask"/> to canceled.
        /// </summary>
        public void Cancel()
        {
            lock (m_Lock)
            {
                m_TaskCompletionSource ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                m_TaskCompletionSource.TrySetCanceled();
            }
        }

        /// <summary>
        /// Lock access to m_TaskCompletionSource.
        /// </summary>
        object m_Lock = new();

        /// <summary>
        /// Used to wake up tasks waiting on the <see cref="AsyncConditionVariable"/>.
        /// </summary>
#if UNITY_64
        TaskCompletionSource<bool> m_TaskCompletionSource;
#else
        TaskCompletionSource? m_TaskCompletionSource;
#endif
    }
}
