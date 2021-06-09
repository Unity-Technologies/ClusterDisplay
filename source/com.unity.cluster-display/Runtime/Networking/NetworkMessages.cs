﻿using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.ClusterDisplay
{

    public enum EMessageType
    {
        AckMsgRx,
        StartFrame,
        FrameDone,
        HelloMaster,
        WelcomeSlave,
        GlobalShutdownRequest
    }

    /// <summary>
    /// Enum containing the Cluster node roles.
    /// </summary>
    public enum ENodeRole
    {
        //Unassigned,
        /// <summary>
        /// The source node that broadcasts synchronization data.
        /// </summary>
        Master,
        /// <summary>
        /// The client nodes that receive synchronization data.
        /// </summary>
        Slave,
        //HotStandby,
        //Dead
    }

    public struct NetworkingStats
    {
        public int rxQueueSize;
        public int txQueueSize;
        public int pendingAckQueueSize;
        public int failedMsgs;
        public int totalResends;
        public int msgsSent;
    }

    public class NetworkingHelpers
    {
        public static byte[] AllocateMessageWithPayload<T>() where T : struct
        {
            return new byte[Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<T>()];
        }

    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MessageHeader
    {
        [Flags]
        public enum EFlag
        {
            None = 0,
            DoesNotRequireAck = 1 << 0,
            Broadcast = 1 << 1,
            Resending = 1 << 2,
            LoopBackToSender =  1 << 3,
            SentFromEditorProcess = 1 << 4,
        }

        public const byte CurrentVersion = 1;

        public byte m_Version;
        private byte m_MessageType; // see EMessageType
        public byte OriginID;
        public UInt64 DestinationIDs; // bit field
        public UInt64 SequenceID;
        public UInt16 PayloadSize;
        private UInt16 m_Flags;
        public UInt16 OffsetToPayload;

        public EFlag Flags
        {
            get => (EFlag) m_Flags;
            set => m_Flags = (UInt16) value;
        }

        public EMessageType MessageType
        {
            get => (EMessageType)m_MessageType;
            set => m_MessageType = (byte)value;
        }

        // Output array will be sized to contain payload also, but payload is not yet stored in output array.
        public unsafe byte[] ToByteArray()
        {
            var len = Marshal.SizeOf<MessageHeader>();
            byte[] arr = new byte[len + PayloadSize];

            fixed (MessageHeader* msgPtr = &this)
            {
                var ptr = (byte*)msgPtr;
                Marshal.Copy( (IntPtr)ptr, arr, 0, len);
            }

            return arr;
        }

        public unsafe void StoreInBuffer( byte[] dest )
        {
            var len = Marshal.SizeOf<MessageHeader>();

            fixed (MessageHeader* msgPtr = &this)
            {
                var ptr = (byte*)msgPtr;
                Marshal.Copy((IntPtr)ptr, dest, 0, len);
            }
        }

        public static unsafe MessageHeader FromByteArray(byte[] arr)
        {
            MessageHeader header = default;
            var len = Marshal.SizeOf<MessageHeader>();
            var ptr = &header;
            Marshal.Copy(arr, 0, (IntPtr)ptr, len);

            return header;
        }

        public static unsafe MessageHeader FromByteArray(NativeArray<byte> arr)
        {
            MessageHeader header = default;
            var len = Marshal.SizeOf<MessageHeader>();
            var ptr = &header;
            UnsafeUtility.MemCpy( ptr, arr.GetUnsafePtr(), len );
            return header;
        }

    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AdvanceFrame
    {
        public static Guid CoreTimeStateID = Guid.Parse("E9F8D0DD-AA7F-4DC3-B604-1011A482BD48");
        public static Guid CoreInputStateID = Guid.Parse("07376B8C-9F18-4DA2-8795-25024F10E572");
        public static Guid CoreRandomStateID = Guid.Parse("ADFB31A9-FE1D-4108-9A4F-D8A0BD1EA9BC");
        public static Guid ClusterInputStateID = Guid.Parse("09D9220F-667A-4EA8-A384-01DAD099A786");
        public static Guid RPCStateID = Guid.Parse("EC624899-7331-4959-A8FD-5346B610768B");

        public UInt64 FrameNumber;

        public unsafe void StoreInBuffer(byte[] dest, int offset )
        {
            var len = Marshal.SizeOf<AdvanceFrame>();

            fixed (AdvanceFrame* msgPtr = &this)
            {
                var ptr = (byte*)msgPtr;
                Marshal.Copy((IntPtr)ptr, dest, offset, len);
            }
        }

        public static unsafe AdvanceFrame FromByteArray(byte[] arr, int offset)
        {
            AdvanceFrame msg = default;
            var len = Marshal.SizeOf<AdvanceFrame>();
            var ptr = &msg;
            Marshal.Copy(arr, offset, (IntPtr)ptr, len);

            return msg;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RolePublication
    {
        private byte m_NodeRole; // see ENodeRole
        public ENodeRole NodeRole
        {
            get => (ENodeRole)m_NodeRole;
            set => m_NodeRole = (byte)value;
        }

        public unsafe void StoreInBuffer(byte[] dest, int offset)
        {
            var len = Marshal.SizeOf<RolePublication>();

            fixed (RolePublication* msgPtr = &this)
            {
                var ptr = (byte*)msgPtr;
                Marshal.Copy((IntPtr)ptr, dest, offset, len);
            }
        }

        public static unsafe RolePublication FromByteArray(byte[] arr, int offset)
        {
            RolePublication msg = default;
            var len = Marshal.SizeOf<RolePublication>();
            var ptr = &msg;
            Marshal.Copy(arr, offset, (IntPtr)ptr, len);

            return msg;
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct FrameDone
    {
        public UInt64 FrameNumber;

        public unsafe void StoreInBuffer(byte[] dest, int offset)
        {
            var len = Marshal.SizeOf<FrameDone>();

            fixed (FrameDone* msgPtr = &this)
            {
                var ptr = (byte*)msgPtr;
                Marshal.Copy((IntPtr)ptr, dest, offset, len);
            }
        }

        public static unsafe FrameDone FromByteArray(byte[] arr, int offset)
        {
            FrameDone msg = default;
            var len = Marshal.SizeOf<FrameDone>();
            var ptr = &msg;
            Marshal.Copy(arr, offset, (IntPtr)ptr, len);

            return msg;
        }
    }


}