using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl
{
    static class MessageBufferExtensions
    {
        static readonly Dictionary<MessageType, Type> k_MessageToDataType = new()
        {
            {MessageType.Discovery, typeof(ServerInfo)},
            {MessageType.NodeStatus, typeof(NodeInfo)},
            {MessageType.Launch, typeof(LaunchInfo)},
            {MessageType.SyncProject, typeof(DirectorySyncInfo)},
            {MessageType.Kill, typeof(KillInfo)}
        };

        static readonly Dictionary<Type, MessageType> k_DataTypeToMessage = k_MessageToDataType
            .ToDictionary(p => p.Value, p => p.Key);


        public static int WriteMessage<T>(this byte[] buffer,
            string hostName,
            IPEndPoint endPoint,
            in T messageData) where T : struct
        {
            if (!k_DataTypeToMessage.TryGetValue(typeof(T), out var messageType))
            {
                return 0;
            }

            var header = new MessageHeader(messageType, hostName, endPoint);
            var offset = buffer.WriteStruct(header);
            offset += buffer.WriteStruct(messageData, offset);
            return offset;
        }

        static bool MatchesType<T>(MessageType type)
        {
            return k_MessageToDataType[type] == typeof(T);
        }

        public delegate void MessageHandler<T>(in MessageHeader header, in T contents);

        public static bool MatchMessage<T>(this byte[] buffer, MessageHandler<T> isMatch) where T : struct
        {
            if (buffer.Length == 0) return false;
            if (MatchesType<T>((MessageType) buffer[0]))
            {
                isMatch?.Invoke(buffer.ReadStruct<MessageHeader>(), buffer.ReadStruct<T>(Marshal.SizeOf<MessageHeader>()));
                return true;
            }

            return false;
        }

        public static bool MatchMessage<T1, T2>(
            this byte[] buffer,
            MessageHandler<T1> matches1,
            MessageHandler<T2> matches2)
            where T1 : struct
            where T2 : struct
        {
            return buffer.MatchMessage(matches1) || buffer.MatchMessage(matches2);
        }

        public static bool MatchMessage<T1, T2, T3>(
            this byte[] buffer,
            MessageHandler<T1> matches1,
            MessageHandler<T2> matches2,
            MessageHandler<T3> matches3)
            where T1 : struct
            where T2 : struct
            where T3 : struct
        {
            return buffer.MatchMessage(matches1, matches2) || buffer.MatchMessage(matches3);
        }

        public static bool MatchMessage<T1, T2, T3, T4>(
            this byte[] buffer,
            MessageHandler<T1> matches1,
            MessageHandler<T2> matches2,
            MessageHandler<T3> matches3,
            MessageHandler<T4> matches4)
            where T1 : struct
            where T2 : struct
            where T3 : struct
            where T4 : struct
        {
            return buffer.MatchMessage(matches1, matches2, matches3) || buffer.MatchMessage(matches4);
        }
    }
}
