using System;

namespace Unity.ClusterDisplay
{
    static class ClusterParams
    {
        /// <summary>
        /// How long should the Master wait for clients to proceed before continuing
        /// How long should Slaves wait for master to respond to registration before quitting 
        /// </summary>
        internal static TimeSpan RegisterTimeout = TimeSpan.FromSeconds(30);
        /// <summary>
        /// How long should the Master wait for clients before kicking them from the cluster and continuing
        /// How Long should Clients wait for GoFromMaster before quitting.
        /// </summary>
        internal static TimeSpan CommunicationTimeout = TimeSpan.FromSeconds(5);
    }
}