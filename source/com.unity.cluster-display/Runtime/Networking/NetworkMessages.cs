using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.ClusterDisplay.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    enum StateID : int
    {
        End = 0,
        Time = 1,
        Input = 2,
        Random = 3,
        CustomEvents = 4,
        InputSystem = 5
    }

    static class NetworkingHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadStruct<T>(this ReadOnlySpan<byte> arr, int offset = 0) where T : unmanaged =>
            MemoryMarshal.Read<T>(arr.Slice(offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int StoreInBuffer<T>(ref this T blittable, NativeArray<byte> dest, int offset = 0)
            where T : unmanaged
        {
            dest.ReinterpretStore(offset, blittable);
            return Marshal.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int StoreInBuffer<T>(ref this T blittable,
            Span<byte> dest,
            int offset = 0) where T : unmanaged =>
            MemoryMarshal.TryWrite(dest.Slice(offset),
                ref blittable)
                ? Marshal.SizeOf<T>()
                : throw new ArgumentOutOfRangeException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadStruct<T>(this NativeArray<byte> arr, int offset = 0) where T : unmanaged =>
            arr.ReinterpretLoad<T>(offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadStruct<T>(this byte[] arr, int offset = 0) where T : unmanaged =>
            LoadStruct<T>((ReadOnlySpan<byte>) arr, offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Span<T> AsSpan<T>(this NativeArray<T> arr) where T : unmanaged =>
            new(arr.GetUnsafePtr(), arr.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ReadOnlySpan<T> AsReadOnlySpan<T>(this NativeArray<T> arr) where T : unmanaged =>
            new(arr.GetUnsafePtr(), arr.Length);
    }

    /// <summary>
    /// Correspond to the struct of the same name, used to identify the different messages that can be sent with
    /// <see cref="UdpAgent"/>.
    /// </summary>
    enum MessageType: byte
    {
        None,
        RegisteringWithEmitter,
        RepeaterRegistered,
        FrameData,
        RetransmitFrameData,
        RepeaterWaitingToStartFrame,
        EmitterWaitingToStartFrame,
        PropagateQuit,
        QuitReceived
    }

    /// <summary>
    /// Simple attribute (mostly for sanity check) used to associate a <see cref="MessageType"/> to a message struct.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    class MessageTypeAttribute : Attribute
    {
        public MessageType Type { get; private set; }

        public MessageTypeAttribute(MessageType type)
        {
            Type = type;
        }

        /// <summary>
        /// Returns the MessageType of the provide message struct.
        /// </summary>
        /// <typeparam name="TM">Type of the struct for which we want to get the <see cref="MessageType"/>.</typeparam>
        /// <returns>MessageType of the provide message struct or <see cref="MessageType.None"/> if no
        /// MessageTypeAttribute can be found on <see cref="TM"/>.</returns>
        public static MessageType GetTypeOf<TM>()
        {
            var messageTypeAttribute = typeof(TM).GetCustomAttribute<MessageTypeAttribute>();
            return messageTypeAttribute?.Type ?? MessageType.None;
        }

        /// <summary>
        /// Return all the types with a <see cref="MessageTypeAttribute"/>.
        /// </summary>
        public static (Type type, MessageTypeAttribute attribute)[] AllTypes { get; } =
            AttributeUtility.GetAllTypes<MessageTypeAttribute>();
    }

    /// <summary>
    /// Message sent by repeaters when they start up to announce their presence to an emitter.
    /// </summary>
    /// <remarks>This message should be sent repetitively every X ms (not too often, something over 100 ms sounds
    /// reasonable) by a starting repeater until it receives a <see cref="RepeaterRegistered"/> from the emitter.</remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [MessageType(MessageType.RegisteringWithEmitter)]
    struct RegisteringWithEmitter
    {
        /// <summary>
        /// NodeId of the repeater node announcing itself.
        /// </summary>
        public byte NodeId;
        /// <summary>
        /// Bytes forming the IP address converted to an integer using <see cref="BitConverter.ToUInt32(byte[],int)"/>
        /// to identify repeater beyond the NodeId (in case someone else on the network would be miss configured to use
        /// the same NodeId).
        /// </summary>
        /// <remarks>We could in theory use Socket.ReceiveFrom to get that information, however doing so would mean we
        /// need to do it for all messages when we in fact only need to do it for this message, so instead replicate
        /// the address inside the message to speed up the general case.</remarks>
        public uint IPAddressBytes;
    }

    /// <summary>
    /// Answer of the emitter to a repeater acknowledging its request to join the cluster.
    /// </summary>
    /// <remarks>This message could in theory be sent directly to the repeater in unicast, however this can start to
    /// cause problems when multiple repeaters are running on the same computer.  So in an effort to keep things simple
    /// this message is also sent to everybody.</remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [MessageType(MessageType.RepeaterRegistered)]
    struct RepeaterRegistered
    {
        /// <summary>
        /// NodeId of the repeater this message is intended for.
        /// </summary>
        public byte NodeId;
        /// <summary>
        /// Bytes forming the IP address converted to an integer using <see cref="BitConverter.ToUInt32(byte[],int)"/>
        /// to identify repeater beyond the NodeId (in case someone else on the network would be miss configured to use
        /// the same NodeId).
        /// </summary>
        /// <remarks>We could in theory use Socket.ReceiveFrom to get that information, however doing so would mean we
        /// need to do it for all messages when we in fact only need to do it for this message, so instead replicate
        /// the address inside the message to speed up the general case.</remarks>
        public uint IPAddressBytes;
        /// <summary>
        /// Was the repeater accepted by the emitter?
        /// </summary>
        /// <remarks>A repeater that was refused should simply abort and shouldn't try to do anything from the traffic
        /// being sent on the multicast address.</remarks>
        [MarshalAs(UnmanagedType.I1)]
        public bool Accepted;
    }

    /// <summary>
    /// Message sent to cary a part of the data of a frame from the emitter to the repeaters.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [MessageType(MessageType.FrameData)]
    struct FrameData
    {
        /// <summary>
        /// Index of the frame this message contains information about.
        /// </summary>
        public ulong FrameIndex;
        /// <summary>
        /// Size (in bytes) of all the data to be transmitted from the emitter to the repeaters for
        /// <see cref="FrameIndex"/>.
        /// </summary>
        public int DataLength;
        /// <summary>
        /// Index of the message in the sequence of datagrams necessary to send all the data to be transmitted from the
        /// emitter to the repeaters for <see cref="FrameIndex"/>.
        /// </summary>
        public int DatagramIndex;
        /// <summary>
        /// Offset of the data carried by this message (located after the last byte of this struct when sent over the
        /// network) in DatagramData within all the data to be transmitted from the emitter to the repeaters for
        /// <see cref="FrameIndex"/>.
        /// </summary>
        public int DatagramDataOffset;
    }

    /// <summary>
    /// Message sent from repeaters to emitters when they think a datagram was lost and they want it to be retransmitted.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [MessageType(MessageType.RetransmitFrameData)]
    struct RetransmitFrameData
    {
        /// <summary>
        /// Index of the frame for which we need a part of the data to be retransmitted.
        /// </summary>
        public ulong FrameIndex;
        /// <summary>
        /// Index of the first datagram to re-transmit in the sequence of datagrams for <see cref="FrameIndex"/>.
        /// </summary>
        /// <remarks>This datagram index is inclusive, it has to be retransmitted.</remarks>
        public int DatagramIndexIndexStart;
        /// <summary>
        /// Index of the datagram where to stop retransmission of the sequence of datagrams for <see cref="FrameIndex"/>.
        /// </summary>
        /// <remarks>This datagram index is exclusive, only the one before it should be re-transmitted.</remarks>
        public int DatagramIndexIndexEnd;
    }

    /// <summary>
    /// Message sent from by repeaters to signal the emitter it is ready to proceed with next frame.
    /// </summary>
    /// <remarks>The first first is always assume to use network based node synchronization, so on the first frame every
    /// repeater has to send this message to the emitter.</remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [MessageType(MessageType.RepeaterWaitingToStartFrame)]
    struct RepeaterWaitingToStartFrame
    {
        /// <summary>
        /// Index of the frame we are ready to start working on.
        /// </summary>
        public ulong FrameIndex;
        /// <summary>
        /// NodeId of the repeater node.
        /// </summary>
        public byte NodeId;
        /// <summary>
        /// Indicate to the emitter if this node will still use network sync on the next frame.  Once a repeater opted
        /// out of the network sync there is no getting back in.
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool WillUseNetworkSyncOnNextFrame;
    }

    /// <summary>
    /// Sent as a reply by the emitter to every repeaters to inform of which node still haven't signaled they are ready
    /// to proceed by sending their <see cref="RepeaterWaitingToStartFrame"/> message.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [MessageType(MessageType.EmitterWaitingToStartFrame)]
    struct EmitterWaitingToStartFrame
    {
        /// <summary>
        /// Index of the frame we are ready to start working on.
        /// </summary>
        public ulong FrameIndex;
        /// <summary>
        /// Bits 0 .. 63 of bit field where the index of set bits indicate that the emitter is still waiting for that
        /// repeater to signal it is ready.
        /// </summary>
        public ulong WaitingOn0;
        /// <summary>
        /// Bits 64 .. 127 of bit field where the index of set bits indicate that the emitter is still waiting for that
        /// repeater to signal it is ready.
        /// </summary>
        public ulong WaitingOn1;
        /// <summary>
        /// Bits 128 .. 191 of bit field where the index of set bits indicate that the emitter is still waiting for that
        /// repeater to signal it is ready.
        /// </summary>
        public ulong WaitingOn2;
        /// <summary>
        /// Bits 192 .. 255 of bit field where the index of set bits indicate that the emitter is still waiting for that
        /// repeater to signal it is ready.
        /// </summary>
        public ulong WaitingOn3;

        /// <summary>
        /// Returns (from the WaitingOn[0..3] ulong) if we are still waiting on the node with the specified identifier.
        /// </summary>
        /// <param name="nodeId">Node identifier</param>
        /// <returns>Are we still waiting on <paramref name="nodeId"/>.</returns>
        public bool IsWaitingOn(byte nodeId)
        {
            BitField64 storage;
            storage.Value = (nodeId >> 6) switch
            {
                0 => WaitingOn0,
                1 => WaitingOn1,
                2 => WaitingOn2,
                3 => WaitingOn3,
                _ => throw new IndexOutOfRangeException("Should not happen, byte >> 6 should only have 4 possible values...")
            };
            return storage.IsSet(nodeId & 0b11_1111);
        }
    }

    /// <summary>
    /// Message sent by the emitter to inform repeaters that we have been requested to quit.
    /// </summary>
    /// <remarks>This message should be sent repetitively every X ms (not too often, something over 100 ms sounds
    /// reasonable) by the emitter requesting to quit until it is forcefully terminated or all repeated have signaled
    /// they received it and are quitting.
    /// <br/><br/>There isn't really any property in this message at the moment, but let's still have it to be symetric
    /// with the other messages.</remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [MessageType(MessageType.PropagateQuit)]
    struct PropagateQuit
    {
    }

    /// <summary>
    /// Message sent by repeaters to inform the emitter that it received the <see cref="PropagateQuit"/> message was
    /// received and is quitting.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [MessageType(MessageType.QuitReceived)]
    struct QuitReceived
    {
        /// <summary>
        /// NodeId of the repeater node sending the message.
        /// </summary>
        public byte NodeId;
    }
}
