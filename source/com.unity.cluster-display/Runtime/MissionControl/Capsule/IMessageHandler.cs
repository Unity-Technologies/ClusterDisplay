using System.Net.Sockets;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    /// <summary>
    /// Interface for handles of capsule messages.
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        /// Handle a message from the capsule.
        /// </summary>
        /// <param name="networkStream">Stream over which to read the message and over which to send the response.</param>
        ValueTask HandleMessage(NetworkStream networkStream);
    }
}
