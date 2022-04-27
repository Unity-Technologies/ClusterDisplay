using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Unity.ClusterDisplay.Utils;
using UnityEngine.PlayerLoop;

namespace Unity.ClusterDisplay
{
    static class ClusterSyncLooper
    {
        public enum TickType
        {
            /// <summary>
            ///  Before we enter the while loop. 
            /// </summary>
            DoPreFrame,
            /// <summary>
            /// Do frame within the network fence.
            /// </summary>
            DoFrame,
            /// <summary>
            /// We've exited the network fence and were about to enter the frame.
            /// </summary>
            PostFrame,
            /// <summary>
            /// In late update right before rendering.
            /// </summary>
            DoLateFrame
        }

        public delegate void OnInstanceDoFrame(ref bool readyToProceed, ref bool isTerminated);
        public delegate void OnInstanceDoLateFrame(ref bool readForLateFrame);

        /// <summary>
        ///  Delegate to execute callbacks before we enter the while loop. 
        /// </summary>
        public static event Action onInstanceDoPreFrame;
        /// <summary>
        ///  Delegate to execute callbacks while were waiting for the network fence. 
        /// </summary>
        public static event OnInstanceDoFrame onInstanceDoFrame;
        /// <summary>
        ///  Delegate to execute callbacks after the network fence has been raised and were about to enter the frame. 
        /// </summary>
        public static event Action onInstancePostFrame;
        /// <summary>
        ///  Delegate to execute callbacks to poll ACKs after we've finished the frame, and were about to render. 
        /// </summary>
        public static event OnInstanceDoLateFrame onInstanceDoLateFrame;

        /// <summary>
        ///  Each time we tick on DoPreFrame, DoFrame, PostFrame, and DoLateFrame, this delegate is executed. 
        /// </summary>
        public static event Action<TickType> onInstanceTick;

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
            bool allReadyToProceed, allIsTerminated;

            onInstanceDoPreFrame?.Invoke();
            onInstanceTick?.Invoke(TickType.DoPreFrame);

            do
            {
                // By default, we are ready to proceed into the frame unless specified otherwise
                // by the methods registered with onInstanceDoFrame.
                allReadyToProceed = true;
                // By default, we are not terminating unless specified by methods registered with onInstanceDoFrame.
                allIsTerminated = false;

                onInstanceDoFrame?.Invoke(ref allReadyToProceed, ref allIsTerminated);
                onInstanceTick?.Invoke(TickType.DoFrame);

            } while (!allReadyToProceed && !allIsTerminated);

            onInstancePostFrame?.Invoke();
            onInstanceTick?.Invoke(TickType.PostFrame);
            // After this were entering the frame.
        }

        public static void SystemLateUpdate()
        {
            bool allReadyForNextFrame = false;

            while (!allReadyForNextFrame)
            {
                allReadyForNextFrame = true;
                onInstanceDoLateFrame?.Invoke(ref allReadyForNextFrame);
                onInstanceTick?.Invoke(TickType.DoLateFrame);
            }
        }
    }
}
