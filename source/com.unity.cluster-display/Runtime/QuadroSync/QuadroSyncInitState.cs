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
                LocalNode.HasHardwareSync = true;
                m_Initialized = true;
            }

            return base.DoFrame(newFrame);
        }
    }
}
