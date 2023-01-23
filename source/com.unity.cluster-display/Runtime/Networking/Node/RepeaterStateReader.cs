using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.RPC")]

namespace Unity.ClusterDisplay
{
    static class RepeaterStateReader
    {
        internal delegate bool OnLoadCustomData(NativeArray<byte> stateData);

        static readonly Dictionary<int, OnLoadCustomData> k_BuiltInOnLoadDelegates = new()
        {
            {(int)StateID.Time, ClusterSerialization.RestoreTimeManagerState},
            {(int)StateID.Random, RestoreRndGeneratorState}
        };

        static readonly Dictionary<int, List<OnLoadCustomData>> k_LoadDataDelegates = k_BuiltInOnLoadDelegates.ToDictionary(
            entry => entry.Key,
            entry => new List<OnLoadCustomData>{entry.Value});

        internal static void RegisterOnLoadDataDelegate(int id, OnLoadCustomData onLoadData)
        {
            if (k_LoadDataDelegates.TryGetValue(id, out var list))
            {
                list.Add(onLoadData);
            }
            else
            {
                k_LoadDataDelegates.Add(id, new List<OnLoadCustomData>{onLoadData});
            }
        }

        internal static void UnregisterOnLoadDataDelegate(int id, OnLoadCustomData onLoadData)
        {
            if (k_LoadDataDelegates.TryGetValue(id, out var list))
            {
                list.Remove(onLoadData);
            }
        }

        internal static void ClearOnLoadDataDelegates()
        {
            k_LoadDataDelegates.Clear();
            // Built-in delegates are always registered
            foreach (var (id, func) in k_BuiltInOnLoadDelegates)
            {
                RegisterOnLoadDataDelegate(id, func);
            }
        }

        /// <summary>
        /// Restore emitter's game state to the current repeater's game state.
        /// </summary>
        /// <param name="nativeArray">Serialized game state.</param>
        public static void RestoreEmitterFrameData(NativeArray<byte> nativeArray)
        {
            foreach (var (id, data) in new FrameDataReader(nativeArray))
            {
                // The built-in delegates restore the states of various subsystems
                if (k_LoadDataDelegates.TryGetValue(id, out var list))
                {
                    foreach (var onLoadCustomData in list)
                    {
                        onLoadCustomData.Invoke(data);
                    }
                }
            }
        }

        static unsafe bool RestoreRndGeneratorState(NativeArray<byte> stateData)
        {
            UnityEngine.Random.State rndState = default;
            var rawData = (byte*)&rndState;

            ClusterDebug.Assert(*((UInt64*)stateData.GetUnsafePtr() + 0) != 0 && *((UInt64*)stateData.GetUnsafePtr() + 0) != 0, "invalid rnd state being restored.");

            UnsafeUtility.MemCpy(rawData, (byte*)stateData.GetUnsafePtr(), Marshal.SizeOf<UnityEngine.Random.State>());

            UnityEngine.Random.state = rndState;

            // RuntimeLogWriter.Log($"Seed: {UnityEngine.Random.seed}");
            return true;
        }
    }
}
