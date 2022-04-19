using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    internal partial class ClusterSync : IClusterSyncState
    {
        internal delegate void OnSyncTick(TimeSpan elapsed);
        static internal OnSyncTick onSyncTick;
        static readonly Stopwatch m_OnSyncTickTime = new Stopwatch();

        private void PreFrame ()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current.kKey.isPressed || Keyboard.current.qKey.isPressed)
                Quit();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyUp(KeyCode.K) || Input.GetKeyUp(KeyCode.Q))
                Quit();
#endif

            if (m_Debugging)
            {
                if (m_NewFrame)
                    m_FrameRatePerf.SampleNow();

                if (!LocalNode.DoFrame(m_NewFrame))
                {
                    // Game Over!
                    syncState.SetIsTerminated(true);
                }

                m_NewFrame = LocalNode.ReadyToProceed;
                if (m_NewFrame)
                {
                    m_StartDelayMonitor.SampleNow();
                    m_StartDelayMonitor.RefPoint();
                }
            }

            else
            {
                m_FrameRatePerf.SampleNow();
                m_FrameRatePerf.RefPoint();

                InstanceLog($"(Frame: {m_CurrentFrameID}): Node is starting frame.");

                m_StartDelayMonitor.RefPoint();
            }

            newFrame = true;
        }

        private bool newFrame = false;
        private void DoFrame (out bool readyToProceed, out bool isTerminated)
        {
            readyToProceed = LocalNode.ReadyToProceed;
            isTerminated = state.IsTerminated;

            if (readyToProceed)
            {
                newFrame = false;
                return;
            }

            if (!LocalNode.DoFrame(newFrame))
            {
                // Game Over!
                syncState.SetIsTerminated(true);
            }

            readyToProceed = LocalNode.ReadyToProceed;
            isTerminated = state.IsTerminated;
            newFrame = false;
        }

        private void PostFrame ()
        {
            LocalNode.EndFrame();

            m_StartDelayMonitor.SampleNow();
            InstanceLog(GetDebugString());
            InstanceLog($"(Frame: {m_CurrentFrameID}): Stepping to next frame.");

            syncState.SetFrame(++m_CurrentFrameID);
        }

        private static void SystemUpdate()
        {
            var instances = k_Instances.Values.ToArray();
            bool allReadyToProceed, allIsTerminated;

            try
            {
                foreach (var instance in instances)
                {
                    instance.PreFrame();
                }

                do
                {
                    allReadyToProceed = true;
                    allIsTerminated = false;

                    onSyncTick?.Invoke(m_OnSyncTickTime.Elapsed);
                    m_OnSyncTickTime.Restart();

                    foreach (var instance in instances)
                    {

                        PushInstance(instance.m_InstanceName);
                        if (instance.m_Debugging)
                        {
                            continue;
                        }

                        instance.DoFrame(out var readyToProceed, out var isTerminated);

                        allReadyToProceed &= readyToProceed;
                        allIsTerminated &= isTerminated;
                    }

                } while (!allReadyToProceed && !allIsTerminated);

                foreach (var instance in instances)
                {
                    instance.PostFrame();
                }
            }

            // Since we've called PushInstance within the while loop, Instance
            // should be the correct instance that threw the exeception.
            catch (Exception e)
            {
                Instance.OnException(e);
            }

            finally
            {
                Instance.OnFinally();
            }

            PopInstance();
        }

        private void DoLateFrame (out bool readyForLateFrame)
        {
            m_EndDelayMonitor.RefPoint();
            LocalNode.DoLateFrame();
            readyForLateFrame = LocalNode.ReadyForNextFrame;
            m_EndDelayMonitor.SampleNow();
        }

        private static void SystemLateUpdate()
        {
            var instances = k_Instances.Values.ToArray();
            bool allReadyForNextFrame = false;

            try
            {
                while (!allReadyForNextFrame)
                {
                    allReadyForNextFrame = true;
                    foreach (var instance in instances)
                    {
                        PushInstance(instance.m_InstanceName);
                        instance.DoLateFrame(out var readyForLateFrame);
                        allReadyForNextFrame &= readyForLateFrame;
                    }
                }
            }

            catch (Exception e)
            {
                Instance.OnException(e);
            }

            finally
            {
                Instance.OnFinally();
            }

            PopInstance();
        }

        private void OnException (Exception e)
        {
            syncState.SetIsTerminated(true);

            InstanceLog($"Encountered exception.");
            ClusterDebug.LogException(e);
        }

        private void OnFinally ()
        {
            if (state.IsTerminated)
            {
                CleanUp();
            }
        }
    }
}
