using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    /// <summary>
    /// Interface for messages send from the capsule to capcom.
    /// </summary>
    public interface IToCapcomMessage: IDisposable
    {
        /// <summary>
        /// Identifier of the message that will be sent in <see cref="Send"/>.
        /// </summary>
        Guid MessageId { get; }

        /// <summary>
        /// Send the message struct from the capsule to capcom (and handle the response).
        /// </summary>
        /// <param name="networkStream">Stream over which to send the message and on which we read the response.</param>
        /// <remarks>Message identifier <see cref="Guid"/> has already been sent by the caller of this method.</remarks>
        ValueTask Send(NetworkStream networkStream);
    }
}
