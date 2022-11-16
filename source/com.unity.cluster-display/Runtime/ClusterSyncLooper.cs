using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.Utils;
using Unity.Profiling;
using UnityEngine.PlayerLoop;

namespace Unity.ClusterDisplay
{
    public static class ClusterSyncLooper
    {
        /// <summary>
        ///  Delegate to execute callbacks before we enter the while loop.
        /// </summary>
        public static event Action onInstanceDoPreFrame;

        /// <summary>
        ///  Delegate to execute callbacks while were waiting for the network fence.
        /// </summary>
        public static event Action onInstanceDoFrame;
        /// <summary>
        ///  Delegate to execute callbacks after the network fence has been raised and were about to enter the frame.
        /// </summary>
        public static event Action onInstancePostFrame;

        /// <summary>
        ///  Delegate to execute callbacks to poll ACKs after we've finished the frame, and were about to render.
        /// </summary>
        public static event Action onInstanceDoLateFrame;

        struct ClusterDisplayStartFrame { }
        struct ClusterDisplayLateUpdate { }

        public static void InjectSynchPointInPlayerLoop()
        {
            PlayerLoopExtensions.RegisterUpdate<TimeUpdate.WaitForLastPresentationAndUpdateTime, ClusterDisplayStartFrame>(
                SystemUpdate);
            PlayerLoopExtensions.RegisterUpdate<PostLateUpdate, ClusterDisplayLateUpdate>(SystemLateUpdate);
        }

        public static void RemoveSynchPointFromPlayerLoop()
        {
            PlayerLoopExtensions.DeregisterUpdate<ClusterDisplayStartFrame>(SystemUpdate);
            PlayerLoopExtensions.DeregisterUpdate<ClusterDisplayLateUpdate>(SystemLateUpdate);
        }

        public static void SystemUpdate()
        {
            using (s_MarkerPreFrame.Auto())
            {
                onInstanceDoPreFrame?.Invoke();
            }

            using (s_MarkerDoFrame.Auto())
            {
                onInstanceDoFrame?.Invoke();
            }

            using (s_MarkerPostFrame.Auto())
            {
                onInstancePostFrame?.Invoke();
            }
        }

        public static void SystemLateUpdate()
        {
            using (s_MarkerLateFrame.Auto())
            {
                onInstanceDoLateFrame?.Invoke();
            }
        }

        static ProfilerMarker s_MarkerPreFrame = new("ClusterSync.PreFrame");
        static ProfilerMarker s_MarkerDoFrame = new("ClusterSync.DoFrame");
        static ProfilerMarker s_MarkerPostFrame = new("ClusterSync.PostFrame");
        static ProfilerMarker s_MarkerLateFrame = new("ClusterSync.LateFrame");
    }
}
