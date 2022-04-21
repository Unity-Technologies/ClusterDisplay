﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.ClusterDisplay.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.ClusterDisplay
{
    internal enum EMessageType
    {
        AckMsgRx,
        LastFrameData,
        EnterNextFrame,
        HelloEmitter,
        WelcomeRepeater,
        GlobalShutdownRequest
    }

    /// <summary>
    /// Enum containing the Cluster node roles.
    /// </summary>
    internal enum ENodeRole
    {
        //Unassigned,
        /// <summary>
        /// The source node that broadcasts synchronization data.
        /// </summary>
        Emitter,
        /// <summary>
        /// The client nodes that receive synchronization data.
        /// </summary>
        Repeater,
        //HotStandby,
        //Dead
    }

    internal struct NetworkingStats
    {
        public int rxQueueSize;
        public int txQueueSize;
        public int pendingAckQueueSize;
        public int failedMsgs;
        public int totalResends;
        public int msgsSent;
    }

    enum StateID : byte
    {
        End = 0,
        Time = 1,
        Input = 2,
        Random = 3,
        ClusterInput = 4
    }

    internal static class NetworkingHelpers
    {
        public static byte[] AllocateMessageWithPayload<T>() where T : unmanaged
        {
            return new byte[Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<T>()];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static T BytesToStruct<T>(ReadOnlySpan<byte> arr,
            int offset) where T : unmanaged =>
            MemoryMarshal.Read<T>(arr.Slice(offset));

        public static int StoreInBuffer<T>(ref this T blittable, NativeArray<byte> dest, int offset = 0)
            where T : unmanaged => blittable.StoreInBuffer(dest.AsSpan(), offset);

        public static int StoreInBuffer<T>(ref this T blittable,
            Span<byte> dest,
            int offset = 0) where T : unmanaged =>
            MemoryMarshal.TryWrite(dest.Slice(offset),
                ref blittable)
                ? Marshal.SizeOf<T>()
                : throw new ArgumentOutOfRangeException();

        public static T LoadStruct<T>(this NativeArray<byte> arr, int offset = 0) where T : unmanaged =>
            BytesToStruct<T>(arr.AsReadOnlySpan(), offset);

        public static T LoadStruct<T>(this byte[] arr, int offset = 0) where T : unmanaged =>
            BytesToStruct<T>(arr, offset);

        public static unsafe Span<T> AsSpan<T>(this NativeArray<T> arr) where T : unmanaged =>
            new(arr.GetUnsafePtr(), arr.Length);

        public static unsafe ReadOnlySpan<T> AsReadOnlySpan<T>(this NativeArray<T> arr) where T : unmanaged =>
            new(arr.GetUnsafeReadOnlyPtr(), arr.Length);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MessageHeader
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
            Fragment = 1 << 5,
        }

        public const byte CurrentVersion = 1;

        public byte m_Version;
        private byte m_MessageType; // see EMessageType
        public byte OriginID;
        public BitVector DestinationIDs; // bit field
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
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EmitterLastFrameData
    {
        public UInt64 FrameNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RolePublication
    {
        private byte m_NodeRole; // see ENodeRole
        public ENodeRole NodeRole
        {
            get => (ENodeRole)m_NodeRole;
            set => m_NodeRole = (byte)value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RepeaterEnteredNextFrame
    {
        public UInt64 FrameNumber;
    }
}
