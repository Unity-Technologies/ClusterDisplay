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
            if (missionControlMirror.StatusNextVersion == m_LastStateNextVersion &&
                missionControlMirror.LaunchConfigurationNextVersion == m_LastLaunchConfigurationNextVersion &&
                missionControlMirror.ComplexesNextVersion == m_LastComplexesNextVersion &&
                missionControlMirror.LaunchParametersForReviewNextVersion == m_LastLaunchParametersForReviewNextVersion)
            {
                return;
            }
            m_LastStateNextVersion = missionControlMirror.StatusNextVersion;
            m_LastLaunchConfigurationNextVersion = missionControlMirror.LaunchConfigurationNextVersion;
            m_LastComplexesNextVersion = missionControlMirror.ComplexesNextVersion;
            m_LastLaunchParametersForReviewNextVersion = missionControlMirror.LaunchParametersForReviewNextVersion;

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
                        .First(lp => lp.Identifier == launchPadConfiguration.Identifier);
                    if (launchpadDefinition.SuitableFor.Contains(LaunchCatalog.Launchable.ClusterNodeType))
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
            // Make a first pass to find the node that was manually marked as emitter and the lowest NodeId.
            LaunchPadInformation emitterNode = null;
            int lowestNodeId = int.MaxValue;
            foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                var launchPadId = launchPadInformation.Definition.Identifier;
                var nodeRoleForReview = missionControlMirror.LaunchParametersForReview.Values
                    .FirstOrDefault(lpfr => lpfr.LaunchPadId == launchPadId &&
                        lpfr.Value.Id == LaunchParameterConstants.NodeRoleParameterId);
                if (nodeRoleForReview == null)
                {
                    continue;
                }

                if (nodeRoleForReview.Value.Value is LaunchParameterConstants.NodeRoleEmitter && emitterNode == null)
                {
                    emitterNode = launchPadInformation;
                }

                if (launchPadInformation.NodeId >= 0)
                {
                    lowestNodeId = Math.Min(launchPadInformation.NodeId, lowestNodeId);
                }
            }

            // Ok, now lets review each node role and update parameters that need updating.
            foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                var launchPadId = launchPadInformation.Definition.Identifier;
                var nodeRoleForReview = missionControlMirror.LaunchParametersForReview.Values
                    .FirstOrDefault(lpfr => lpfr.LaunchPadId == launchPadId &&
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

                // Deal with emitter
                if (emitterNode == null)
                {
                    // There was not manually specified emitter, assign the role to the lowest node id.
                    if (launchPadInformation.NodeId == lowestNodeId)
                    {
                        emitterNode = launchPadInformation;
                        if (reviewedNodeRole.Value.Value is not LaunchParameterConstants.NodeRoleUnassigned)
                        {
                            reviewedNodeRole.ReviewComments = "Changed to emitter as there was no emitter assigned " +
                                "to any node.";
                        }
                        reviewedNodeRole.Value.Value = LaunchParameterConstants.NodeRoleEmitter;
                    }
                }
                else
                {
                    // Ensure there is only one emitter
                    if (reviewedNodeRole.Value.Value is LaunchParameterConstants.NodeRoleEmitter &&
                        !ReferenceEquals(launchPadInformation, emitterNode))
                    {
                        reviewedNodeRole.ReviewComments = "There can only be one emitter, role changed to repeater.";
                        reviewedNodeRole.Value.Value = LaunchParameterConstants.NodeRoleRepeater;
                    }
                }

                // Any remaining node should be a repeater
                if (launchPadInformation.NodeId >= 0 &&
                    reviewedNodeRole.Value.Value is LaunchParameterConstants.NodeRoleUnassigned)
                {
                    reviewedNodeRole.Value.Value = LaunchParameterConstants.NodeRoleRepeater;
                }

                // Conclude the review
                launchPadInformation.NodeRole = (string)reviewedNodeRole.Value.Value;
                PublishReview(reviewedNodeRole);
            }
        }

        /// <summary>
        /// Review NodeRole LaunchParameter for the different launchpads.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        void ComputeRepeaterCount(MissionControlMirror missionControlMirror)
        {
            int repeaterCount = missionControlMirror.LaunchPadsInformation.Count(
                lpi => lpi.NodeRole == LaunchParameterConstants.NodeRoleRepeater);

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
        /// Last version of <see cref="MissionControlMirror.LaunchConfigurationNextVersion"/> that the
        /// <see cref="Process"/> method was called on.
        /// </summary>
        ulong m_LastLaunchConfigurationNextVersion = ulong.MaxValue;
        /// <summary>
        /// Last version of <see cref="MissionControlMirror.ComplexesNextVersion"/> that the <see cref="Process"/>
        /// method was called on.
        /// </summary>
        ulong m_LastComplexesNextVersion = ulong.MaxValue;
        /// <summary>
        /// Last version of <see cref="MissionControlMirror.LaunchParametersForReviewNextVersion"/> that the
        /// <see cref="Process"/> method was called on.
        /// </summary>
        ulong m_LastLaunchParametersForReviewNextVersion = ulong.MaxValue;
        /// <summary>
        /// Last version of <see cref="MissionControlMirror.StatusNextVersion"/> that the <see cref="Process"/> method
        /// was called on.
        /// </summary>
        ulong m_LastStateNextVersion = ulong.MaxValue;
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
    }
}
