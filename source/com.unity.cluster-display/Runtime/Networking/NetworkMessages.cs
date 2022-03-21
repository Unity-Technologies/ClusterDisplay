using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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

    internal enum StateID : byte
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

        public unsafe static T BytesToStruct<T>(byte[] arr, int offset) where T : unmanaged
        {
            fixed (byte * ptr = arr)
                return Marshal.PtrToStructure<T>((IntPtr)(ptr + offset));
        }

        public unsafe static void StructToBytes<T>(byte[] dest, int offset, ref T s) where T : unmanaged
        {
            fixed (byte* ptr = dest)
                UnsafeUtility.MemCpy(
                    ptr + offset, 
                    UnsafeUtility.AddressOf(ref s), 
                    Marshal.SizeOf<T>());
        }
        
        public unsafe static T BytesToStruct<T>(NativeArray<byte> arr, int offset) where T : unmanaged
        {
            var ptr = (byte*)arr.GetUnsafePtr() + offset;
            return Marshal.PtrToStructure<T>((IntPtr)ptr);
        }

        public unsafe static void StructToBytes<T>(NativeArray<byte> dest, int offset, ref T s) where T : unmanaged
        {
                UnsafeUtility.MemCpy(
                    (byte*)dest.GetUnsafePtr() + offset, 
                    UnsafeUtility.AddressOf(ref s), 
                    Marshal.SizeOf<T>());
        }
        
        public static void StoreInBuffer<T>(ref this T blittable, NativeArray<byte> dest, int offset)
            where T : unmanaged => StructToBytes(dest, offset, ref blittable);
      
        public static void StoreInBuffer<T>(ref this T blittable, byte[] dest, int offset = 0)
            where T : unmanaged => StructToBytes(dest, offset, ref blittable);
        
        public static T LoadStruct<T>(this NativeArray<byte> arr, int offset = 0) where T : unmanaged =>
            BytesToStruct<T>(arr, offset);
        
        public static T LoadStruct<T>(this byte[] arr, int offset = 0) where T : unmanaged =>
            BytesToStruct<T>(arr, offset);
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
