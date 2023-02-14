using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.ClusterDisplay.Utils;
using Utils;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    /// <summary>
    /// Handle <see cref="MessagesId.ChangeClusterTopology"/> messages.
    /// </summary>
    public class ChangeClusterTopologyMessageHandler: IMessageHandler
    {
        /// <summary>
        /// Constructor
        /// </summary>

        public ChangeClusterTopologyMessageHandler()
        {
        }

        /// <inheritdoc/>
        public async ValueTask HandleMessage(NetworkStream networkStream)
        {
            var header = await networkStream.ReadStructAsync<ChangeClusterTopologyMessageHeader>(m_MessageReadBuffer);
            if (header == null)
            {
                return;
            }

            List<ChangeClusterTopologyEntry> entries = new();
            for (int i = 0; i < header.Value.EntriesCount; ++i)
            {
                var entry = await networkStream.ReadStructAsync<ChangeClusterTopologyEntry>(m_MessageReadBuffer);
                if (entry == null)
                {
                    break;
                }
                entries.Add(entry.Value);
            }

            if (ServiceLocator.TryGet<IClusterSyncState>(out var clusterSync))
            {
                clusterSync.UpdatedClusterTopology = entries;
            }

            // Done processing the quit request
            await networkStream.WriteStructAsync(new ChangeClusterTopologyResponse(), m_ResponseWriteBuffer);
        }

        /// <summary>
        /// Buffer used when reading from the network stream.
        /// </summary>
        byte[] m_MessageReadBuffer = new byte[Math.Max(Marshal.SizeOf<ChangeClusterTopologyMessageHeader>(),
                                                       Marshal.SizeOf<ChangeClusterTopologyEntry>())];

        /// <summary>
        /// Buffer used when writing to the network stream.
        /// </summary>
        byte[] m_ResponseWriteBuffer = new byte[Marshal.SizeOf<ChangeClusterTopologyResponse>()];
    }
}
