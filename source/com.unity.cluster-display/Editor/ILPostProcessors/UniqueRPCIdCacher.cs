using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public partial class RPCILPostProcessor
    {
        private static class UniqueRPCIdManager
        {
            private const string RPCIDsCachePath = "./Temp/ClusterDisplay/RPCIDCache";
            private static FileStream fileStream;

            private readonly static List<ushort> usedRPCIds = new List<ushort>();
            private readonly static Queue<ushort> unusedRPCIds = new Queue<ushort>();

            public static int Count => usedRPCIds.Count;
            private static int lastRPCId = -1;

            [UnityEditor.Callbacks.DidReloadScripts]
            private static void OnScriptsReloaded() 
            {
                try
                {
                    if (File.Exists(RPCIDsCachePath))
                    {
                        File.Delete(RPCIDsCachePath);
                        // Debug.Log($"Deleted RPC ID cache file at path: \"{RPCIDsCachePath}\".");
                    }

                    var folder = Path.GetDirectoryName(RPCIDsCachePath);
                    if (Directory.Exists(folder))
                        if (Directory.GetFiles(folder).Length == 0)
                            Directory.Delete(folder);

                } catch (System.Exception e)
                {
                    Debug.LogException(e);
                    return;
                }
            }

            public static ushort Get()
            {
                var newRPCId = unusedRPCIds.Count > 0 ? unusedRPCIds.Dequeue() : (ushort)++lastRPCId;
                Add(newRPCId);
                return newRPCId;
            }

            public static void Add (ushort rpcId)
            {
                usedRPCIds.Add(rpcId);
                CreateFIleStream();

                fileStream.WriteByte((byte)rpcId);
                fileStream.WriteByte((byte)(rpcId >> 8));
            }

            public static void PollUnused ()
            {
                if (usedRPCIds.Count > 0)
                {
                    usedRPCIds.Sort();
                    lastRPCId = usedRPCIds.Last();
                    for (ushort rpcId = 0; rpcId < lastRPCId; rpcId++)
                    {
                        if (usedRPCIds.Contains(rpcId))
                            continue;
                        unusedRPCIds.Enqueue(rpcId);
                    }
                }
            }

            public static void Read ()
            {
                byte[] bytes = new byte[2];
                CreateFIleStream();

                while (fileStream.Position < fileStream.Length)
                {
                    bytes[0] = (byte)fileStream.ReadByte();
                    bytes[1] = (byte)fileStream.ReadByte();
                    usedRPCIds.Add(BitConverter.ToUInt16(bytes, 0));
                }
            }

            private static void CreateFIleStream ()
            {
                if (fileStream != null)
                    return;

                var folder = Path.GetDirectoryName(RPCIDsCachePath);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                fileStream = new FileStream(RPCIDsCachePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }

            public static void Close()
            {
                if (fileStream != null)
                {
                    fileStream.Close();
                    fileStream = null;
                }

                usedRPCIds.Clear();
                unusedRPCIds.Clear();
            }
        }
    }
}
