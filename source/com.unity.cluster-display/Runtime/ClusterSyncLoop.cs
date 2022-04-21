using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    internal partial class ClusterSync : IClusterSyncState
    {
        internal enum TickType
        {
            DoPreFrame,
            DoFrame,
            PostFrame,
            DoLateFrame
        }

        internal delegate void OnInstanceTick(TickType tickType);

        private delegate void OnInstanceDoPreFrame();
        private delegate void OnInstanceDoFrame(out bool readyToProceed, out bool isTerminated);
        private delegate void OnInstancePostFrame();
        private delegate void OnInstanceDoLateFrame(out bool readForLateFrame);

        private static OnInstanceDoPreFrame onInstanceDoPreFrame;
        private static OnInstanceDoFrame onInstanceDoFrame;
        private static OnInstancePostFrame onInstancePostFrame;
        private static OnInstanceDoLateFrame onInstanceDoLateFrame;

        internal static OnInstanceTick onInstanceTick;

        internal void PreFrame ()
        {
            try
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

            catch (Exception e)
            {
                OnException(e);
            }

            finally
            {
                OnFinally();
            }
        }

        private bool newFrame = false;
        internal void DoFrame (out bool readyToProceed, out bool isTerminated)
        {
            readyToProceed = true;
            isTerminated = false;

            try
            {
                readyToProceed = LocalNode.ReadyToProceed;
                isTerminated = state.IsTerminated;

                if (!LocalNode.DoFrame(newFrame))
                {
                    // Game Over!
                    syncState.SetIsTerminated(true);
                }

                readyToProceed = LocalNode.ReadyToProceed;
                isTerminated = state.IsTerminated;
                newFrame = false;
            }

            catch (Exception e)
            {
                OnException(e);
            }

            finally
            {
                OnFinally();
            }
        }

        private void PostFrame ()
        {
            try
            {
                LocalNode.EndFrame();

                m_StartDelayMonitor.SampleNow();
                InstanceLog(GetDebugString());
                InstanceLog($"(Frame: {m_CurrentFrameID}): Stepping to next frame.");

                syncState.SetFrame(++m_CurrentFrameID);
                onInstanceTick?.Invoke(TickType.PostFrame);
            }

            catch (Exception e)
            {
                OnException(e);
            }

            finally
            {
                OnFinally();
            }
        }

        private static void SystemUpdate()
        {
            bool allReadyToProceed, allIsTerminated;

            onInstanceDoPreFrame?.Invoke();
            onInstanceTick.Invoke(TickType.DoPreFrame);

            do
            {
                allReadyToProceed = true;
                allIsTerminated = false;

                onInstanceDoFrame?.Invoke(out allReadyToProceed, out allIsTerminated);
                onInstanceTick?.Invoke(TickType.DoFrame);

            } while (!allReadyToProceed && !allIsTerminated);

            onInstancePostFrame.Invoke();
        }

        internal void DoLateFrame (out bool readyForLateFrame)
        {
            readyForLateFrame = true;

            try
            {
                m_EndDelayMonitor.RefPoint();
                LocalNode.DoLateFrame();
                readyForLateFrame = LocalNode.ReadyForNextFrame;
                m_EndDelayMonitor.SampleNow();
            }

            catch (Exception e)
            {
                OnException(e);
            }

            finally
            {
                OnFinally();
            }
        }

        private static void SystemLateUpdate()
        {
            bool allReadyForNextFrame = false;

            while (!allReadyForNextFrame)
            {
                allReadyForNextFrame = true;
                onInstanceDoLateFrame?.Invoke(out allReadyForNextFrame);
                onInstanceTick?.Invoke(TickType.DoLateFrame);
            }
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
