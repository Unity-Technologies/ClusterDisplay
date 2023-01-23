using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.RPC.Runtime")]

namespace Unity.ClusterDisplay
{
    class EmitterStateWriter : IDisposable
    {
        bool m_Disposed;

        FrameDataBuffer m_StagedFrameData = new();
        FrameDataBuffer m_CurrentFrameData = new();
        bool m_CurrentFrameDataValid;
        bool m_StagedFrameDataValid;

        /// <summary>
        /// Engine state data gets collected on each frame but may be published on a delay.
        /// </summary>
        static readonly (int, FrameDataBuffer.StoreDataDelegate)[] k_StateDataDelegates =
        {
            ((int)StateID.Time, ClusterSerialization.SaveTimeManagerState),
            ((int)StateID.Random, SaveRandomState)
        };

        /// <summary>
        /// Overrides k_StateDataDelegates when performing unit tests.
        /// </summary>
        static (int, FrameDataBuffer.StoreDataDelegate)[] s_StateDataDelegatesOverride;

        /// <summary>
        /// Custom data gets published on the same frame that delegates are invoked.
        /// </summary>
        static readonly Dictionary<int, List<FrameDataBuffer.StoreDataDelegate>> k_CustomDataDelegates = new();

        readonly bool k_RepeatersDelayed;

        internal static void RegisterOnStoreCustomDataDelegate(int id, FrameDataBuffer.StoreDataDelegate storeDataDelegate)
        {
            if (k_CustomDataDelegates.TryGetValue(id, out var list))
            {
                list.Add(storeDataDelegate);
            }
            else
            {
                k_CustomDataDelegates.Add(id, new List<FrameDataBuffer.StoreDataDelegate>{storeDataDelegate});
            }
        }

        internal static void UnregisterCustomDataDelegate(int id, FrameDataBuffer.StoreDataDelegate storeDataDelegate)
        {
            if (k_CustomDataDelegates.TryGetValue(id, out var list))
            {
                list.Remove(storeDataDelegate);
            }
        }

        internal static void ClearCustomDataDelegates() => k_CustomDataDelegates.Clear();

        internal static void OverrideStateDelegates((int, FrameDataBuffer.StoreDataDelegate)[] delegates)
        {
            s_StateDataDelegatesOverride = delegates;
        }

        internal static void ClearStateDelegatesOverride()
        {
            s_StateDataDelegatesOverride = null;
        }

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

        public void PublishCurrentState(ulong currentFrameId, FrameDataSplitter frameDataSplitter)
        {
            if (!m_StagedFrameDataValid)
            {
                return;
            }

            frameDataSplitter.SendFrameData(currentFrameId, ref m_StagedFrameData);

            // We must stop using m_StagedFrameData as frameDataSplitter is now "owning it" (so that he can re-use it
            // to repeat lost datagrams).  So recycle an old one or if none are available allocate a new one.
            m_StagedFrameData = frameDataSplitter.FrameDataBufferPool != null ?
                frameDataSplitter.FrameDataBufferPool.Get() : new FrameDataBuffer();

            m_StagedFrameDataValid = false;
        }

        static void StoreStateData(FrameDataBuffer frameDataBuffer)
        {
            foreach (var (id, dataDelegate) in s_StateDataDelegatesOverride ?? k_StateDataDelegates)
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
                    foreach (var (id, customDataDelegateList) in k_CustomDataDelegates)
                    {
                        foreach (var storeDataDelegate in customDataDelegateList)
                        {
                            m_StagedFrameData.Store(id, storeDataDelegate);
                        }
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
