namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Defines constants used for server discovery.
    /// </summary>
    static class Constants
    {
        /// <summary>
        /// The default UDP port used for server discovery.
        /// </summary>
        public const ushort DefaultPort = 9876;

        /// <summary>
        /// The number of characters that may be used in the name strings.
        /// </summary>
        public const int StringMaxLength = 32;
        
        /// <summary>
        /// The size of the message buffers in bytes. This limits the size of the messages used by the discovery protocol.
        /// </summary>
        public const int BufferSize = 1024;
    }
}
