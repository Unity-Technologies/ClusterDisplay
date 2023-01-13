using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// <see cref="IApplicationProcess"/> responsible for reviewing launch parameters (updating the value and marking
    /// them as ready (with an optional review comment)).
    /// </summary>
    public class ReviewLaunchParametersProcess: IApplicationProcess
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="httpClient">HttpClient already configured (<see cref="HttpClient.BaseAddress"/>) to send to
        /// MissionControl.</param>
        public ReviewLaunchParametersProcess(HttpClient httpClient)
        {
            m_HttpClient = httpClient;
        }

        /// <inheritdoc/>
        public void Process(MissionControlMirror missionControlMirror)
        {
            // As anything that might interest us changed?
            if (m_StatusLastVersion >= missionControlMirror.StatusVersionNumber &&
                m_LaunchConfigurationLastVersion >= missionControlMirror.LaunchConfigurationVersionNumber &&
                m_ComplexesLastVersion >= missionControlMirror.Complexes.VersionNumber &&
                m_LaunchParametersForReviewLastVersion >= missionControlMirror.LaunchParametersForReview.VersionNumber)
            {
                return;
            }
            m_StatusLastVersion = missionControlMirror.StatusVersionNumber;
            m_LaunchConfigurationLastVersion = missionControlMirror.LaunchConfigurationVersionNumber;
            m_ComplexesLastVersion = missionControlMirror.Complexes.VersionNumber;
            m_LaunchParametersForReviewLastVersion = missionControlMirror.LaunchParametersForReview.VersionNumber;

            // Changes might happen, but for as long as we are not launching again, keep the LaunchConfiguration we
            // already computed.
            if (m_ComputedOnState.HasValue && missionControlMirror.LaunchPadsInformation.Any())
            {
                if (missionControlMirror.Status.State > m_ComputedOnState ||
                    (missionControlMirror.Status.State == m_ComputedOnState &&
                        missionControlMirror.Status.EnteredStateTime <= m_ComputedOnEnteredStateTime))
                {
                    return;
                }
            }
            m_ComputedOnState = missionControlMirror.Status.State;
            m_ComputedOnEnteredStateTime = missionControlMirror.Status.EnteredStateTime;

            // Get rid of whatever previous LaunchPadInformation we had
            foreach (var oldLaunchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                oldLaunchPadInformation.Dispose();
            }
            missionControlMirror.LaunchPadsInformation.Clear();

            // Only try to compute launchpad parameters if the state is at least preparing
            if (missionControlMirror.Status.State < State.Preparing ||
                !missionControlMirror.LaunchParametersForReview.Any())
            {
                return;
            }

            // Compute them
            ComputeLaunchpadsInformation(missionControlMirror);
            ReviewNodeIds(missionControlMirror);
            ReviewNodeRoles(missionControlMirror);
            ComputeRepeaterCount(missionControlMirror);
            ComputeBackupNodeCount(missionControlMirror);
            ComputeCapsulePorts(missionControlMirror);
        }

        /// <summary>
        /// Assemble all the information we need about launchpads participating to the cluster.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        /// <remarks>Keep order the same as in the <see cref="LaunchConfiguration"/>.</remarks>
        static void ComputeLaunchpadsInformation(MissionControlMirror missionControlMirror)
        {
            if (!missionControlMirror.Assets.TryGetValue(missionControlMirror.LaunchConfiguration.AssetId,
                    out var selectedAsset))
            {
                return;
            }

            foreach (var complexConfiguration in missionControlMirror.LaunchConfiguration.LaunchComplexes)
            {
                LaunchComplex complexDefinition =
                    missionControlMirror.Complexes.Values.First(lc => lc.Id == complexConfiguration.Identifier);
                foreach (var launchPadConfiguration in complexConfiguration.LaunchPads)
                {
                    var launchpadDefinition = complexDefinition.LaunchPads
                        .FirstOrDefault(lp => lp.Identifier == launchPadConfiguration.Identifier);
                    if (launchpadDefinition != null &&
                        launchpadDefinition.SuitableFor.Contains(LaunchCatalog.Launchable.ClusterNodeType))
                    {
                        var launchable = selectedAsset.Launchables.FirstOrDefault(
                            l => l.Name == launchPadConfiguration.LaunchableName);
                        if (launchable is {Type: LaunchCatalog.Launchable.ClusterNodeType})
                        {
                            missionControlMirror.LaunchPadsInformation.Add(new() {
                                Definition = launchpadDefinition,
                                ComplexDefinition = complexDefinition,
                                Configuration = launchPadConfiguration,
                                ComplexConfiguration = complexConfiguration
                            });
                        }
                        // else this launchable is not of a type managed by this capcom, skip it.
                    }
                    // else this is launchpad is not supporting anything managed by this capcom, skip it.
                }
            }

            ++missionControlMirror.LaunchPadsInformationVersion;
        }

        /// <summary>
        /// Review NodeId LaunchParameter for the different launchpads.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        void ReviewNodeIds(MissionControlMirror missionControlMirror)
        {
            // Make a first pass to discover all the manually specified node ids.  Loop based on launchpadsInformation
            // to have a deterministic order (since order in the incremental collection is not).
            Dictionary<byte, LaunchPadInformation> nodeIdToLaunchPad = new();
            foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                var launchPadId = launchPadInformation.Definition.Identifier;
                var nodeIdForReview = missionControlMirror.LaunchParametersForReview.Values
                    .FirstOrDefault(lpfr => lpfr.LaunchPadId == launchPadId &&
                        lpfr.Value.Id == LaunchParameterConstants.NodeIdParameterId);
                if (nodeIdForReview == null)
                {
                    continue;
                }
                try
                {
                    var nodeIdInt = Convert.ToInt32(nodeIdForReview.Value.Value);
                    if (nodeIdInt is >= byte.MinValue and <= byte.MaxValue)
                    {
                        byte nodeId = (byte)nodeIdInt;
                        nodeIdToLaunchPad.Add(nodeId, launchPadInformation);
                    }
                }
                catch (Exception)
                {
                    // Silent for now, will soon be considered when we compute the actual value of each NodeId.
                }
            }

            // Ok, now lets review each node id and update parameters that need updating.
            byte freeNodeIdSearchPosition = 0;
            foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                var launchPadId = launchPadInformation.Definition.Identifier;
                var nodeIdForReview = missionControlMirror.LaunchParametersForReview.Values
                    .FirstOrDefault(lpfr => lpfr.LaunchPadId == launchPadId &&
                        lpfr.Value.Id == LaunchParameterConstants.NodeIdParameterId);
                if (nodeIdForReview == null)
                {
                    // This is not supposed to happen, MissionControl should have created a NodeId
                    // LaunchParameterForReview for every launch pad...  Let's just skip it as we cannot do anything to
                    // fix this problem...
                    // TODO: Log
                    continue;
                }
                LaunchParameterForReview reviewedNodeId = nodeIdForReview.DeepClone();

                // Get the proposed value
                int nodeId;
                try
                {
                    nodeId = Convert.ToInt32(reviewedNodeId.Value.Value);
                }
                catch (Exception)
                {
                    reviewedNodeId.ReviewComments = "Must be an integer.";
                    nodeId = -1;
                }

                // Is the proposed value valid
                if (nodeId != -1 && nodeId is < 0 or > 255)
                {
                    reviewedNodeId.ReviewComments = "Must be in the [0, 255] range.";
                    nodeId = -1;
                }
                if (nodeId != -1)
                {
                    if (nodeIdToLaunchPad.TryGetValue((byte)nodeId, out var launchPadWithNodeId) &&
                        !ReferenceEquals(launchPadWithNodeId, launchPadInformation))
                    {
                        reviewedNodeId.ReviewComments = $"NodeId {nodeId} is already used by LaunchPad " +
                            $"{launchPadWithNodeId.Definition.Identifier}.";
                        nodeId = -1;
                    }
                }

                // Deal with default values
                if (nodeId == -1)
                {
                    while (nodeIdToLaunchPad.ContainsKey(freeNodeIdSearchPosition))
                    {
                        if (freeNodeIdSearchPosition == byte.MaxValue)
                        {
                            reviewedNodeId.ReviewComments = $"Cannot find a free NodeId value, are we really trying " +
                                $"to run a {byte.MaxValue + 1} cluster?";
                            break;
                        }
                        ++freeNodeIdSearchPosition;
                    }

                    if (!nodeIdToLaunchPad.ContainsKey(freeNodeIdSearchPosition))
                    {
                        nodeId = freeNodeIdSearchPosition;
                        nodeIdToLaunchPad.Add(freeNodeIdSearchPosition, launchPadInformation);
                    }
                }

                // Conclude the review
                launchPadInformation.NodeId = nodeId;
                reviewedNodeId.Value.Value = nodeId;
                PublishReview(reviewedNodeId);
            }
        }

        /// <summary>
        /// Review NodeRole LaunchParameter for the different launchpads.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        void ReviewNodeRoles(MissionControlMirror missionControlMirror)
        {
            var orderedLaunchPads = missionControlMirror.LaunchPadsInformation.OrderBy(lpi => lpi.NodeId).ToList();
            Dictionary<int, string> reviewComments = new();

            // Let's assign NodeRole straight from parameters to review without changing anything
            foreach (var launchPad in orderedLaunchPads)
            {
                launchPad.StartRole = NodeRole.Unassigned;

                var nodeRoleForReview = missionControlMirror.LaunchParametersForReview.Values
                    .FirstOrDefault(lpfr => lpfr.LaunchPadId == launchPad.Definition.Identifier &&
                        lpfr.Value.Id == LaunchParameterConstants.NodeRoleParameterId);
                if (nodeRoleForReview == null)
                {
                    // This is not supposed to happen, MissionControl should have created a NodeRole
                    // LaunchParameterForReview for every launch pad...  Let's just skip it as we cannot do anything to
                    // fix this problem...
                    // TODO: Log
                    continue;
                }

                if (nodeRoleForReview.Value.Value is string value)
                {
                    if (Enum.TryParse<NodeRole>(value, out var role))
                    {
                        launchPad.StartRole = role;
                    }
                }
            }

            // Then let's do a few different passes to correctly assign node roles
            AssignEmitterNodes(orderedLaunchPads, reviewComments);
            AssignBackupNodes(orderedLaunchPads, reviewComments, missionControlMirror);
            UnassignedToRepeaters(orderedLaunchPads);

            // Lets publish review for NodeRoleParameterId for all the nodes.
            foreach (var launchPad in orderedLaunchPads)
            {
                var nodeRoleForReview = missionControlMirror.LaunchParametersForReview.Values
                    .FirstOrDefault(lpfr => lpfr.LaunchPadId == launchPad.Definition.Identifier &&
                        lpfr.Value.Id == LaunchParameterConstants.NodeRoleParameterId);
                if (nodeRoleForReview == null)
                {
                    // This is not supposed to happen, MissionControl should have created a NodeRole
                    // LaunchParameterForReview for every launch pad...  Let's just skip it as we cannot do anything to
                    // fix this problem...
                    // TODO: Log
                    continue;
                }
                LaunchParameterForReview reviewedNodeRole = nodeRoleForReview.DeepClone();

                reviewedNodeRole.Value.Value = launchPad.StartRole.ToString();
                if (reviewComments.TryGetValue(launchPad.NodeId, out var reviewComment))
                {
                    reviewedNodeRole.ReviewComments = reviewComment;
                }
                PublishReview(reviewedNodeRole);
            }
        }

        /// <summary>
        /// Ensure we have one launchpad that is assigned the emitter node role.
        /// </summary>
        /// <param name="orderedLaunchPads">Ordered <see cref="LaunchPadInformation"/> list.</param>
        /// <param name="reviewComments">Dictionary to which we add review comments if a change deserve a comment.
        /// </param>
        static void AssignEmitterNodes(List<LaunchPadInformation> orderedLaunchPads,
            Dictionary<int, string> reviewComments)
        {
            int launchpadIndex;
            bool emitterFound = false;
            for (launchpadIndex = 0; launchpadIndex < orderedLaunchPads.Count; ++launchpadIndex)
            {
                var launchPad = orderedLaunchPads[launchpadIndex];
                if (launchPad.StartRole == NodeRole.Emitter)
                {
                    emitterFound = true;
                    break;
                }
            }
            for (++launchpadIndex; launchpadIndex < orderedLaunchPads.Count; ++launchpadIndex)
            {
                var launchPad = orderedLaunchPads[launchpadIndex];
                if (launchPad.StartRole == NodeRole.Emitter)
                {
                    launchPad.StartRole = NodeRole.Unassigned;
                    reviewComments[launchPad.NodeId] = $"There can only be one emitter, role changed to unassigned " +
                        $"(which will then be reassigned to another role).";
                }
            }
            foreach (var nodeRoleForEmitter in k_SortedNodeRolesForEmitter)
            {
                if (emitterFound)
                {
                    break;
                }
                foreach (var launchPad in orderedLaunchPads)
                {
                    if (launchPad.StartRole == nodeRoleForEmitter)
                    {
                        launchPad.StartRole = NodeRole.Emitter;
                        if (nodeRoleForEmitter != NodeRole.Unassigned)
                        {
                            reviewComments[launchPad.NodeId] = "LaunchPad was assigned the role of emitter (since no " +
                                "node had the role).";
                        }
                        emitterFound = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Deal with assignation of backup nodes.
        /// </summary>
        /// <param name="orderedLaunchPads">Ordered <see cref="LaunchPadInformation"/> list.</param>
        /// <param name="reviewComments">Dictionary to which we add review comments if a change deserve a comment.
        /// </param>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        /// <remarks>Nodes manually assigned as backup are kept as backup and other nodes will be transformed to
        /// backup nodes if the count is &lt; the LaunchParameterConstants.BackupNodeCountParameterId parameter.
        /// </remarks>
        static void AssignBackupNodes(List<LaunchPadInformation> orderedLaunchPads,
            Dictionary<int, string> reviewComments, MissionControlMirror missionControlMirror)
        {
            // Find how many backup nodes do we need (should be the same for all launchpads since
            // LaunchParameterConstants.BackupNodeCountParameterId is a global parameter).
            var backupNodeCountForReview = missionControlMirror.LaunchParametersForReview.Values
                .FirstOrDefault(lpfr => lpfr.LaunchPadId == orderedLaunchPads.First().Definition.Identifier &&
                    lpfr.Value.Id == LaunchParameterConstants.BackupNodeCountParameterId);
            if (backupNodeCountForReview == null)
            {
                // This is not supposed to happen, MissionControl should have created a NodeRole
                // LaunchParameterForReview for every launch pad...  Let's just skip it as we cannot do anything to
                // fix this problem...
                // TODO: Log
                return;
            }

            if (backupNodeCountForReview.Value.Value is not int requestedBackupNodeCount)
            {
                // Shouldn't be the case either...
                // TODO: Log
                return;
            }

            // We will want to start assigning backup nodes to the node ids that are the highest first, so reverse the
            // order of orderedLaunchPads.
            List<LaunchPadInformation> reversedLaunchpads = new(orderedLaunchPads);
            reversedLaunchpads.Reverse();

            // Go through the launchpad and change role until we have enough backup nodes.
            int currentBackupNodeCount =
                orderedLaunchPads.Count(lpi => lpi.StartRole == NodeRole.Backup);
            foreach (var nodeRoleForEmitter in k_SortedNodeRolesForBackup)
            {
                if (currentBackupNodeCount >= requestedBackupNodeCount)
                {
                    break;
                }
                foreach (var launchPad in reversedLaunchpads)
                {
                    if (launchPad.StartRole == nodeRoleForEmitter)
                    {
                        launchPad.StartRole = NodeRole.Backup;
                        if (nodeRoleForEmitter != NodeRole.Unassigned)
                        {
                            reviewComments[launchPad.NodeId] = $"LaunchPad was assigned the role of backup (to reach " +
                                "the requested number of backup nodes).";
                        }
                        ++currentBackupNodeCount;

                        if (currentBackupNodeCount >= requestedBackupNodeCount)
                        {
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Transform launchpads with the unassigned node role to repeaters.
        /// </summary>
        /// <param name="orderedLaunchPads">Ordered <see cref="LaunchPadInformation"/> list.</param>
        static void UnassignedToRepeaters(List<LaunchPadInformation> orderedLaunchPads)
        {
            foreach (var launchPad in orderedLaunchPads)
            {
                if (launchPad.StartRole == NodeRole.Unassigned)
                {
                    launchPad.StartRole = NodeRole.Repeater;
                }
            }
        }

        /// <summary>
        /// Review RepeaterCount LaunchParameter for the different launchpads.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        void ComputeRepeaterCount(MissionControlMirror missionControlMirror)
        {
            int repeaterCount = missionControlMirror.LaunchPadsInformation.Count(
                lpi => lpi.StartRole == NodeRole.Repeater);

            foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                var launchPadId = launchPadInformation.Definition.Identifier;
                var repeaterCountForReview = missionControlMirror.LaunchParametersForReview.Values
                    .FirstOrDefault(lpfr => lpfr.LaunchPadId == launchPadId &&
                        lpfr.Value.Id == LaunchParameterConstants.RepeaterCountParameterId);
                if (repeaterCountForReview == null)
                {
                    // This is not supposed to happen, MissionControl should have created a RepeaterCount
                    // LaunchParameterForReview for every launch pad...  Let's just skip it as we cannot do anything to
                    // fix this problem...
                    // TODO: Log
                    continue;
                }

                LaunchParameterForReview reviewedRepeaterCount = repeaterCountForReview.DeepClone();
                reviewedRepeaterCount.Value.Value = repeaterCount;
                PublishReview(reviewedRepeaterCount);
            }
        }

        /// <summary>
        /// Review BackupNodeCount LaunchParameter for the different launchpads.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        void ComputeBackupNodeCount(MissionControlMirror missionControlMirror)
        {
            int backupNodeCount = missionControlMirror.LaunchPadsInformation.Count(
                lpi => lpi.StartRole == NodeRole.Backup);

            foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                var launchPadId = launchPadInformation.Definition.Identifier;
                var backupNodeCountForReview = missionControlMirror.LaunchParametersForReview.Values
                    .FirstOrDefault(lpfr => lpfr.LaunchPadId == launchPadId &&
                        lpfr.Value.Id == LaunchParameterConstants.BackupNodeCountParameterId);
                if (backupNodeCountForReview == null)
                {
                    // This is not supposed to happen, MissionControl should have created a RepeaterCount
                    // LaunchParameterForReview for every launch pad...  Let's just skip it as we cannot do anything to
                    // fix this problem...
                    // TODO: Log
                    continue;
                }

                LaunchParameterForReview reviewedRepeaterCount = backupNodeCountForReview.DeepClone();
                reviewedRepeaterCount.Value.Value = backupNodeCount;
                PublishReview(reviewedRepeaterCount);
            }
        }

        /// <summary>
        /// Compute the port to which the capsule processes will be listening for REST requests.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        void ComputeCapsulePorts(MissionControlMirror missionControlMirror)
        {
            foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                var launchPadId = launchPadInformation.Definition.Identifier;
                var capsulePortForReview = missionControlMirror.LaunchParametersForReview.Values
                    .FirstOrDefault(lpfr => lpfr.LaunchPadId == launchPadId &&
                        lpfr.Value.Id == LaunchParameterConstants.CapsuleBasePortParameterId);
                if (capsulePortForReview == null)
                {
                    // This is not supposed to happen, MissionControl should have created a CapsulePort
                    // LaunchParameterForReview for every launch pad...  Let's just skip it as we cannot do anything to
                    // fix this problem...
                    // TODO: Log
                    continue;
                }

                LaunchParameterForReview reviewedCapsulePort = capsulePortForReview.DeepClone();

                // Get the proposed value
                int capsulePort;
                try
                {
                    capsulePort = Convert.ToInt32(reviewedCapsulePort.Value.Value);
                }
                catch (Exception)
                {
                    reviewedCapsulePort.ReviewComments = "Must be an integer.";
                    capsulePort = LaunchParameterConstants.DefaultCapsuleBasePort;
                }

                // Find the index of the launchpad in the configuration of the launch complex.  This will be the
                // increment applied to the configured port.
                int launchPadIndex = launchPadInformation.ComplexConfiguration.LaunchPads.FindIndex(
                    lp => lp.Identifier == launchPadId);
                if (launchPadIndex >= 0)
                {
                    capsulePort += launchPadIndex;
                }
                else
                {
                    // TODO: Log
                    reviewedCapsulePort.ReviewComments = "Unexpected capcom error, let's hope this port is not already used...";
                    continue;
                }

                // Conclude review of the parameter
                launchPadInformation.CapsulePort = capsulePort;
                reviewedCapsulePort.Value.Value = capsulePort;
                PublishReview(reviewedCapsulePort);
            }
        }

        /// <summary>
        /// Publish a parameter review.
        /// </summary>
        /// <param name="reviewedParameter">Reviewed parameter.</param>
        void PublishReview(LaunchParameterForReview reviewedParameter)
        {
            reviewedParameter.Ready = true;
            m_HttpClient.PutAsJsonAsync(k_PutUri, reviewedParameter).Wait();
        }

        /// <summary>
        /// HttpClient already configured (<see cref="HttpClient.BaseAddress"/>) to send to MissionControl.
        /// </summary>
        readonly HttpClient m_HttpClient;

        /// <summary>
        /// Last version of <see cref="MissionControlMirror.LaunchConfiguration"/> that the <see cref="Process"/> method
        /// was called on.
        /// </summary>
        ulong m_LaunchConfigurationLastVersion;
        /// <summary>
        /// Last version of <see cref="MissionControlMirror.Complexes"/> that the <see cref="Process"/> method was
        /// called on.
        /// </summary>
        ulong m_ComplexesLastVersion;
        /// <summary>
        /// Last version of <see cref="MissionControlMirror.LaunchParametersForReview"/> that the <see cref="Process"/>
        /// method was called on.
        /// </summary>
        ulong m_LaunchParametersForReviewLastVersion;
        /// <summary>
        /// Last version of <see cref="MissionControlMirror.Status"/> that the <see cref="Process"/> method was called
        /// on.
        /// </summary>
        ulong m_StatusLastVersion;
        /// <summary>
        /// <see cref="Status.State"/> when we last computed <see cref="MissionControlMirror.LaunchPadsInformation"/>.
        /// </summary>
        State? m_ComputedOnState;
        /// <summary>
        /// <see cref="Status.EnteredStateTime"/> when we last computed
        /// <see cref="MissionControlMirror.LaunchPadsInformation"/>.
        /// </summary>
        DateTime m_ComputedOnEnteredStateTime;

        const string k_PutUri = "api/v1/currentMission/launchParametersForReview";
        static readonly NodeRole[] k_SortedNodeRolesForEmitter = new[] {NodeRole.Unassigned, NodeRole.Repeater,
            NodeRole.Backup};
        static readonly NodeRole[] k_SortedNodeRolesForBackup = new[] {NodeRole.Unassigned, NodeRole.Repeater};
    }
}
