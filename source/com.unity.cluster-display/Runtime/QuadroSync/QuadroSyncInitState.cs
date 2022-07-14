using System;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Unity.ClusterDisplay
{
    class QuadroSyncInitState: HardwareSyncInitState
    {
        bool m_Initialized;

        internal QuadroSyncInitState(ClusterNode node)
            : base(node) { }

        struct CheckQuadroInitState { }

        protected override NodeState DoFrameImplementation()
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
                // Remark: We can't use ClusterSyncLooper.onInstanceDoFrame as this method is being called from it and
                // we would be modifying a collection we are currently enumerating.  So let's instead register directly
                // in the PlayerLoopExtensions.
                PlayerLoopExtensions.RegisterUpdate<TimeUpdate.WaitForLastPresentationAndUpdateTime, CheckQuadroInitState>(
                    ProcessQuadroSyncInitResult);

                // The initialization is "done" (or at least, triggered and will conclude shortly)
                m_Initialized = true;
            }

            return base.DoFrameImplementation();
        }

        void ProcessQuadroSyncInitResult()
        {
            var initializationState = GfxPluginQuadroSyncSystem.Instance.FetchState().InitializationState;
            if (initializationState != GfxPluginQuadroSyncInitializationState.NotInitialized)
            {
                Node.HasHardwareSync = (initializationState == GfxPluginQuadroSyncInitializationState.Initialized);

                // Initialization finished, we do not need to be called again
                PlayerLoopExtensions.DeregisterUpdate<CheckQuadroInitState>(ProcessQuadroSyncInitResult);
            }
        }
    }
}
