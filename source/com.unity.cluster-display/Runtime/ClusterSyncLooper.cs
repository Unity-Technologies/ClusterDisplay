using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.Utils;
using UnityEngine.PlayerLoop;

using DoFrameFunc = System.Func<(System.Boolean readyToProceed, System.Boolean isTerminated)>;
using DoLateFrameFunc = System.Func<System.Boolean>;

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

        /// <summary>
        ///  Delegate to execute callbacks before we enter the while loop.
        /// </summary>
        public static event Action onInstanceDoPreFrame;
        /// <summary>
        ///  Delegate to execute callbacks while were waiting for the network fence.
        /// </summary>
        public static event DoFrameFunc onInstanceDoFrame
        {
            add => s_DoFrameListeners.Add(value);
            remove => s_DoFrameListeners.Remove(value);
        }
        static List<DoFrameFunc> s_DoFrameListeners = new();
        /// <summary>
        ///  Delegate to execute callbacks after the network fence has been raised and were about to enter the frame.
        /// </summary>
        public static event Action onInstancePostFrame;
        /// <summary>
        ///  Delegate to execute callbacks to poll ACKs after we've finished the frame, and were about to render.
        /// </summary>
        public static event DoLateFrameFunc onInstanceDoLateFrame
        {
            add => s_DoLateFrameListeners.Add(value);
            remove => s_DoLateFrameListeners.Remove(value);
        }
        static List<DoLateFrameFunc> s_DoLateFrameListeners = new();

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

                foreach (var doFrame in s_DoFrameListeners)
                {
                    var instanceStatus = doFrame.Invoke();
                    allReadyToProceed &= instanceStatus.readyToProceed;
                    allIsTerminated |= instanceStatus.isTerminated;
                }

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

                foreach (var doLateFrame in s_DoLateFrameListeners)
                {
                    bool instanceReadyForNextFrame = doLateFrame.Invoke();
                    allReadyForNextFrame &= instanceReadyForNextFrame;
                }

                onInstanceTick?.Invoke(TickType.DoLateFrame);
            }
        }
    }
}
