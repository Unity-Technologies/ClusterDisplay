using System;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    class QuadroSyncInitState : HardwareSyncInitState
    {
        bool m_Initialized;

        internal QuadroSyncInitState(ClusterNode node)
            : base(node) { }

        protected override NodeState DoFrame(bool newFrame)
        {
            if (!m_Initialized)
            {
                ClusterDebug.Log("Enabling VSYNC");
                QualitySettings.vSyncCount = 1;
                QualitySettings.maxQueuedFrames = 1;

                ClusterDebug.Log("Initializing Quadro Sync.");
                GfxPluginQuadroSyncSystem.Instance.ExecuteQuadroSyncCommand(GfxPluginQuadroSyncSystem.EQuadroSyncRenderEvent.QuadroSyncInitialize, new IntPtr());

                // We won't know immediately if everything worked (and if we are really using hardware acceleration), so
                // continue to peek at the state to know when initialization of QuadroSync is done.
                CarriedPreDoFrame ??= new();
                CarriedPreDoFrame.Add(() =>
                {
                    var initializationState = GfxPluginQuadroSyncSystem.Instance.FetchState().InitializationState;
                    if (initializationState == GfxPluginQuadroSyncInitializationState.NotInitialized)
                    {
                        // Still not fully initialized, we need to continue checking...
                        return true;
                    }
                    else
                    {
                        // Initialization finished, we do not need to be called again
                        LocalNode.HasHardwareSync =
                            (initializationState == GfxPluginQuadroSyncInitializationState.Initialized);
                        return false;
                    }
                });

                // The initialization is "done" (or at least, triggered and will conclude shortly)
                m_Initialized = true;
            }

            return base.DoFrame(newFrame);
        }
    }
}
