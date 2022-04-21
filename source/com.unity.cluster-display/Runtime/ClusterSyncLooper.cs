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
            DoPreFrame,
            DoFrame,
            PostFrame,
            DoLateFrame
        }

        internal delegate void OnInstanceTick(TickType tickType);

        internal delegate void OnInstanceDoPreFrame();
        internal delegate void OnInstanceDoFrame(ref bool readyToProceed, ref bool isTerminated);
        internal delegate void OnInstancePostFrame();
        internal delegate void OnInstanceDoLateFrame(ref bool readForLateFrame);

        internal static OnInstanceDoPreFrame onInstanceDoPreFrame;
        internal static OnInstanceDoFrame onInstanceDoFrame;
        internal static OnInstancePostFrame onInstancePostFrame;
        internal static OnInstanceDoLateFrame onInstanceDoLateFrame;

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
                allReadyToProceed = true;
                allIsTerminated = false;

                onInstanceDoFrame?.Invoke(ref allReadyToProceed, ref allIsTerminated);
                onInstanceTick?.Invoke(TickType.DoFrame);

            } while (!allReadyToProceed && !allIsTerminated);

            onInstancePostFrame?.Invoke();
            onInstanceTick?.Invoke(TickType.PostFrame);
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
