using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public partial class RPCILPostProcessor
    {
        private static class UniqueRPCIdManager
        {
            private const string RPCIDsCachePath = "./Temp/ClusterDisplay/RPCIDCache";
            private static FileStream fileStream;

            private readonly static List<ushort> usedRPCIds = new List<ushort>();
            private readonly static Queue<ushort> unusedRPCIds = new Queue<ushort>();

            private static SharedMutex mutex = new SharedMutex("RPCIDCacheMutex");

            public static int Count => usedRPCIds.Count;
            private static int lastRPCId = -1;

            [UnityEditor.Callbacks.DidReloadScripts]
            private static void OnScriptsReloaded() 
            {
                try
                {
                    mutex.Lock();
                    if (File.Exists(RPCIDsCachePath))
                        File.Delete(RPCIDsCachePath);
                    mutex.Release();

                }
                
                catch (System.Exception e)
                {
                    LogWriter.LogException(e);
                }
            }

            public static ushort GetUnused ()
            {
                Open();
                Poll();
                var newRPCId = unusedRPCIds.Count > 0 ? unusedRPCIds.Dequeue() : (ushort)++lastRPCId;
                WriteUse(newRPCId);
                Close();
                
                return newRPCId;
            }

            private static void WriteUse(ushort rpcId)
            {
                fileStream.Seek(0, SeekOrigin.End);

                fileStream.WriteByte((byte)rpcId);
                fileStream.WriteByte((byte)(rpcId >> 8));
            }

            public static void Use (ushort rpcId)
            {
                Open();
                WriteUse(rpcId);
                Close();
            }

            public static bool InUse(ushort rpcId)
            {
                Open();
                Poll();
                Close();
                return usedRPCIds.Contains(rpcId);
            }
            
            private static void Poll ()
            {
                usedRPCIds.Clear();
                unusedRPCIds.Clear();
                
                byte[] bytes = new byte[2];
                
                while (fileStream.Position < fileStream.Length)
                {
                    bytes[0] = (byte)fileStream.ReadByte();
                    bytes[1] = (byte)fileStream.ReadByte();

                    ushort rpcId = BitConverter.ToUInt16(bytes, 0);
                    if (!usedRPCIds.Contains(rpcId))
                        usedRPCIds.Add(rpcId);
                }
                
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
            
            private static void Open ()
            {
                mutex.Lock();
                if (fileStream != null)
                    return;

                var folder = Path.GetDirectoryName(RPCIDsCachePath);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                fileStream = new FileStream(RPCIDsCachePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            }

            private static void Close()
            {
                if (fileStream != null)
                {
                    fileStream.Close();
                    fileStream.Dispose();
                    fileStream = null;
                }

                mutex.Release();
            }
        }
    }
}
