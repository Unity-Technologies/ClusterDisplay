using System;
using System.Threading;

namespace Unity.ClusterDisplay.Editor.SourceGenerators
{
    public class SharedMutex : IDisposable
    {
        readonly string k_MutexName;
        Mutex mutex = null;
        
        public SharedMutex(string mutexName) => this.k_MutexName = mutexName;
        
        public void Lock ()
        {
            try
            {
                if (mutex == null)
                {
                    mutex = Mutex.OpenExisting(k_MutexName);
                }

                while (!mutex.WaitOne()) {}
            }

            catch
            {
                mutex = new Mutex(true, k_MutexName, out var isOwned);
                while (!isOwned && !mutex.WaitOne()) {}
            }
        }

        public void Release () => mutex.ReleaseMutex();

        public void Dispose()
        {
            mutex.Close();
            mutex.Dispose();
            mutex = null;
        }
    }
}
