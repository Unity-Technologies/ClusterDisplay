using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utils;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    /// <summary>
    /// Handle <see cref="Capsule.MessagesId.Land"/> messages.
    /// </summary>
    public class LandMessageHandler: IMessageHandler
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="toCancel">To cancel when we are asked to land.</param>
        public LandMessageHandler(CancellationTokenSource toCancel)
        {
            m_ToCancel = toCancel;
        }

        /// <inheritdoc/>
        public async ValueTask HandleMessage(NetworkStream networkStream)
        {
            var landMessage = await networkStream.ReadStructAsync<LandMessage>(m_MessageReadBuffer);
            if (landMessage == null)
            {
                return;
            }

            // Post the internal message indicating that we should quit
            InternalMessageQueue<InternalQuitMessage>.Instance.Enqueue(new());

            // Register to stop capsule resources when application starts to quit
            Application.quitting += () => m_ToCancel.Cancel();

            // Done processing the quit request
            await networkStream.WriteStructAsync(new LandResponse(), m_ResponseWriteBuffer);
        }

        /// <summary>
        /// Buffer used when receiving a <see cref="LandMessage"/>..
        /// </summary>
        byte[] m_MessageReadBuffer = new byte[Marshal.SizeOf<LandMessage>()];

        /// <summary>
        /// Buffer used to produce a <see cref="LandResponse"/>.
        /// </summary>
        byte[] m_ResponseWriteBuffer = new byte[Marshal.SizeOf<LandResponse>()];

        /// <summary>
        /// To cancel when we are asked to land.
        /// </summary>
        CancellationTokenSource m_ToCancel;
    }
}
