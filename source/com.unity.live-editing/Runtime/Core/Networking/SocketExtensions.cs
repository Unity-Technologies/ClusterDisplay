using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Unity.LiveEditing.LowLevel.Networking
{
    static class SocketExtensions
    {
        /// <summary>
        /// Sets the keep alive parameters for a socket.
        /// </summary>
        /// <param name="socket">The socket to configure.</param>
        /// <param name="enable">Should keep alive messages be sent by this socket.</param>
        /// <param name="time">The time in milliseconds between successful keep alives.</param>
        /// <param name="interval">The time in milliseconds between keep alive retransmissions.</param>
        public static void SetKeepAlive(this Socket socket, bool enable, int time, int interval)
        {
            try
            {
                var keepAliveValues = new byte[sizeof(uint) * 3];
                uint enablePart = (uint)(enable ? 1 : 0);
                MemoryMarshal.Write(keepAliveValues, ref enablePart);
                MemoryMarshal.Write(keepAliveValues[sizeof(uint)..], ref time);
                MemoryMarshal.Write(keepAliveValues.AsSpan(2 * sizeof(uint)), ref interval);

                socket.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }
        }
    }
}
