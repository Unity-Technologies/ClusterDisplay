using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    /// <summary>
    /// Send a message to capcom about change in the status to the capsule.
    /// </summary>
    public class SendCapsuleStatus: IToCapcomMessage
    {
        /// <summary>
        /// Method to get a new <see cref="SendCapsuleStatus"/> (using pooling mechanism).
        /// </summary>
        public static SendCapsuleStatus New()
        {
            lock (s_LinkedPoolLock)
            {
                return s_LinkedPool.Get();
            }
        }

        public NodeRole NodeRole { get; set; }
        public byte NodeId { get; set; }
        public byte RenderNodeId { get; set; }

        public Guid MessageId => MessagesId.CapsuleStatus;

        public async ValueTask Send(NetworkStream networkStream)
        {
            CapsuleStatusMessage message = new() {NodeRole = NodeRole, NodeId = NodeId,
                RenderNodeId = RenderNodeId};

            // Send the message
            await networkStream.WriteStructAsync(message, m_BytesBuffer);

            // Read the response
            var response = await networkStream.ReadStructAsync<CapsuleStatusResponse>(m_BytesBuffer);
            if (!response.HasValue)
            {
                throw new InvalidOperationException("Connection broken while waiting for response.");
            }
            // Remark: CapsuleStatusResponse is empty, so nothing to look at...
        }

        public void Dispose()
        {
            lock (s_LinkedPoolLock)
            {
                s_LinkedPool.Release(this);
            }
        }

        /// <summary>
        /// Private constructor (to force using the pooling methods).
        /// </summary>
        SendCapsuleStatus() { }

        byte[] m_BytesBuffer = new byte[Marshal.SizeOf<CapsuleStatusMessage>()];

        static object s_LinkedPoolLock = new();
        static LinkedPool<SendCapsuleStatus> s_LinkedPool = new(() => new());
    }
}
