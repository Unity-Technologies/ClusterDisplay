using System;
using System.Threading;

namespace Unity.ClusterDisplay
{
    public class SharedMutex : IDisposable
    {
        private readonly string mutexName;
        private Mutex mutex = null;
        
        public SharedMutex(string mutexName) =>
            this.mutexName = mutexName;
        
        public void Lock ()
        {
            try
            {
                if (mutex == null)
                    mutex = Mutex.OpenExisting(mutexName);

                while (!mutex.WaitOne()) {}
            }

            catch
            {
                mutex = new Mutex(true, mutexName, out var isOwned);
                while (!isOwned && !mutex.WaitOne()) {}
            }
        }

        public void Release () =>
            mutex.ReleaseMutex();

        public void Dispose()
        {
            mutex.Close();
            mutex.Dispose();
            mutex = null;
        }
    }
}