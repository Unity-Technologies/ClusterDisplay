using System;
using System.Runtime.InteropServices;

namespace Unity.ClusterRendering
{

    internal enum EMessageType
    {
        AckMsgRx,
        StartFrame,
        FrameDone,
        HelloMaster,
        MissedMessageReq,
        WelcomeSlave
    }
    public enum EKnownDestination : UInt16
    {
        MasterNode = 0,
        AllSlaves = 0xFFFF
    }

    public enum ENodeRole
    {
        Unassigned,
        Master,
        Slave,
        HotStandby,
        Dead
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
        }

        public const byte CurrentVersion = 1;

        public byte m_Version;
        private byte m_MessageType; // see EMessageType
        private byte m_NodeRole; // see ENodeRole
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

        public ENodeRole NodeRole
        {
            get => (ENodeRole)m_NodeRole;
            set => m_NodeRole = (byte)value;
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
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AdvanceFrame
    {
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
    internal struct FrameDone
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
