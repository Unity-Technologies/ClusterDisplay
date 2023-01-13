using System;
using System.Runtime.CompilerServices;

namespace Unity.ClusterDisplay.MissionControl
{
    [ClusterParamProcessor]
    static class MissionControlClusterParamExtensions
    {
        /// <summary>
        /// Override the given parameters with values from MissionControl launch context (if available).
        /// </summary>
        /// <param name="clusterParams">Initial parameters to modify.</param>
        /// <returns>The new set of cluster parameters.</returns>
        [ClusterParamProcessorMethod, UnityEngine.Scripting.Preserve]
        public static ClusterParams Apply(ClusterParams clusterParams)
        {
            var launchConfig = MissionControlLaunchConfiguration.Instance;
            if (launchConfig == null)
            {
                return clusterParams;
            }

            var nodeRole = launchConfig.GetNodeRole() ?? NodeRole.Unassigned;
            var repeaterNodeCount = launchConfig.GetRepeaterCount() ?? 0;
            int backupNodeCount = launchConfig.GetBackupCount() ?? 0;
            if (nodeRole != NodeRole.Unassigned && (repeaterNodeCount + backupNodeCount) > 0)
            {
                clusterParams.ClusterLogicSpecified = true;
            }
            else
            {
                return clusterParams;
            }

            clusterParams.Role = nodeRole;
            ApplyArgument(ref clusterParams.NodeID, launchConfig.GetNodeId());
            clusterParams.RepeaterCount = repeaterNodeCount;
            clusterParams.BackupCount = backupNodeCount;
            ApplyArgument(ref clusterParams.Port, launchConfig.GetMulticastPort());
            clusterParams.MulticastAddress ??= launchConfig.GetMulticastAddress();
            clusterParams.AdapterName ??= launchConfig.GetMulticastAdapterName();
            ApplyArgument(ref clusterParams.TargetFps, launchConfig.GetTargetFrameRate());
            ApplyArgument(ref clusterParams.DelayRepeaters, launchConfig.GetDelayRepeaters());
            ApplyArgument(ref clusterParams.HeadlessEmitter, launchConfig.GetHeadlessEmitter());
            ApplyArgument(ref clusterParams.ReplaceHeadlessEmitter, launchConfig.GetReplaceHeadlessEmitter());
            if (launchConfig.GetHandshakeTimeout() is { } handshakeTimeout)
            {
                clusterParams.HandshakeTimeout = handshakeTimeout;
            }
            if (launchConfig.GetCommunicationTimeout() is { } communicationTimeout)
            {
                clusterParams.CommunicationTimeout = communicationTimeout;
            }
            if (launchConfig.GetEnableHardwareSync() is { } enableHardwareSync)
            {
                clusterParams.Fence = enableHardwareSync ? FrameSyncFence.Hardware : FrameSyncFence.Network;
            }

            // We know we are launching from Mission Control, this is a good time to start the capsule that will receive
            // instructions from Capcom.
            _ = (new Capsule.ProcessingLoop()).Start(launchConfig.GetCapsulePort());

            return clusterParams;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ApplyArgument<T>(ref T target, T? arg) where T : struct
        {
            if (arg.HasValue)
            {
                target = arg.Value;
            }
        }
    }
}
