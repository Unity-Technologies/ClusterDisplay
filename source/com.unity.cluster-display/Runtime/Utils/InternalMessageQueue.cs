using System.Collections.Concurrent;

namespace Utils
{
    /// <summary>
    /// Message queue used to send some messages to whoever want to consume them within the process.
    /// </summary>
    /// <typeparam name="T">Type of messages in the message queue.</typeparam>
    public class InternalMessageQueue<T>: ConcurrentQueue<T>
    {
        /// <summary>
        /// Access to the singleton instance.
        /// </summary>
        public static InternalMessageQueue<T> Instance { get; } = new();
    }

    /// <summary>
    /// Message to be posted to a <see cref="InternalMessageQueue{InternalQuitMessage}"/> indicating that the cluster
    /// should quit.
    /// </summary>
    /// <remarks>At the moment there are not parameters in the message, the presence of the message tels everything we
    /// need to know about quitting...</remarks>
    public class InternalQuitMessage
    {
    }
}
