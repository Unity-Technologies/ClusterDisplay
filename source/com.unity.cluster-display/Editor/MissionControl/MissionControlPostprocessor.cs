using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.ClusterDisplay.MissionControl;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Unity.ClusterDisplay
{
    public class MissionControlPostprocessor: IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                HashSet<string> buildFiles = new(report.GetFiles()
                    .Select(f => f.path.Replace('/', Path.DirectorySeparatorChar)));
                Postprocess(report.summary.outputPath, p => buildFiles.Contains(p));
            }
            catch (Exception e)
            {
                throw new BuildFailedException(e);
            }
        }

        static void Postprocess(string pathToBuiltProjectExe, Func<string, bool> filesFilter)
        {
            var missionControlSettings = MissionControlSettings.Current;
            if (!missionControlSettings.Instrument)
            {
                return;
            }
            var pathToBuiltProject = Path.GetDirectoryName(pathToBuiltProjectExe)!;
            var projectName = Path.GetFileNameWithoutExtension(pathToBuiltProjectExe);

            var catalogDirectives = new[]
            {
                new CatalogBuilderDirective(),
                new CatalogBuilderDirective()
            };

            // Ask Capcom to setup the directive for all the files it needs
            MissionControl.Capcom.MainClass.SetupCatalogBuilderDirective(pathToBuiltProject, catalogDirectives[0],
                TimeSpan.FromSeconds(missionControlSettings.QuitTimeout), filesFilter);
            catalogDirectives[0].Launchable.LandingTime = TimeSpan.FromSeconds(missionControlSettings.QuitTimeout);
            Debug.Assert(!catalogDirectives[0].ExcludedFiles.Any());

            // All the other files will be for the cluster node
            catalogDirectives[1].Launchable = new()
            {
                Name = projectName,
                Type = Launchable.ClusterNodeType,
                Data = ComputeLaunchableData(),
                PreLaunchPath = catalogDirectives[0].Launchable.LaunchPath.Replace(".Capcom.", ".PreLaunch."),
                LaunchPath = Path.GetRelativePath(pathToBuiltProject, pathToBuiltProjectExe),
                LandingTime = TimeSpan.FromSeconds(missionControlSettings.QuitTimeout)
            };
            FillMainLaunchableParameters(catalogDirectives[1].Launchable);
            AddLaunchParametersForProjectionPolicy(catalogDirectives[1].Launchable);
            catalogDirectives[1].Files = new[] {"*"};

            // Check if there is a catalog file from a previous build, if yes remove it (so that it does not get
            // included in the catalog!).  This is a big problem as it will not have the right checksum and so will
            // cause an import failure in MissionControl.
            var catalogPath = Path.Combine(pathToBuiltProject, k_CatalogFilename);
            if (File.Exists(catalogPath))
            {
                try
                {
                    File.Delete(catalogPath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to delete previous {k_CatalogFilename}: {e}");
                    throw;
                }
            }

            // Create the catalog
            var catalog = CatalogBuilder.Build(pathToBuiltProject, catalogDirectives, filesFilter);
            File.WriteAllText(catalogPath,JsonConvert.SerializeObject(catalog, Json.SerializerOptions));
        }

        static JToken ComputeLaunchableData()
        {
            JObject ret = new();
            ret[LaunchableDataConstants.CompanyNameProperty] = Application.companyName;
            ret[LaunchableDataConstants.ProgramNameProperty] = Application.productName;
            return ret;
        }

        /// <summary>
        /// Add the <see cref="LaunchParameter"/>s to the main <see cref="Launchable"/>.
        /// </summary>
        /// <param name="toFill">The <see cref="Launchable"/> that represent the ClusterDisplay process to be executed
        /// on each cluster nodes.</param>
        static void FillMainLaunchableParameters(Launchable toFill)
        {
            toFill.LaunchPadParameters.Add(new()
            {
                Id = LaunchParameterConstants.NodeIdParameterId,
                Type = LaunchParameterType.Integer, DefaultValue = -1,
                Name = "Node identifier",
                Description = "Unique identifier among the nodes of the cluster, keep default value for an automatic assignment based on the order of the launchpad in the launch configuration (-1 for automatic assignment).",
                Constraint = new RangeConstraint() { Min = -1, Max = 255 },
                ToBeRevisedByCapcom = true
            });
            toFill.LaunchPadParameters.Add(new()
            {
                Id = LaunchParameterConstants.NodeRoleParameterId,
                Type = LaunchParameterType.String, DefaultValue = LaunchParameterConstants.NodeRoleUnassigned,
                Name = "Node role",
                Description = "Role of the node in the cluster.  One node is to be configured as the Emitter while the other ones should be configured as a Repeater.",
                Constraint = new ListConstraint() { Choices = new[] {
                    LaunchParameterConstants.NodeRoleUnassigned, LaunchParameterConstants.NodeRoleEmitter,
                    LaunchParameterConstants.NodeRoleRepeater, LaunchParameterConstants.NodeRoleBackup
                } },
                ToBeRevisedByCapcom = true
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.RepeaterCountParameterId,
                Type = LaunchParameterType.Integer, DefaultValue = 0,
                Constraint = new RangeConstraint() { Min = 0, Max = 254 },
                ToBeRevisedByCapcom = true, Hidden = true
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.BackupNodeCountParameterId,
                Type = LaunchParameterType.Integer, DefaultValue = 0,
                Constraint = new RangeConstraint() { Min = 0, Max = 254 },
                Name = "Backup node count",
                Description = $"How many nodes with a Node role of \"{LaunchParameterConstants.NodeRoleUnassigned}\" " +
                    $"will have the role of \"{LaunchParameterConstants.NodeRoleBackup}\"?",
                ToBeRevisedByCapcom = true
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.MulticastAddressParameterId,
                Type = LaunchParameterType.String,
                DefaultValue = ClusterDisplaySettings.Current.ClusterParams.MulticastAddress,
                Name = "Multicast address",
                Description = "IPv4 multicast UDP address used for inter-node communication (state propagation).",
                Constraint = new RegularExpressionConstraint() {
                    RegularExpression = "^((25[0-5]|(2[0-4]|1\\d|[1-9]|)\\d)\\.?\\b){4}$",
                    ErrorMessage = "Must be an ipv4 address: ###.###.###.### (each number from 0 to 255)."
                }
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.MulticastPortParameterId,
                Type = LaunchParameterType.Integer,
                DefaultValue = ClusterDisplaySettings.Current.ClusterParams.Port,
                Name = "Multicast port",
                Description = "Multicast UDP port used for inter-node communication (state propagation).",
                Constraint = new RangeConstraint() { Min = 1, Max = ushort.MaxValue }
            });
            toFill.LaunchPadParameters.Add(new()
            {
                Id = LaunchParameterConstants.MulticastAdapterNameParameterId,
                Type = LaunchParameterType.String, DefaultValue = "",
                Name = "Multicast adapter name",
                Description = "Network adapter name (or ip) identifying the network adapter to use for inter-node " +
                    "communication (state propagation).  Default adapter defined in the launchpad's configuration " +
                    "will be used if not specified."
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.TargetFrameRateParameterId,
                Type = LaunchParameterType.Integer,
                DefaultValue = Math.Max(ClusterDisplaySettings.Current.ClusterParams.TargetFps, 0),
                Name = "Target frame rate",
                Description = "Target frame per seconds at which the cluster should run.  Set to 0 for unlimited.",
                Constraint = new RangeConstraint() { Min = 0, Max = 240 }
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.DelayRepeatersParameterId,
                Type = LaunchParameterType.Boolean,
                DefaultValue = ClusterDisplaySettings.Current.ClusterParams.DelayRepeaters,
                Name = "Delay repeaters",
                Description = "Delay rendering of repeaters by one frame, to be used when repeaters depends on state " +
                    "computed during the frame processing of the emitter.  Increases latency of the system by one " +
                    "frame."
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.HeadlessEmitterParameterId,
                Type = LaunchParameterType.Boolean,
                DefaultValue = ClusterDisplaySettings.Current.ClusterParams.HeadlessEmitter,
                Name = "Headless emitter",
                Description = "Disables rendering of the emitter (headless)."
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.ReplaceHeadlessEmitterParameterId,
                Type = LaunchParameterType.Boolean,
                DefaultValue = ClusterDisplaySettings.Current.ClusterParams.ReplaceHeadlessEmitter,
                Name = "Replace headless emitter",
                Description = "Will shift NodeId used for rendering of repeater nodes (RenderNodeId = NodeId - 1) " +
                    "when used with a headless emitter."
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.HandshakeTimeoutParameterId,
                Type = LaunchParameterType.Float,
                DefaultValue = ClusterDisplaySettings.Current.ClusterParams.HandshakeTimeout.TotalSeconds,
                Name = "Handshake timeout (seconds)",
                Description = "Timeout for a starting node to perform handshake with the other nodes during cluster " +
                    "startup.",
                Constraint = new RangeConstraint() {MinExclusive = true, Min = 0.0f }
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.CommunicationTimeoutParameterId,
                Type = LaunchParameterType.Float,
                DefaultValue = ClusterDisplaySettings.Current.ClusterParams.CommunicationTimeout.TotalSeconds,
                Name = "Communication timeout (seconds)",
                Description = "Timeout for communication once the cluster is started.",
                Constraint = new RangeConstraint() {MinExclusive = true, Min = 0.0f }
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.EnableHardwareSyncParameterId,
                Type = LaunchParameterType.Boolean,
                DefaultValue = ClusterDisplaySettings.Current.ClusterParams.Fence == FrameSyncFence.Hardware,
                Name = "Enable hardware synchronization",
                Description = "Does the cluster tries to use hardware synchronization?"
            });
            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.CapsuleBasePortParameterId,
                Type = LaunchParameterType.Integer, DefaultValue = LaunchParameterConstants.DefaultCapsuleBasePort,
                Name = "Capsule port",
                Description = "Port opened by the process running alongside the cluster display executable listening " +
                    "for instructions while running.  It will be incremented by 1 for every LaunchPad after the " +
                    "first in the same LaunchComplex.",
                Constraint = new RangeConstraint() { Min = 1, Max = ushort.MaxValue },
                ToBeRevisedByCapcom = true
            });

            toFill.GlobalParameters.Add(new()
            {
                Id = LaunchParameterConstants.DeleteRegistryKeyParameterId,
                Type = LaunchParameterType.Boolean, DefaultValue = LaunchParameterConstants.DefaultDeleteRegistryKey,
                Name = "Delete registry key",
                Description = "Does the setup of the cluster node deletes the registry key produced by a previous " +
                    "execution?  Especially useful to clear display settings of the previously running version of the" +
                    "application."
            });
        }

        /// <summary>
        /// Add the <see cref="LaunchParameter"/>s used by the projection policy.
        /// </summary>
        /// <param name="toFill">The <see cref="Launchable"/> that represent the ClusterDisplay process to be executed
        /// on each cluster nodes.</param>
        static void AddLaunchParametersForProjectionPolicy(Launchable toFill)
        {
            var missionControlSettings = MissionControlSettings.Current;
            if (missionControlSettings.PolicyParameters == null)
            {
                return;
            }

            toFill.GlobalParameters.AddRange(missionControlSettings.PolicyParameters.GlobalParameters);
            toFill.LaunchComplexParameters.AddRange(missionControlSettings.PolicyParameters.LaunchComplexParameters);
            toFill.LaunchPadParameters.AddRange(missionControlSettings.PolicyParameters.LaunchPadParameters);
        }

        const string k_CatalogFilename = "LaunchCatalog.json";
    }
}
