using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.ClusterDisplay.MissionControl.Capsule;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// Process <see cref="CapsuleStatusMessage"/>.
    /// </summary>
    public class CapsuleStatusProcessor : ICapsuleMessageProcessor
    {
        /// <inheritdoc/>
        public void Process(MissionControlMirror mirror, Guid launchpadId, Stream stream)
        {
            // Get the message
            var statusMessage = stream.ReadStructAsync<CapsuleStatusMessage>(m_IOBuffer).Result;
            if (!statusMessage.HasValue)
            {
                return;
            }

            // Relay the change to MissionControl
            try
            {
                var entries = new[] {
                    new LaunchPadReportDynamicEntry() {
                        Name = LaunchPadReportDynamicEntryConstants.StatusNodeRole,
                        Value = statusMessage.Value.NodeRole.ToString()
                    },
                    new LaunchPadReportDynamicEntry() {
                        Name = LaunchPadReportDynamicEntryConstants.StatusNodeId,
                        Value = (int)statusMessage.Value.NodeId
                    },
                    new LaunchPadReportDynamicEntry() {
                        Name = LaunchPadReportDynamicEntryConstants.StatusRenderNodeId,
                        Value = statusMessage.Value.NodeRole is NodeRole.Unassigned ?
                            -1 : statusMessage.Value.RenderNodeId
                    }
                };
                var putRet = mirror.MissionControlHttpClient.PutAsJsonAsync(
                    $"api/v1/launchPadsStatus/{launchpadId}/dynamicEntries", entries).Result;
                putRet.EnsureSuccessStatusCode();
            }
            catch (Exception)
            {
                // TODO: Log
            }

            // Send the response
            CapsuleStatusResponse response = new();
            stream.WriteStructAsync(response, m_IOBuffer).AsTask().Wait();
        }

        byte[] m_IOBuffer = new byte[Marshal.SizeOf<CapsuleStatusMessage>()];
    }
}
