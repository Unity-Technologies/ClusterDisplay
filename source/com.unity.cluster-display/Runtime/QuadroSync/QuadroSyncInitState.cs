using System;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Unity.ClusterDisplay
{
    class QuadroSyncInitState : HardwareSyncInitState
    {
        bool m_Initialized;

        protected QuadroSyncInitState(ClusterNode node)
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
#if UNITY_EDITOR
                ClusterDebug.Log("You are attempting to initialize Quadro Sync swap barriers in the Editor. This will likely fail.");
#endif
                GfxPluginQuadroSyncSystem.ExecuteQuadroSyncCommand(GfxPluginQuadroSyncSystem.EQuadroSyncRenderEvent.QuadroSyncInitialize, new IntPtr());

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

        protected GfxPluginQuadroSyncInitializationState InitializationState { get; private set; }
            = GfxPluginQuadroSyncInitializationState.NotInitialized;

        void ProcessQuadroSyncInitResult()
        {
            InitializationState = GfxPluginQuadroSyncSystem.FetchState().InitializationState;
            if (InitializationState != GfxPluginQuadroSyncInitializationState.NotInitialized)
            {
                Node.UsingNetworkSync = (InitializationState != GfxPluginQuadroSyncInitializationState.Initialized);

                // Initialization finished, we do not need to be called again
                PlayerLoopExtensions.DeregisterUpdate<CheckQuadroInitState>(ProcessQuadroSyncInitResult);

                // Initialization failed
                if (InitializationState is not GfxPluginQuadroSyncInitializationState.Initialized)
                {
                    // Disable logging so we don't pollute the output
                    GfxPluginQuadroSyncSystem.DisableLogging();
                }
            }
        }

        /// <summary>
        /// If we are waiting for a frame to be presented for more than 150 milliseconds it means that we are blocked
        /// waiting for other nodes to render the frame and so the barrier is up and running.
        /// </summary>
        /// <remarks>150 was chosen as it is a few time longer than the slower rate that we should expect running at (24
        /// fps) and presenting should in theory never wait more than 1 frame (or 2 or maybe 3 if things are going
        /// really bad).</remarks>
        protected static readonly TimeSpan k_BlockDelay = TimeSpan.FromMilliseconds(150);
        /// <summary>
        /// We need to repeat messages once in a while in case it gets lost (UDP is not reliable).
        /// </summary>
        protected static readonly TimeSpan k_RepeatMessageInterval = TimeSpan.FromMilliseconds(25);
    }
}
