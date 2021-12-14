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
        public static byte[] AllocateMessageWithPayload<T>() where T : struct
        {
            return new byte[Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<T>()];
        }

        public unsafe static T BytesToStruct<T>(byte[] arr, int offset) where T : unmanaged
        {
            fixed (byte * ptr = arr)
                return Marshal.PtrToStructure<T>((IntPtr)(ptr + offset));
        }

        public unsafe static void StructToBytes<T>(byte[] dest, int offset, ref T s) where T : struct
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

        public unsafe static void StructToBytes<T>(NativeArray<byte> dest, int offset, ref T s) where T : struct
        {
                UnsafeUtility.MemCpy(
                    (byte*)dest.GetUnsafePtr() + offset, 
                    UnsafeUtility.AddressOf(ref s), 
                    Marshal.SizeOf<T>());
        }
    }

    internal interface IBlittable<T> where T : unmanaged
    {
        void StoreInBuffer(NativeArray<byte> dest, int offset);

        public static T FromByteArray(NativeArray<byte> arr, int offset) =>
            NetworkingHelpers.BytesToStruct<T>(arr, offset);
        
        void StoreInBuffer(byte[] dest, int offset);

        public static T FromByteArray(byte[] arr, int offset) =>
            NetworkingHelpers.BytesToStruct<T>(arr, offset);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MessageHeader : IBlittable<MessageHeader>
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

        public void StoreInBuffer(NativeArray<byte> dest, int offset) =>
            NetworkingHelpers.StructToBytes(dest, offset, ref this);

        public void StoreInBuffer(byte[] dest, int offset) =>
            NetworkingHelpers.StructToBytes(dest, offset, ref this);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EmitterLastFrameData : IBlittable<EmitterLastFrameData>
    {
        public static Guid CoreTimeStateID = Guid.Parse("E9F8D0DD-AA7F-4DC3-B604-1011A482BD48");
        public static Guid CoreInputStateID = Guid.Parse("07376B8C-9F18-4DA2-8795-25024F10E572");
        public static Guid CoreRandomStateID = Guid.Parse("ADFB31A9-FE1D-4108-9A4F-D8A0BD1EA9BC");
        public static Guid ClusterInputStateID = Guid.Parse("09D9220F-667A-4EA8-A384-01DAD099A786");

        public UInt64 FrameNumber;

        public void StoreInBuffer(NativeArray<byte> dest, int offset) =>
            NetworkingHelpers.StructToBytes(dest, offset, ref this);
        
        public void StoreInBuffer(byte[] dest, int offset) =>
            NetworkingHelpers.StructToBytes(dest, offset, ref this);
    }
    
    [StructLayout(LayoutKind.Sequential)]
    internal struct RolePublication : IBlittable<RolePublication>
    {
        private byte m_NodeRole; // see ENodeRole
        public ENodeRole NodeRole
        {
            get => (ENodeRole)m_NodeRole;
            set => m_NodeRole = (byte)value;
        }

        public void StoreInBuffer(NativeArray<byte> dest, int offset) =>
            NetworkingHelpers.StructToBytes(dest, offset, ref this);
        
        public void StoreInBuffer(byte[] dest, int offset) =>
            NetworkingHelpers.StructToBytes(dest, offset, ref this);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RepeaterEnteredNextFrame : IBlittable<RepeaterEnteredNextFrame>
    {
        public UInt64 FrameNumber;

        public void StoreInBuffer(NativeArray<byte> dest, int offset) =>
            NetworkingHelpers.StructToBytes(dest, offset, ref this);
        
        public void StoreInBuffer(byte[] dest, int offset) =>
            NetworkingHelpers.StructToBytes(dest, offset, ref this);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ClusterRuntimeConfig : IBlittable<ClusterRuntimeConfig>
    {
        public byte headlessEmitter;
        
        public void StoreInBuffer(NativeArray<byte> dest, int offset) =>
            NetworkingHelpers.StructToBytes(dest, offset, ref this);
        
        public void StoreInBuffer(byte[] dest, int offset) =>
            NetworkingHelpers.StructToBytes(dest, offset, ref this);
    }
}
