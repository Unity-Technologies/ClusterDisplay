using System;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Same objective as lock (some object) { ... } but Task friendly.
    /// </summary>
    /// <remarks>Allow locking, await on something and then unlocking.  Shouldn't be done with a simple lock ( ... ) or
    /// monitor as there is no guarantee we are executed on the same thread after the await call.<br/><br/>
    /// Inspired from <see href="https://stackoverflow.com/questions/21011179/how-to-protect-resources-that-may-be-used-in-a-multi-threaded-or-async-environme/21011273#21011273">
    /// StackOverflow</see>.</remarks>
    public class AsyncLock
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public AsyncLock()
        {
            m_Releaser = new Releaser(m_Semaphore);
        }

        /// <summary>
        /// Used to lock in a synchronous way.
        /// </summary>
        /// <remarks>Unlock by disposing of the returned <see cref="IDisposable"/>.</remarks>
        public IDisposable Lock()
        {
            m_Semaphore.Wait();
            return m_Releaser;
        }

        /// <summary>
        /// Used to lock asynchronously (returns a task that will have the lock once completed).
        /// </summary>
        /// <remarks>Unlock by disposing of the returned <see cref="IDisposable"/>.</remarks>
        public async Task<IDisposable> LockAsync()
        {
            await m_Semaphore.WaitAsync();
            return m_Releaser;
        }

        /// <summary>
        /// Are we locked?
        /// </summary>
        /// <remarks>Not as strict as Monitor.IsEntered would be as we are not checking for only this thread...  Returns
        /// <c>true</c> as soon as a thread have locked the <see cref="AsyncLock"/>.</remarks>
        public bool IsLocked => m_Semaphore.CurrentCount == 0;

        class Releaser : IDisposable
        {
            public Releaser(SemaphoreSlim semaphore)
            {
                m_Semaphore = semaphore;
            }
            public void Dispose()
            {
                m_Semaphore.Release();
            }

            readonly SemaphoreSlim m_Semaphore;
        }

        readonly SemaphoreSlim m_Semaphore = new SemaphoreSlim(1, 1);
        readonly IDisposable m_Releaser;
    }
}
