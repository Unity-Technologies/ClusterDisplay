using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    /// <summary>
    /// Direction messages are sent over the connection.
    /// </summary>
    public enum MessageDirection
    {
        /// <summary>
        /// Messages are sent from Capcom to the Capsule.
        /// </summary>
        CapcomToCapsule,
        /// <summary>
        /// Messages are sent from the Capsule to Capcom.
        /// </summary>
        CapsuleToCapcom
    }

    /// <summary>
    /// Chunk of information sent when establishing a connection with a Capsule.
    /// </summary>
    public struct ConnectionInit
    {
        /// <summary>
        /// Direction messages are sent over the connection.
        /// </summary>
        public MessageDirection MessageFlow;
    }
}
