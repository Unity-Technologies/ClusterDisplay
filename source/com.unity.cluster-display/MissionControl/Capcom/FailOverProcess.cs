using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.ClusterDisplay.MissionControl.Capsule;
using Unity.ClusterDisplay.MissionControl.MissionControl;
using State = Unity.ClusterDisplay.MissionControl.LaunchPad.State;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// <see cref="IApplicationProcess"/> responsible for managing node fail over.
    /// </summary>
    /// <remarks>Create a Command "Fail" <see cref="MissionControl.MissionParameter"/> for each launchpad that can be
    /// set to true to indicate the node has failed and that it is to be removed from the cluster.  This process will
    /// send the appropriate messages to the capsules adapter to theses changes in the cluster topology.</remarks>
    public class FailOverProcess: IApplicationProcess
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="launchableData">Launchable data giving additional static parameters to capcom.</param>
        /// <param name="processNeedsToBeCalled">Delegate to call when the process method needs to be called (can be
        /// called from any thread).</param>
        public FailOverProcess(LaunchableData launchableData, Action processNeedsToBeCalled)
        {
            m_LaunchableData = launchableData;
            m_ProcessNeedsToBeCalled = processNeedsToBeCalled;
        }

        /// <inheritdoc/>
        public void Process(MissionControlMirror missionControlMirror)
        {
            StopExpiredLaunchpads(missionControlMirror).Wait();

            if (m_LaunchPadStatusLastVersion >= missionControlMirror.LaunchPadsStatus.VersionNumber &&
                m_ParametersDesiredValuesLastVersion >= missionControlMirror.ParametersDesiredValues.VersionNumber &&
                m_ParametersEffectiveValuesLastVersion >= missionControlMirror.ParametersEffectiveValues.VersionNumber &&
                missionControlMirror.LaunchPadsInformationVersion == m_LastLaunchPadsInformationVersion)
            {
                return;
            }
            m_LaunchPadStatusLastVersion = missionControlMirror.LaunchPadsStatus.VersionNumber;
            m_ParametersDesiredValuesLastVersion = missionControlMirror.ParametersDesiredValues.VersionNumber;
            m_ParametersEffectiveValuesLastVersion = missionControlMirror.ParametersEffectiveValues.VersionNumber;

            if (missionControlMirror.LaunchPadsInformationVersion != m_LastLaunchPadsInformationVersion)
            {
                m_LastLaunchPadsInformationVersion = missionControlMirror.LaunchPadsInformationVersion;
                m_AssignedRenderNodeIds.Clear();
                m_PendingAssignments.Clear();
                m_BackupsKnownAsFailed.Clear();
                m_ManuallySignaledAsFailed.Clear();
                m_StopDeadlines.Clear();
            }

            UpdateMissionParameters(missionControlMirror).Wait();
            ProcessFailParameters(missionControlMirror).Wait();
            UpdateNodeAssignments(missionControlMirror).Wait();
        }

        /// <summary>
        /// Report how many assignments are pending (sent and the node has not yet reported it is ok with it).
        /// </summary>
        /// <remarks>Mostly useful for testing.</remarks>
        public int PendingAssignments => m_PendingAssignments.Count;

        /// <summary>
        /// Check for launchpads that were supposed to be stopped and stop them.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        async Task StopExpiredLaunchpads(MissionControlMirror missionControlMirror)
        {
            foreach (var pair in m_StopDeadlines.ToArray())
            {
                if (pair.Value.Elapsed > m_LaunchableData.FailOverProcessTimeout)
                {
                    // Should be stopped...
                    var launchPadInformation = missionControlMirror.LaunchPadsInformation
                        .FirstOrDefault(lpi => lpi.Definition.Identifier == pair.Key);
                    if (launchPadInformation != null && launchPadInformation.Status.IsDefined &&
                        launchPadInformation.Status.State is not (State.Idle or State.Over))
                    {
                        // Looks like it is still running...  Ask the LaunchPad to stop it.
                        try
                        {
                            using HttpClient httpClient = new();
                            httpClient.BaseAddress = launchPadInformation.Definition.Endpoint;
                            var ret = await httpClient.PostAsJsonAsync("api/v1/commands",
                                new LaunchPad.AbortCommand() {AbortToOver = true});
                            ret.EnsureSuccessStatusCode();
                        }
                        catch (Exception)
                        {
                            // TODO: Log
                        }

                        // Check again in a few seconds that it is really stopped
                        m_StopDeadlines[pair.Key] = Stopwatch.StartNew();
                        _ = Task.Delay(m_LaunchableData.FailOverProcessTimeout)
                            .ContinueWith(_ => m_ProcessNeedsToBeCalled());
                    }
                    else
                    {
                        // It is finally stopped, remove it from our watch list.
                        m_StopDeadlines.Remove(pair.Key);
                    }
                }
            }
        }

        /// <summary>
        /// Update the list of <see cref="MissionParameter"/> based on the status of <see cref="LaunchPad"/>.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        async Task UpdateMissionParameters(MissionControlMirror missionControlMirror)
        {
            // Get the list of launchpads for which we need to have a failed MissionParameter.
            HashSet<Guid> aliveLaunchpads = new();
            int runningBackupNodesCount = missionControlMirror.LaunchPadsInformation
                .Count(lpi => lpi.Status.IsDefined && lpi.Status.State == State.Launched &&
                       lpi.CurrentRole == NodeRole.Backup);
            if (runningBackupNodesCount > 0)
            {
                foreach (var id in missionControlMirror.LaunchPadsInformation
                             .Where(lpi => lpi.Status.IsDefined && lpi.Status.State == State.Launched)
                             .Select(lpi => lpi.Identifier))
                {
                    aliveLaunchpads.Add(id);
                }
            }

            // Create missing parameters
            HashSet<string> parameterValuesToClean = new();
            foreach (var launchpadId in aliveLaunchpads)
            {
                if (m_CreatedMissionParameters.ContainsKey(launchpadId))
                {
                    continue;
                }

                var launchPadInformation = missionControlMirror.LaunchPadsInformation
                    .FirstOrDefault(i => i.Identifier == launchpadId);
                if (launchPadInformation == null)
                {
                    continue;
                }

                try
                {
                    MissionParameter failedParameter = new(Guid.NewGuid()) {
                        ValueIdentifier = launchPadInformation.FailMissionParameterValueIdentifier,
                        Name = "Set Failover",
                        Description = "Failing a launchpad causes the fail over mechanism to terminate that node and " +
                            "assign a backup node to perform its work.",
                        Type = MissionParameterType.Command,
                        Constraint = new ConfirmationConstraint() {
                            ConfirmationType = ConfirmationType.Danger,
                            Title = "Confirm Failover",
                            FullText = $"Once flagged as failed, you cannot change the status of the device back " +
                                $"unless you relaunch the mission.  Do you really want to mark " +
                                $"\"{launchPadInformation.Definition.Name}\" as failed?"
                        },
                        Group = launchpadId.ToString()
                    };
                    if (launchPadInformation.StartRole == NodeRole.Emitter)
                    {
                        ((ConfirmationConstraint)failedParameter.Constraint).FullText += " >>> NOT YET IMPLEMENTED FOR EMITTER <<<";
                    }
                    var putRet = await missionControlMirror.MissionControlHttpClient.PutAsJsonAsync(
                        "api/v1/currentMission/parameters", failedParameter).ConfigureAwait(false);
                    putRet.EnsureSuccessStatusCode();
                    m_CreatedMissionParameters[launchpadId] = new() {
                        ValueIdentifier = launchPadInformation.FailMissionParameterValueIdentifier,
                        Identifier = failedParameter.Id
                    };
                    parameterValuesToClean.Add(launchPadInformation.FailMissionParameterValueIdentifier);
                }
                catch (Exception)
                {
                    // TODO: Log
                }
            }

            // Remove old ones
            foreach (var missionParameterPair in m_CreatedMissionParameters.ToList())
            {
                if (aliveLaunchpads.Contains(missionParameterPair.Key))
                {
                    continue;
                }

                try
                {
                    var deleteRet = await missionControlMirror.MissionControlHttpClient.DeleteAsync(
                        $"api/v1/currentMission/parameters/{missionParameterPair.Value.Identifier}").ConfigureAwait(false);
                    if (deleteRet.StatusCode != HttpStatusCode.NotFound) // We still want to continue as a success if
                    {                                                    // it is not found (don't care why)
                        deleteRet.EnsureSuccessStatusCode();
                    }
                    m_CreatedMissionParameters.Remove(missionParameterPair.Key);
                    parameterValuesToClean.Add(missionParameterPair.Value.ValueIdentifier);
                }
                catch (Exception)
                {
                    // TODO: Log
                }
            }

            // Clean old mission parameters values
            if (parameterValuesToClean.Any())
            {
                await DeleteOldMissionParameterValues(missionControlMirror, parameterValuesToClean).ConfigureAwait(false);
            }

            // Create missing effective parameter value for each failed parameters that does not have one yet.
            // Remark: Has to be done after DeleteOldMissionParameterValues so that any old effective
            //         MissionParameterValue have been deleted.
            foreach (var missionParameterInformationPair in m_CreatedMissionParameters)
            {
                if (missionParameterInformationPair.Value.EffectiveValueIdentifier != Guid.Empty)
                {
                    continue;
                }

                try
                {
                    // Create the matching effective MissionParameterValue
                    MissionParameterValue failedParameterValue = new(Guid.NewGuid()) {
                        ValueIdentifier = missionParameterInformationPair.Value.ValueIdentifier
                    };
                    var putRet = await missionControlMirror.MissionControlHttpClient.PutAsJsonAsync(
                        "api/v1/currentMission/parametersEffectiveValues", failedParameterValue).ConfigureAwait(false);
                    putRet.EnsureSuccessStatusCode();
                    missionParameterInformationPair.Value.EffectiveValueIdentifier = failedParameterValue.Id;
                }
                catch (Exception)
                {
                    // Shouldn't be the case, but let's continue and try again next time...
                    // TODO: Log
                }
            }
        }

        static Task DeleteOldMissionParameterValues(MissionControlMirror missionControlMirror, HashSet<string> valueIdentifiers)
        {
            List<Task> cleanupTasks = new();

            async Task DeleteParameterValue(string url)
            {
                try
                {
                    await missionControlMirror.MissionControlHttpClient.DeleteAsync(url).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Cleanup is done on a best effort basis, so just ignore errors...
                }
            }

            // Delete old desired values
            foreach (var value in missionControlMirror.ParametersDesiredValues.Values)
            {
                if (valueIdentifiers.Contains(value.ValueIdentifier))
                {
                    cleanupTasks.Add(Task.Run(
                        () => DeleteParameterValue($"api/v1/currentMission/parametersDesiredValues/{value.Id}")));
                }
            }

            // Delete old effective values
            foreach (var value in missionControlMirror.ParametersEffectiveValues.Values)
            {
                if (valueIdentifiers.Contains(value.ValueIdentifier))
                {
                    cleanupTasks.Add(Task.Run(
                        () => DeleteParameterValue($"api/v1/currentMission/parametersEffectiveValues/{value.Id}")));
                }
            }

            // Wait for all deletes to be done
            return Task.WhenAll(cleanupTasks);
        }

        /// <summary>
        /// Look for failed mission parameter desired values that would have been set to true and process them.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        async Task ProcessFailParameters(MissionControlMirror missionControlMirror)
        {
            foreach (var desiredValue in missionControlMirror.ParametersDesiredValues.Values)
            {
                var valueIdentifier = desiredValue.ValueIdentifier;
                if (!valueIdentifier.EndsWith(".Fail"))
                {
                    continue;
                }

                Guid launchpadId;
                try
                {
                    const int dotFailedLength = 5;
                    launchpadId = Guid.Parse(
                        valueIdentifier.Substring(0, desiredValue.ValueIdentifier.Length - dotFailedLength));
                }
                catch (Exception)
                {
                    // Then this is not really the failed parameter we are looking for, must be something else.
                    continue;
                }

                var launchpadInformation = missionControlMirror.LaunchPadsInformation.FirstOrDefault(
                    lpi => lpi.Identifier == launchpadId);
                if (launchpadInformation == null)
                {
                    // Someone else naming its mission parameters the same way we are?  Just ignore it.
                    continue;
                }

                Guid failCommandId;
                try
                {
                    failCommandId = desiredValue.AsGuid();
                }
                catch (Exception)
                {
                    // TODO: Log
                    continue;
                }

                if (m_ManuallySignaledAsFailed.Add(launchpadId))
                {
                    if (m_CreatedMissionParameters.TryGetValue(launchpadId, out var missionParameterInformation) &&
                        missionParameterInformation.EffectiveValueIdentifier != Guid.Empty)
                    {
                        try
                        {
                            // Update the effective value to indicate we understood someone requested the launchpad
                            // to fail.
                            MissionParameterValue toPut = new(missionParameterInformation.EffectiveValueIdentifier)
                            {
                                ValueIdentifier = desiredValue.ValueIdentifier,
                                Value = JToken.Parse($"\"{failCommandId}\"")
                            };
                            var ret = await missionControlMirror.MissionControlHttpClient.PutAsJsonAsync(
                                "api/v1/currentMission/parametersEffectiveValues", toPut).ConfigureAwait(false);
                            ret.EnsureSuccessStatusCode();
                        }
                        catch (Exception)
                        {
                            // Not ideal, but can still continue even if it failed, not critical.
                            // TODO: Log
                        }
                    }
                    else
                    {
                        // else there is something really weird going on...  Let's still proceed but effective parameter
                        // value will not be up to date
                        // TODO: Log
                    }
                }
            }
        }

        /// <summary>
        /// Analyse node status and update nodes assignments accordingly.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        Task UpdateNodeAssignments(MissionControlMirror missionControlMirror)
        {
            if (missionControlMirror.CapcomUplink.ProceedWithLanding)
            {
                // A landing has been requested, we do not want to reconfigure the cluster while landing...
                return Task.CompletedTask;
            }

            // Clean pending assignments that have been processed
            foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                if (m_PendingAssignments.ContainsKey(launchPadInformation.Identifier) &&
                    launchPadInformation.CurrentRole != NodeRole.Backup)
                {
                    m_PendingAssignments.Remove(launchPadInformation.Identifier);
                }
            }

            // Get the RenderNodeIds that are currently active
            HashSet<byte> activeRenderNodeIds = new();
            foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                // Is it a backup launchpad that has been reassigned and hasn't yet processed its new role?
                if (launchPadInformation.CurrentRole is NodeRole.Backup &&
                    m_PendingAssignments.TryGetValue(launchPadInformation.Identifier, out var pendingAssignment) &&
                    pendingAssignment.Role != NodeRole.Unassigned)
                {
                    if (IsLaunchPadFailed(launchPadInformation))
                    {
                        // Yes, but it failed...  Let's forget about it...
                        pendingAssignment.Role = NodeRole.Unassigned;
                    }
                    else
                    {
                        // It has not yet acknowledged its new role, but it hasn't denied it it either, so let's
                        // continue to hope and do like if it will eventually accept it.
                        activeRenderNodeIds.Add(pendingAssignment.RenderNodeId);
                    }
                    continue;
                }

                // A failed launchpad does not have any active RenderNodeId
                if (IsLaunchPadFailed(launchPadInformation))
                {
                    continue;
                }

                // A current role of unassigned indicate we are still starting up, so state is not clear yet, skip it.
                // A backup role does not have any meaningful RenderNodeId for us, so skip them.
                if (launchPadInformation.CurrentRole is NodeRole.Unassigned or NodeRole.Backup)
                {
                    continue;
                }

                int renderNodeId = launchPadInformation.RenderNodeId;
                if (renderNodeId != -1)
                {
                    activeRenderNodeIds.Add((byte)renderNodeId);
                }
            }

            // Detect any RenderNodeIds that are not active anymore
            List<byte> unassignedRenderNodeIds = new();
            foreach (var renderNodeId in m_AssignedRenderNodeIds.ToList())
            {
                if (!activeRenderNodeIds.Contains(renderNodeId))
                {
                    unassignedRenderNodeIds.Add(renderNodeId);
                }
            }
            m_AssignedRenderNodeIds = activeRenderNodeIds;

            // Dispatch render node ids if any is unassigned
            if (unassignedRenderNodeIds.Any())
            {
                return DispatchRenderNodeIds(missionControlMirror, unassignedRenderNodeIds);
            }

            // So far so good, last thing to check is that a backup node has failed, in which case we still need to
            // update the cluster topology to remove it.
            bool backupFailed = false;
            foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                if (launchPadInformation.CurrentRole == NodeRole.Backup && IsLaunchPadFailed(launchPadInformation) &&
                    !m_BackupsKnownAsFailed.Contains(launchPadInformation.Identifier))
                {
                    backupFailed = true;
                    m_BackupsKnownAsFailed.Add(launchPadInformation.Identifier);
                }
            }

            if (backupFailed)
            {
                return DispatchNewClusterConfiguration(missionControlMirror);
            }

            // Detect new launchpads that are failed but the process is still running.  We will assign them a deadline
            // by which they have to be done or else we will force stop them.
            foreach (var pendingAssignment in m_PendingAssignments.Where(a => a.Value.Role == NodeRole.Unassigned))
            {
                if (!m_StopDeadlines.ContainsKey(pendingAssignment.Key))
                {
                    m_StopDeadlines.Add(pendingAssignment.Key, Stopwatch.StartNew());
                    Task.Delay(m_LaunchableData.FailOverProcessTimeout).ContinueWith(_ => m_ProcessNeedsToBeCalled());
                }
            }
            foreach (var manuallySignaled in m_ManuallySignaledAsFailed)
            {
                if (!m_StopDeadlines.ContainsKey(manuallySignaled))
                {
                    m_StopDeadlines.Add(manuallySignaled, Stopwatch.StartNew());
                    Task.Delay(m_LaunchableData.FailOverProcessTimeout).ContinueWith(_ => m_ProcessNeedsToBeCalled());
                }
            }

            // Done
            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns if the provided launchpad should be considered as failed.
        /// </summary>
        /// <param name="launchPadInformation">Information about the launchpadé</param>
        /// <remarks> An undefined status could only indicate the launchpad exploded (crashed) for some reason but the
        /// capsule might still be running fine, so don't panic if the launchpad status is undefined and continue with
        /// the last information we had.</remarks>
        bool IsLaunchPadFailed(LaunchPadInformation launchPadInformation)
        {
            return (launchPadInformation.Status.IsDefined && launchPadInformation.Status.State != State.Launched) ||
                m_ManuallySignaledAsFailed.Contains(launchPadInformation.Identifier);
        }

        /// <summary>
        /// Dispatch RenderNodeIds that were not assigned to anybody to backup nodes.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        /// <param name="renderNodeIds">List of RenderNodeIds to dispatch.</param>
        Task DispatchRenderNodeIds(MissionControlMirror missionControlMirror, IEnumerable<byte> renderNodeIds)
        {
            // Dispatch currently active nodes based on their roles.
            Dictionary<NodeRole, List<LaunchPadInformation>> launchpadByRole = new();
            foreach (var launchPadInfo in missionControlMirror.LaunchPadsInformation)
            {
                if (IsLaunchPadFailed(launchPadInfo))
                {
                    continue;
                }
                if (m_PendingAssignments.TryGetValue(launchPadInfo.Definition.Identifier, out var pendingAssignment))
                {
                    launchpadByRole.GetOrAddNew(pendingAssignment.Role).Add(launchPadInfo);
                    continue;
                }

                launchpadByRole.GetOrAddNew(launchPadInfo.CurrentRole).Add(launchPadInfo);
            }

            // Do we currently have an emitter?
            launchpadByRole.TryGetValue(NodeRole.Emitter, out var emitterList);
            bool hasEmitterNode = emitterList?.FirstOrDefault() != null ||
                m_PendingAssignments.Values.Any(a => a.Role == NodeRole.Emitter);

            // Assign RenderNodeIds to dispatch to backup nodes
            launchpadByRole.TryGetValue(NodeRole.Backup, out var backupNodes);
            backupNodes ??= new();
            backupNodes = backupNodes.OrderBy(lpi => lpi.NodeId).ToList();
            bool changedAssignments = false;
            foreach (var renderNodeId in renderNodeIds)
            {
                if (!backupNodes.Any())
                {
                    // Not enough backup for all the render node ids to dispatch... And new nodes cannot "appear later
                    // on" once we are started.  So there is nothing we can do, let's simply stop there...
                    // TODO: Log
                    break;
                }

                var newNode = backupNodes.First();
                backupNodes.RemoveAt(0);
                NodeAssignment newAssignment = new ()
                {
                    Role = hasEmitterNode ? NodeRole.Repeater : NodeRole.Emitter,
                    RenderNodeId = renderNodeId
                };
                if (newAssignment.Role == NodeRole.Emitter)
                {
                    hasEmitterNode = true;
                }
                m_PendingAssignments[newNode.Definition.Identifier] = newAssignment;
                changedAssignments = true;
            }

            return changedAssignments ? DispatchNewClusterConfiguration(missionControlMirror) : Task.CompletedTask;
        }

        /// <summary>
        /// Compute the updated cluster configuration and send it to every capsule (so that they can all have a good
        /// picture of reorg that occured).
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        async Task DispatchNewClusterConfiguration(MissionControlMirror missionControlMirror)
        {
            // Prepare entries for all active nodes in the cluster
            List<ChangeClusterTopologyEntry> topologyEntries = new();
            foreach (var launchPadInfo in missionControlMirror.LaunchPadsInformation)
            {
                if (IsLaunchPadFailed(launchPadInfo))
                {
                    continue;
                }

                ChangeClusterTopologyEntry entry = new() {
                    NodeId = (byte)launchPadInfo.NodeId,
                    NodeRole = launchPadInfo.CurrentRole,
                    RenderNodeId = (byte)launchPadInfo.RenderNodeId
                };

                if (m_PendingAssignments.TryGetValue(launchPadInfo.Definition.Identifier, out var pendingAssignment))
                {
                    entry.NodeRole = pendingAssignment.Role;
                    entry.RenderNodeId = pendingAssignment.RenderNodeId;
                }

                topologyEntries.Add(entry);
            }

            if (topologyEntries.Count(e => e.NodeRole == NodeRole.Emitter) != 1)
            {
                // It is essential to have one, and only one, emitter.  Something must be going really wrong, this
                // configuration is not good, don't dispatch it.
                // TODO: Log
                return;
            }

            // Prepare the message to send to every capsules
            MemoryStream toSendToCapsules = new();
            toSendToCapsules.WriteStruct(MessagesId.ChangeClusterTopology);
            ChangeClusterTopologyMessageHeader header = new() {EntriesCount = (byte)topologyEntries.Count};
            toSendToCapsules.WriteStruct(header);
            toSendToCapsules.Write(MemoryMarshal.AsBytes(topologyEntries.ToArray().AsSpan()));
            toSendToCapsules.Flush();
            var messageToSend = toSendToCapsules.ToArray();

            // Send it to capsules (and wait for empty responses)
            List<Task> sendTasks = new();
            foreach (var launchPadInfo in missionControlMirror.LaunchPadsInformation)
            {
                sendTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await launchPadInfo.StreamToCapsule.WriteAsync(messageToSend).ConfigureAwait(false);

                        _ = await launchPadInfo.StreamToCapsule.ReadStructAsync<ChangeClusterTopologyResponse>(
                            new byte[Marshal.SizeOf<ChangeClusterTopologyResponse>()]).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // This will happen when trying to send to a "crashed capsule", so this is "normal" and we
                        // simply silently ignore it.
                    }
                }));
            }
            await Task.WhenAll(sendTasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Information about a created MissionParameter used to fail a node of the cluster.
        /// </summary>
        class MissionParameterInformation
        {
            public string ValueIdentifier { get; set; }
            public Guid Identifier { get; set; }
            public Guid EffectiveValueIdentifier { get; set; }
        }

        /// <summary>
        /// Information about the mission that is assigned to a node of the cluster.
        /// </summary>
        class NodeAssignment
        {
            public NodeRole Role { get; set; }
            public byte RenderNodeId { get; set; }
        }

        readonly LaunchableData m_LaunchableData;
        readonly Action m_ProcessNeedsToBeCalled;

        /// <summary>
        /// Last version of <see cref="MissionControlMirror.LaunchPadsStatus"/> for which the <see cref="Process"/>
        /// method did something.
        /// </summary>
        ulong m_LaunchPadStatusLastVersion;
        /// <summary>
        /// Last version of <see cref="MissionControlMirror.ParametersDesiredValues"/>
        /// </summary>
        ulong m_ParametersDesiredValuesLastVersion;
        /// <summary>
        /// Last version of <see cref="MissionControlMirror.ParametersEffectiveValues"/>
        /// </summary>
        ulong m_ParametersEffectiveValuesLastVersion;
        /// <summary>
        /// Last value of <see cref="MissionControlMirror.LaunchPadsInformationVersion"/> we processed for.  This allow
        /// us easily clear things between launches.
        /// </summary>
        ulong m_LastLaunchPadsInformationVersion;

        /// <summary>
        /// Maps a <see cref="LaunchPad.Identifier"/> to the <see cref="MissionParameter.Id"/> of the Failed
        /// <see cref="MissionParameter"/>.
        /// </summary>
        Dictionary<Guid, MissionParameterInformation> m_CreatedMissionParameters = new();

        /// <summary>
        /// RenderNodeId of nodes that are currently doing their job.
        /// </summary>
        HashSet<byte> m_AssignedRenderNodeIds = new();
        /// <summary>
        /// NodeAssignment that have been assigned to nodes and waiting until we receive feedback that they accepted the
        /// job.
        /// </summary>
        Dictionary<Guid, NodeAssignment> m_PendingAssignments = new();
        /// <summary>
        /// Identifier of the launchpads that were running as a backup, that failed and that have already been factored
        /// in the topology changes.
        /// </summary>
        HashSet<Guid> m_BackupsKnownAsFailed = new();
        /// <summary>
        /// Launchpads that have been manually marked as failed.
        /// </summary>
        HashSet<Guid> m_ManuallySignaledAsFailed = new();
        /// <summary>
        /// Time that a node has to land (or crash).  Launchpad will be stopped when <see cref="Stopwatch.Elapsed"/>
        /// exceeds <see cref="LaunchableData.FailOverProcessTimeout"/>.
        /// </summary>
        Dictionary<Guid, Stopwatch> m_StopDeadlines = new();
    }
}
