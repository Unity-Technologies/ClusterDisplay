using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay
{
    public struct RPCMethodInfo
    {
        public readonly ushort rpcId;
        public readonly MethodInfo methodInfo;
        public bool IsValid => methodInfo != null;
        public bool IsStatic => methodInfo != null ? methodInfo.IsStatic : false;


        public ushort instanceCount;

        public RPCMethodInfo (ushort _id, MethodInfo _methodInfo)
        {
            rpcId = _id;
            methodInfo = _methodInfo;
            instanceCount = 1;
        }
    }

    [CreateAssetMenu(fileName = "RPCRegistry", menuName = "Cluster Display/RPC Registry")]
    public partial class RPCRegistry : SingletonScriptableObject<RPCRegistry>, ISerializationCallbackReceiver
    {
        private readonly Dictionary<int, ushort> rpcLut = new Dictionary<int, ushort>();
        private readonly RPCMethodInfo[] rpcs = new RPCMethodInfo[ushort.MaxValue];
        public RPCMethodInfo this[ushort rpcId]
        {
            get => rpcs[rpcId];
            private set => rpcs[rpcId] = value;
        }

        public RPCMethodInfo GetRPCByIndex (ushort rpcIndex) => rpcs[idManager[rpcIndex].id];
        public ushort RPCCount => idManager.SerializedIdCount;
        public ushort RPCUpperBoundID => idManager.UpperBoundID;

        [SerializeField][HideInInspector] private IDManager<string> idManager = new IDManager<string>();

        private bool isDirty = false;

        private static string cachedRPCStubsPath = null;
        public static string RPCStubsPath
        {
            get
            {
                if (string.IsNullOrEmpty(cachedRPCStubsPath))
                    cachedRPCStubsPath = $"./RPCStubs.txt";
                return cachedRPCStubsPath;
            }
        }

        public bool TryIncrementMethodReference (System.Type type, MethodInfo methodInfo, out RPCMethodInfo rpcMethodInfo)
        {
            if (rpcLut.TryGetValue(methodInfo.MetadataToken, out var id))
            {
                rpcMethodInfo = this[id];
                rpcMethodInfo.instanceCount++;
                this[id] = rpcMethodInfo;
                return true;
            }

            if(!methodInfo.GetParameters().All(paramterInfo => paramterInfo.ParameterType.IsValueType))
                throw new System.Exception($"Unable to register method: \"{methodInfo.Name}\" as an RPC, one or more of the parameters is not a value type!");

            if (!idManager.TryPopId(out id))
            {
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            var newRpc = new RPCMethodInfo(id, methodInfo);
            if (!RPCSerializer.TrySerializeMethodInfo(ref newRpc, out var methodString))
            {
                rpcMethodInfo = default(RPCMethodInfo);
                idManager.PushId(id);
                return false;
            }

            rpcs[id] = rpcMethodInfo = newRpc;
            idManager.SetData(id, methodString);
            rpcLut.Add(methodInfo.MetadataToken, id);
            isDirty = true;

            Debug.Log($"Registered method with UUID: \"{id}\" for method: \"{ReflectionUtils.GetMethodSignature(methodInfo)}\" from type: \"{type.FullName}\" from assembly: \"{type.Assembly}\".");

            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif

            return true;
        }

        public void DeincrementMethodReference (ushort rpcId)
        {
            var rpcMethodInfo = this[rpcId];
            rpcMethodInfo.instanceCount--;

            if (rpcMethodInfo.instanceCount == 0)
            {
                rpcs[rpcId] = default(RPCMethodInfo);
                rpcLut.Remove(rpcMethodInfo.methodInfo.MetadataToken);
                idManager.PushId(rpcId);
                idManager.SetData(rpcId, null);
            }

            else this[rpcId] = rpcMethodInfo;
            isDirty = true;

            Debug.Log($"Unregistered method: \"{rpcMethodInfo.methodInfo.Name}\" with UUID: \"{rpcMethodInfo.rpcId}\".");

            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
        }

        public void Clear ()
        {
            idManager.Reset();
            rpcLut.Clear();
            isDirty = true;

            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
        }

        public void OnAfterDeserialize()
        {
            if (!idManager.HasSerializedData)
                return;

            for (ushort i = 0; i < idManager.SerializedIdCount; i++)
            {
                (ushort rpcId, string serializedRpcMethodInfo) = idManager[i];
                if (!RPCSerializer.TryDeserializeMethodInfo(serializedRpcMethodInfo, out var methodInfo))
                {
                    Debug.LogError($"Unable to deserialize MethodInfo: \"{serializedRpcMethodInfo}\".");
                    idManager.PushId(rpcId);
                    continue;
                }

                Debug.Log($"Successfully deserialize MethodInfo: \"{serializedRpcMethodInfo}\".");

                rpcs[rpcId] = new RPCMethodInfo(rpcId, methodInfo);
                rpcLut.Add(methodInfo.MetadataToken, rpcId);
            }
        }

        public void OnBeforeSerialize() 
        {
            if (!isDirty)
                return;

            RPCSerializer.TryWriteRPCStubs(RPCStubsPath, idManager.SerializedData);
            isDirty = false;
        }
    }
}
