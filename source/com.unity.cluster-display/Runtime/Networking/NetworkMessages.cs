using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.ClusterDisplay
{

    public enum EMessageType
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
    public enum ENodeRole
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

    public struct NetworkingStats
    {
        public int rxQueueSize;
        public int txQueueSize;
        public int pendingAckQueueSize;
        public int failedMsgs;
        public int totalResends;
        public int msgsSent;
    }

    public enum StateID : byte
    {
        End = 0,
        Time = 1,
        Input = 2,
        Random = 3,
        ClusterInput = 4
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
    public struct EmitterLastFrameData
    {
        public UInt64 FrameNumber;

        public unsafe void StoreInBuffer(byte[] dest, int offset )
        {
            var len = Marshal.SizeOf<EmitterLastFrameData>();

            fixed (EmitterLastFrameData* msgPtr = &this)
            {
                var ptr = (byte*)msgPtr;
                Marshal.Copy((IntPtr)ptr, dest, offset, len);
            }
        }

        public static unsafe EmitterLastFrameData FromByteArray(byte[] arr, int offset)
        {
            EmitterLastFrameData msg = default;
            var len = Marshal.SizeOf<EmitterLastFrameData>();
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
    public struct RepeaterEnteredNextFrame
    {
        public UInt64 FrameNumber;

        public unsafe void StoreInBuffer(byte[] dest, int offset)
        {
            var len = Marshal.SizeOf<RepeaterEnteredNextFrame>();

            fixed (RepeaterEnteredNextFrame* msgPtr = &this)
            {
                var ptr = (byte*)msgPtr;
                Marshal.Copy((IntPtr)ptr, dest, offset, len);
            }
        }

        public static unsafe RepeaterEnteredNextFrame FromByteArray(byte[] arr, int offset)
        {
            RepeaterEnteredNextFrame msg = default;
            var len = Marshal.SizeOf<RepeaterEnteredNextFrame>();
            var ptr = &msg;
            Marshal.Copy(arr, offset, (IntPtr)ptr, len);

            return msg;
        }
    }


}
