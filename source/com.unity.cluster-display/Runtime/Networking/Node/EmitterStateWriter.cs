using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using buint = System.UInt32;

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.RPC.Runtime")]

namespace Unity.ClusterDisplay
{
    internal class EmitterStateWriter : IDisposable
    {
        private const int k_MaxFrameNetworkByteBufferSize = ushort.MaxValue;

        bool m_Disposed;

        FrameDataBuffer m_StagedFrameData = new(k_MaxFrameNetworkByteBufferSize);
        FrameDataBuffer m_CurrentFrameData = new(k_MaxFrameNetworkByteBufferSize);
        bool m_CurrentFrameDataValid;
        bool m_StagedFrameDataValid;

        private byte[] m_MsgBuffer = Array.Empty<byte>();

        /// <summary>
        /// Engine state data gets collected on each frame but may be published on a delay.
        /// </summary>
        static readonly (byte, FrameDataBuffer.StoreDataDelegate)[] k_StateDataDelegates =
        {
            ((byte)StateID.Time, ClusterSerialization.SaveTimeManagerState),
            ((byte)StateID.Input, ClusterSerialization.SaveInputManagerState),
            ((byte)StateID.Random, SaveRandomState),
        };

        /// <summary>
        /// Custom data gets published on the same frame that delegates are invoked.
        /// </summary>
        static readonly Dictionary<byte, FrameDataBuffer.StoreDataDelegate> k_CustomDataDelegates = new();

        private readonly bool k_RepeatersDelayed;

        internal static void RegisterOnStoreCustomDataDelegate(byte id, FrameDataBuffer.StoreDataDelegate storeDataDelegate)
        {
            k_CustomDataDelegates[id] = storeDataDelegate;
        }

        internal static void UnregisterCustomDataDelegate(byte id) => k_CustomDataDelegates.Remove(id);
        internal static void ClearCustomDataDelegates() => k_CustomDataDelegates.Clear();

        public EmitterStateWriter(bool repeatersDelayed)
        {
            k_RepeatersDelayed = repeatersDelayed;
        }

        void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed objects here
            }

            // NativeArrays look like regular IDisposable objects but they
            // behave more like unmanaged objects
            m_StagedFrameData.Dispose();
            m_CurrentFrameData.Dispose();
            m_Disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~EmitterStateWriter()
        {
            Dispose(false);
        }

        public void PublishCurrentState(UDPAgent agent, ulong currentFrameId)
        {
            if (!m_StagedFrameDataValid)
            {
                return;
            }

            var mainMessageSize = Marshal.SizeOf<MessageHeader>() + Marshal.SizeOf<EmitterLastFrameData>();
            var len = mainMessageSize + m_StagedFrameData.Length;

            if (m_MsgBuffer.Length < len)
                m_MsgBuffer = new byte[len];

            var msg = new EmitterLastFrameData() {FrameNumber = currentFrameId };
            msg.StoreInBuffer(m_MsgBuffer, Marshal.SizeOf<MessageHeader>()); // Leaver room for header

            // Copy state + custom data
            m_StagedFrameData.CopyTo(m_MsgBuffer, mainMessageSize);

            var msgHdr = new MessageHeader()
            {
                MessageType = EMessageType.LastFrameData,
                Flags = MessageHeader.EFlag.Broadcast,
                PayloadSize = (UInt16)m_StagedFrameData.Length
            };

            agent.PublishMessage(msgHdr, m_MsgBuffer);
        }

        internal static void StoreStateData(FrameDataBuffer frameDataBuffer)
        {
            foreach (var (id, dataDelegate) in k_StateDataDelegates)
            {
                frameDataBuffer.Store(id, dataDelegate);
            }
        }

        internal void GatherPreFrameState()
        {
            try
            {
                m_CurrentFrameData.Clear();
                StoreStateData(m_CurrentFrameData);
                m_CurrentFrameDataValid = true;
            }
            catch (Exception e)
            {
                m_CurrentFrameDataValid = false;
                ClusterDebug.Log(e.Message);
            }
        }

        void StageCurrentStateBuffer()
        {
            if (m_CurrentFrameDataValid)
            {
                Swap(ref m_CurrentFrameData, ref m_StagedFrameData);
                m_CurrentFrameDataValid = false;

                try
                {
                    foreach (var (id, customDataDelegate) in k_CustomDataDelegates)
                    {
                        m_StagedFrameData.Store(id, customDataDelegate);
                    }

                    m_StagedFrameData.Store((byte)StateID.End);
                    m_StagedFrameDataValid = true;
                }
                catch (Exception e)
                {
                    ClusterDebug.Log(e.Message);
                    m_StagedFrameDataValid = false;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Swap<T>(ref T a, ref T b)
        {
            (a, b) = (b, a);
        }

        static int SaveRandomState(NativeArray<byte> arr)
        {
            var state = UnityEngine.Random.state;
            return state.StoreInBuffer(arr);
        }

        public void GatherFrameState()
        {
            if (!k_RepeatersDelayed)
            {
                GatherPreFrameState();
                StageCurrentStateBuffer();

                return;
            }

            StageCurrentStateBuffer();
            GatherPreFrameState();
        }
    }
}
