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
        [SerializeField]
        bool m_UseMinimalCommandLine;

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

        void OnEnable() => GetOrCreateClusterSyncInstance().EnableClusterDisplay(m_UseMinimalCommandLine ? DetectSettings() : ClusterParams.FromCommandLine());

        static ClusterParams DetectSettings()
        {
            var commandLineArgs = Environment.GetCommandLineArgs();
            var repeaterCountIdx = Array.IndexOf(commandLineArgs, "-repeaters");
            int? repeaterCount =
                repeaterCountIdx > 0 && commandLineArgs.Length > repeaterCountIdx + 1 &&
                int.TryParse(commandLineArgs[repeaterCountIdx + 1], out var repeaterCountArg)
                    ? repeaterCountArg
                    : null;

            ClusterDebug.Log($"Trying to assign ids for {repeaterCount} repeaters");

            var clusterParams = new ClusterParams
            {
                Port = 25690,
                MulticastAddress = "224.0.1.0",
                ClusterLogicSpecified = true,
                CommunicationTimeout = TimeSpan.FromSeconds(5),
                HandshakeTimeout = TimeSpan.FromSeconds(10),
                NodeID = 255    // placeholder
            };

#if NET_4_6
            // Get the node info from the Win32 registry. Use the Set-NodeProperty.ps1 script (look for it in the
            // repo root) to set these values.
            using var clusterKey = Registry.LocalMachine.OpenSubKey("Software\\Unity Technologies\\ClusterDisplay", true);
            if (clusterKey != null)
            {
                clusterParams.NodeID = (byte)(int)clusterKey.GetValue("NodeID");
                clusterParams.RepeaterCount = repeaterCount ?? (int)clusterKey.GetValue("RepeaterCount");
                clusterParams.MulticastAddress = (string)clusterKey.GetValue("MulticastAddress");
                clusterParams.Port = (int)clusterKey.GetValue("MulticastPort");
                clusterParams.AdapterName = (string)clusterKey.GetValue("AdapterName");
            }
            else
            {
#endif
                // If we're running several nodes on the same machine, use a single-access file to keep track
                // of node ids in use.
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

                clusterParams.RepeaterCount = repeaterCount ?? 1;

                Application.quitting += () =>
                {
                    File.Delete(nodeIdFilePath);
                };

#if NET_4_6
            }
#endif

            if (clusterParams.NodeID == 255)
            {
                ClusterDebug.LogError("Cannot assign a node ID.");
                clusterParams.ClusterLogicSpecified = false;
            }


            ClusterDebug.Log($"Auto-assigning node ID {clusterParams.NodeID} (repeaters: {clusterParams.RepeaterCount})");
            // First one to start up gets to be the emitter - node 0
            clusterParams.EmitterSpecified = clusterParams.NodeID == 0;

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
