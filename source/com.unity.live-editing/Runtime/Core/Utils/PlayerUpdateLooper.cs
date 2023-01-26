using System;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Unity.LiveEditing.LowLevel
{
    public class PlayerUpdateLooper : ILooper, IDisposable
    {
        public Action Update { get; set; }

        struct LooperUpdate { }

        public PlayerUpdateLooper()
        {
            PlayerLoopExtensions.RegisterUpdate<Update, LooperUpdate>(DoUpdate);
        }

        public void Dispose()
        {
            PlayerLoopExtensions.DeregisterUpdate<LooperUpdate>(DoUpdate);
            GC.SuppressFinalize(this);
        }

        void DoUpdate()
        {
            Update?.Invoke();
        }

        ~PlayerUpdateLooper()
        {
            Dispose();
        }
    }
}
