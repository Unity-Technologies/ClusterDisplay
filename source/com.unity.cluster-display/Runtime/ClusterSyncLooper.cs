using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Unity.ClusterDisplay.Utils;
using UnityEngine.PlayerLoop;

namespace Unity.ClusterDisplay
{
    internal static class ClusterSyncLooper
    {
        internal enum TickType
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

        internal delegate void OnInstanceTick(TickType tickType);

        internal delegate void OnInstanceDoPreFrame();
        internal delegate void OnInstanceDoFrame(ref bool readyToProceed, ref bool isTerminated);
        internal delegate void OnInstancePostFrame();
        internal delegate void OnInstanceDoLateFrame(ref bool readForLateFrame);

        /// <summary>
        ///  Delegate to execute callbacks before we enter the while loop. 
        /// </summary>
        internal static OnInstanceDoPreFrame onInstanceDoPreFrame;
        /// <summary>
        ///  Delegate to execute callbacks while were waiting for the network fence. 
        /// </summary>
        internal static OnInstanceDoFrame onInstanceDoFrame;
        /// <summary>
        ///  Delegate to execute callbacks after the network fence has been raised and were about to enter the frame. 
        /// </summary>
        internal static OnInstancePostFrame onInstancePostFrame;
        /// <summary>
        ///  Delegate to execute callbacks to poll ACKs after we've finished the frame, and were about to render. 
        /// </summary>
        internal static OnInstanceDoLateFrame onInstanceDoLateFrame;

        /// <summary>
        ///  Each time we tick on DoPreFrame, DoFrame, PostFrame, and DoLateFrame, this delegate is executed. 
        /// </summary>
        internal static OnInstanceTick onInstanceTick;

        struct ClusterDisplayStartFrame { }
        struct ClusterDisplayLateUpdate { }

        internal static void InjectSynchPointInPlayerLoop()
        {
            PlayerLoopExtensions.RegisterUpdate<TimeUpdate.WaitForLastPresentationAndUpdateTime, ClusterDisplayStartFrame>(
                SystemUpdate);
            PlayerLoopExtensions.RegisterUpdate<PostLateUpdate, ClusterDisplayLateUpdate>(SystemLateUpdate);
        }

        internal static void RemoveSynchPointFromPlayerLoop()
        {
            PlayerLoopExtensions.DeregisterUpdate<ClusterDisplayStartFrame>(SystemUpdate);
            PlayerLoopExtensions.DeregisterUpdate<ClusterDisplayLateUpdate>(SystemLateUpdate);
        }

        internal static void SystemUpdate()
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

        internal static void SystemLateUpdate()
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
