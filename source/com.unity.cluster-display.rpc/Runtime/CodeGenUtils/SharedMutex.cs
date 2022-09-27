using System;
using System.Threading;

namespace Unity.ClusterDisplay
{
    internal class SharedMutex : IDisposable
    {
        readonly string mutexName;
        Mutex mutex = null;
        
        public SharedMutex(string mutexName) =>
            this.mutexName = mutexName;
        
        public void Lock ()
        {
            try
            {
                if (mutex == null)
                    mutex = Mutex.OpenExisting(mutexName);
                mutex.WaitOne();
            }

            catch
            {
                mutex = new Mutex(true, mutexName, out var isOwned);
            }
        }

        public void Release () =>
            mutex.ReleaseMutex();

        public void Dispose()
        {
            mutex.ReleaseMutex();
            mutex.Close();
            mutex.Dispose();
            mutex = null;
        }
    }
}
