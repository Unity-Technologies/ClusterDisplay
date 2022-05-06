using System;
using Unity.ClusterDisplay.Utils;
using UnityEngine.PlayerLoop;

using DoFrameFunc = System.Action<Unity.ClusterDisplay.DoFrameState>;
using DoLateFrameFunc = System.Action<Unity.ClusterDisplay.DoLateFrameState>;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// State to be updated by onInstanceDoFrame callbacks.
    /// </summary>
    class DoFrameState
    {
        /// <summary>
        /// Are all callbacks ready to proceed into the frame?
        /// </summary>
        public bool AllReadyToProceed { get; private set; } = true;
        /// <summary>
        /// Has at least one callback requested termination of the system?
        /// </summary>
        public bool OneAsksForTermination { get; private set; } = true;

        /// <summary>
        /// Method to be called between invocations of onInstanceDoFrame to clear the state to a brand new one.
        /// </summary>
        public void Reset()
        {
            AllReadyToProceed = true;
            OneAsksForTermination = false;
        }

        /// <summary>
        /// Method to be called at least once by every onInstanceDoFrame callback to provide their input on the state
        /// of things.
        /// </summary>
        /// <param name="readyToProceed">Is the callback ready to proceed into the frame?</param>
        /// <param name="asksForTermination">Does the callback requests termination of the system?</param>
        public void Update(bool readyToProceed, bool asksForTermination)
        {
            AllReadyToProceed &= readyToProceed;
            OneAsksForTermination |= asksForTermination;
        }
    }

    /// <summary>
    /// State to be updated by onInstanceDoLateFrame callbacks.
    /// </summary>
    class DoLateFrameState
    {
        /// <summary>
        /// Are all callbacks ready to proceed to the next frame?
        /// </summary>
        public bool AllReadyForNextFrame { get; private set;  }= true;

        /// <summary>
        /// Method to be called between invocations of onInstanceDoLateFrame to clear the state to a brand new one.
        /// </summary>
        public void Reset()
        {
            AllReadyForNextFrame = true;
        }

        /// <summary>
        /// Method to be called at least once by every onInstanceDoLateFrame callback to provide input on the state of
        /// things.
        /// </summary>
        /// <param name="readyForNextFrame">Is the callback ready to proceed to the next frame?</param>
        public void Update(bool readyForNextFrame)
        {
            AllReadyForNextFrame &= readyForNextFrame;
        }
    }

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
        public static event DoFrameFunc onInstanceDoFrame;
        /// <summary>
        ///  Delegate to execute callbacks after the network fence has been raised and were about to enter the frame.
        /// </summary>
        public static event Action onInstancePostFrame;
        /// <summary>
        ///  Delegate to execute callbacks to poll ACKs after we've finished the frame, and were about to render.
        /// </summary>
        public static event DoLateFrameFunc onInstanceDoLateFrame;

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

        private static readonly DoFrameState m_DoFrameState = new();
        public static void SystemUpdate()
        {
            bool allReadyToProceed, allIsTerminated;

            onInstanceDoPreFrame?.Invoke();
            onInstanceTick?.Invoke(TickType.DoPreFrame);

            do
            {
                m_DoFrameState.Reset();
                onInstanceDoFrame?.Invoke(m_DoFrameState);

                onInstanceTick?.Invoke(TickType.DoFrame);

            } while (!m_DoFrameState.AllReadyToProceed && !m_DoFrameState.OneAsksForTermination);

            onInstancePostFrame?.Invoke();
            onInstanceTick?.Invoke(TickType.PostFrame);
            // After this were entering the frame.
        }

        private static readonly DoLateFrameState m_DoLateFrameState = new();
        public static void SystemLateUpdate()
        {
            do
            {
                m_DoLateFrameState.Reset();
                onInstanceDoLateFrame?.Invoke(m_DoLateFrameState);

                onInstanceTick?.Invoke(TickType.DoLateFrame);
            } while (!m_DoLateFrameState.AllReadyForNextFrame);
        }
    }
}
