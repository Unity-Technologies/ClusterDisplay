using System;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Class representing a locked <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>This class does not perform any locking by itself, however it will give access to a locked object and
    /// keep it locked for as long as it is not disposed of.  Normal work-flow for using this object is:
    /// <list type="number">
    /// <item>Lock an object and create an <see cref="IDisposable"/> that will unlock it when disposed of.</item>
    /// <item>Create a <see cref="AsyncLockedObject{T}"/> passing the locked object and the <see cref="IDisposable"/>
    /// created in step 1 to the constructor.</item>
    /// <item>Return the <see cref="AsyncLockedObject{T}"/> to someone that will use the <see cref="Value"/> property to
    /// access the locked object.</item>
    /// <item>When done of the locked object, the user will dispose of the <see cref="AsyncLockedObject{T}"/> which will
    /// also unlock the object (by disposing of the <see cref="IDisposable"/> received in the constructor).</item>
    /// </list>
    /// </remarks>
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
