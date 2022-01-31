namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Defines constants used for server discovery.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// The default UDP port used for server discovery.
        /// </summary>
        public const ushort DiscoveryPort = 9876;
        
        /// <summary>
        /// Port for forwarding discovery broadcasts
        /// </summary>
        public const ushort BroadcastProxyPort = 11000;
        
        /// <summary>
        /// The number of characters that may be used in the name strings.
        /// </summary>
        public const int MaxHostNameLength = 256;
        
        /// <summary>
        /// The number of characters that may be used in a path string.
        /// </summary>
        public const int PathMaxLength = 260;
        
        /// <summary>
        /// The number of characters that may be used in a log string.
        /// </summary>
        public const int LogMaxLength = 140;
        
        /// <summary>
        /// The size of the message buffers in bytes. This limits the size of the messages used by the discovery protocol.
        /// </summary>
        public const int BufferSize = 9216;
    }
}
