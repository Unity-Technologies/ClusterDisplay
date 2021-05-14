using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public static class ClusterDisplayState
    {
        internal interface IClusterDisplayStateSetter
        {
            void SetIsMaster(bool isMaster);
            void SetCLusterLogicEnabled(bool clusterLogicEnabled);
            void SetIsTerminated(bool isTerminated);
        }

        private class ClusterDisplayStateStore : IClusterDisplayStateSetter
        {
            public bool m_IsMaster = false;
            public bool m_ClusterLogicEnabled = false;
            public bool m_Terminated = false;

            public void SetCLusterLogicEnabled(bool clusterLogicEnabled) => this.m_ClusterLogicEnabled = clusterLogicEnabled;
            public void SetIsMaster(bool isMaster) => this.m_IsMaster = isMaster;

            public void SetIsTerminated(bool isTerminated) => m_Terminated = isTerminated;
        }

        private readonly static ClusterDisplayStateStore stateStore = new ClusterDisplayStateStore();
        internal static IClusterDisplayStateSetter GetStateStoreSetter () => stateStore; 

        public static bool IsMaster => stateStore.m_IsMaster;

        /// <summary>
        /// Enables or disables the Cluster Display Synchronization. Beware that once the logic is disabled, it cannot be reenabled without restarting the application.
        /// </summary>
        public static bool ClusterLogicEnabled => stateStore.m_ClusterLogicEnabled;

        /// <summary>
        /// Getter that returns if there exists a ClusterSync instance and the synchronization has been enabled.
        /// </summary>
        public static bool IsActive => ClusterSync.TryGetInstance(out var instance) && stateStore.m_ClusterLogicEnabled;

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// </summary>
        public static bool IsTerminated => stateStore.m_Terminated;
    }
}
