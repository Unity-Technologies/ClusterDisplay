using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Win32;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Make sure ClusterRenderer executes late.
    public class ClusterDisplayManager : SingletonMonoBehaviour<ClusterDisplayManager>
    {
        [SerializeField]
        bool m_IgnoreCommandLine;

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

        void OnEnable() => GetOrCreateClusterSyncInstance().EnableClusterDisplay(m_IgnoreCommandLine ? DetectSettings() : ClusterParams.FromCommandLine());

        static ClusterParams DetectSettings()
        {
            var clusterParams = new ClusterParams
            {
                Port = 25690,
                MulticastAddress = "224.0.1.0",
                ClusterLogicSpecified = true,
                CommunicationTimeout = TimeSpan.FromSeconds(5),
                HandshakeTimeout = TimeSpan.FromSeconds(10),
                NodeID = 255
            };

            using var clusterKey = Registry.LocalMachine.OpenSubKey("Software\\Unity Technologies\\ClusterDisplay", true);
            if (clusterKey != null)
            {
                clusterParams.NodeID = (byte)clusterKey.GetValue("NodeID");
            }
            else
            {
                var retries = 0;
                var nodeIdFilePath = Path.Combine(Path.GetTempPath(), Application.productName + ".node");
                while (clusterParams.NodeID == 255 && retries < 10)
                {
                    try
                    {
                        using var fileStream = new FileStream(nodeIdFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                        using var reader = new StreamReader(fileStream);
                        if (!byte.TryParse(reader.ReadLine(), out clusterParams.NodeID))
                        {
                            clusterParams.NodeID = 0;
                        }

                        fileStream.Position = 0;
                        using var writer = new StreamWriter(fileStream);
                        writer.Write(clusterParams.NodeID + 1);
                        fileStream.SetLength(fileStream.Position);
                    }
                    catch (Exception ex)
                    {
                        retries++;
                        ClusterDebug.Log($"Unable to access node ID file: {ex.Message}");
                        Thread.Sleep(500);
                    }
                }

                Application.quitting += () =>
                {
                    File.Delete(nodeIdFilePath);
                };

                ClusterDebug.Log($"Auto-assigning node ID {clusterParams.NodeID}");
            }

            if (clusterParams.NodeID == 255)
            {
                ClusterDebug.LogError("Cannot assign a node ID.");
                clusterParams.ClusterLogicSpecified = false;
            }

            // First one to start up gets to be the emitter - node 0
            clusterParams.EmitterSpecified = clusterParams.NodeID == 0;
            clusterParams.RepeaterCount = clusterParams.EmitterSpecified ? 1 : 0;

            return clusterParams;
        }

        void CleanUp()
        {
            ClusterSyncInstance?.DisableClusterDisplay();
            ServiceLocator.Withdraw<ClusterSync>();
        }

        private void OnDisable() => CleanUp();
        private void OnApplicationQuit() => CleanUp();
    }
}
