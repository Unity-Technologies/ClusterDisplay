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

        /// <summary>
        /// Send the structure over the given stream (to initialize the connection).
        /// </summary>
        /// <param name="stream">The <see cref="NetworkStream"/> over which to send the struct.</param>
        public void Send(NetworkStream stream)
        {
            int sizeOfStruct = Marshal.SizeOf<ConnectionInit>();
            Span<byte> buffer = stackalloc byte[sizeOfStruct];
            MemoryMarshal.Write(buffer, ref this);
            stream.Write(buffer);
        }

        /// <summary>
        /// Fill the structure from the given stream.
        /// </summary>
        /// <param name="stream">The <see cref="NetworkStream"/> from which we set this struct.</param>
        public static ConnectionInit ReadFrom(NetworkStream stream)
        {
            int sizeOfStruct = Marshal.SizeOf<ConnectionInit>();
            Span<byte> buffer = stackalloc byte[sizeOfStruct];
            if (!stream.ReadAllBytes(buffer))
            {
                throw new InvalidOperationException("Failed to read from stream.");
            }
            return MemoryMarshal.Read<ConnectionInit>(buffer);
        }
    }
}
