using System;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Class representing a locked <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>The <see cref="Value"/> property will contain the <typeparamref name="T"/> for as long as this object
    /// hasn't been disposed of and other callers without the lock will not be able to access it either.  So this object
    /// should be disposed of as soon as possible.</remarks>
    /// <typeparam name="T"><see cref="ObservableObject"/> we are making accessible.</typeparam>
    public class AsyncLockedObject<T> : IDisposable where T : class
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="observableObject"><see cref="ObservableObject"/> to make accessible.</param>
        /// <param name="lockHandle">Object that keeps <paramref name="observableObject"/> locked and that we should
        /// dispose of when we are disposed.</param>
        public AsyncLockedObject(T observableObject, IDisposable lockHandle)
        {
            m_ObservableObject = observableObject;
            m_LockHandle = lockHandle;
        }

        /// <summary>
        /// Returns the <typeparamref name="T"/>.
        /// </summary>
        public T Value
        {
            get
            {
                if (m_ObservableObject == null)
                {
                    throw new ObjectDisposedException(nameof(AsyncLockedObject<T>),
                        $"Trying to access Value of a disposed {nameof(AsyncLockedObject<T>)}");
                }
                return m_ObservableObject;
            }
        }

        public void Dispose()
        {
            IDisposable? toUnlock = Interlocked.Exchange(ref m_LockHandle, null);
            if (toUnlock != null)
            {
                m_ObservableObject = null;
                toUnlock.Dispose();
            }
        }

        T? m_ObservableObject;
        IDisposable? m_LockHandle;
    }
}
