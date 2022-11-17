using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
#if NET_4_6
using Microsoft.Win32;
#endif
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1)] // Make sure ClusterDisplay logic initializes early
    public class ClusterDisplayManager : SingletonMonoBehaviour<ClusterDisplayManager>
    {
        static ClusterSync GetOrCreateClusterSyncInstance()
        {
            if (ClusterSyncInstance is not { } instance)
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
            GetOrCreateClusterSyncInstance();

            ClusterDebug.Log("Cluster Display started bootstrap.");

            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        void OnEnable() => GetOrCreateClusterSyncInstance().EnableClusterDisplay(ClusterParams.FromCommandLine());

        void CleanUp()
        {
            ClusterSyncInstance?.DisableClusterDisplay();
            ServiceLocator.Withdraw<IClusterSyncState>();
        }

        private void OnDisable() => CleanUp();
        private void OnApplicationQuit() => CleanUp();
    }
}
