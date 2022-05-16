using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Make sure ClusterRenderer executes late.
    public class ClusterDisplayManager : SingletonMonoBehaviour<ClusterDisplayManager>
    {
        static ClusterSync GetOrCreateClusterSyncInstance()
        {
            if (ClusterSyncInstance is not {} instance)
            {
                // Creating ClusterSync instance on demand.
                ClusterDebug.Log($"Creating instance of: {nameof(ClusterSync)} on demand.");
                instance = new ClusterSync();
                ServiceLocator.Provide<IClusterSyncState>(instance);
            }

            Debug.Assert(instance != null);
            return instance;
        }

        internal static ClusterSync ClusterSyncInstance =>
            ServiceLocator.TryGet(out IClusterSyncState instance) ? instance as ClusterSync : null;

        protected override void OnAwake()
        {
            SetInstance(this);
            GetOrCreateClusterSyncInstance();

            ClusterDebug.Log("Cluster Display started bootstrap.");

            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            GetOrCreateClusterSyncInstance().EnableClusterDisplay(ClusterParams.FromCommandLine());
        }

        private void OnDisable() => ClusterSyncInstance?.DisableClusterDisplay();
        private void OnApplicationQuit() => ClusterSyncInstance?.ShutdownAllClusterNodes();
    }
}
