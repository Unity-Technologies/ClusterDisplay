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
        public RPCExecutionStage rpcExecutionStage;

        public ushort instanceCount;

        public bool IsValid => methodInfo != null;
        public bool IsStatic => methodInfo != null ? methodInfo.IsStatic : false;

        public RPCMethodInfo (ushort rpcId, RPCExecutionStage rpcExecutionStage, MethodInfo methodInfo)
        {
            this.rpcExecutionStage = rpcExecutionStage;
            this.rpcId = rpcId;
            this.methodInfo = methodInfo;
            this.instanceCount = 1;
        }
    }

    [CreateAssetMenu(fileName = "RPCRegistry", menuName = "Cluster Display/RPC Registry")]
    public partial class RPCRegistry : SingletonScriptableObject<RPCRegistry>, ISerializationCallbackReceiver
    {
        private readonly Dictionary<int, ushort> m_RPCLut = new Dictionary<int, ushort>();
        private readonly RPCMethodInfo[] m_RPCs = new RPCMethodInfo[ushort.MaxValue];

        private void ApplyRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            if (!IsSerializing)
            {
                if (!RPCSerializer.TrySerializeMethodInfo(ref rpcMethodInfo, out var serializedRPC))
                    return;
                else m_IDManager.SetData(rpcMethodInfo.rpcId, serializedRPC);
            }

            m_RPCs[rpcMethodInfo.rpcId] = rpcMethodInfo;

            if (!m_RPCLut.ContainsKey(rpcMethodInfo.methodInfo.MetadataToken))
                m_RPCLut.Add(rpcMethodInfo.methodInfo.MetadataToken, rpcMethodInfo.rpcId);

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void RemoveRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            m_RPCs[rpcMethodInfo.rpcId] = default(RPCMethodInfo);
            m_RPCLut.Remove(rpcMethodInfo.methodInfo.MetadataToken);
            m_IDManager.SetData(rpcMethodInfo.rpcId, null);
            m_IDManager.PushId(rpcMethodInfo.rpcId);

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void RemoveRPCOnDeserialize (ushort rpcId)
        {
            m_RPCs[rpcId] = default(RPCMethodInfo);
            m_IDManager.SetData(rpcId, null);
            m_IDManager.PushId(rpcId);
        }

        public bool TryGetRPC(ushort rpcId, out RPCMethodInfo rpcMethodInfo) => (rpcMethodInfo = m_RPCs[rpcId]).IsValid;
        public bool TryGetRPCByIndex (ushort rpcIndex, out RPCMethodInfo rpcMethodInfo) => (rpcMethodInfo = m_RPCs[m_IDManager.GetDataByIndex(rpcIndex).rpcId]).IsValid;

        public void SetRPC(ref RPCMethodInfo rpcMethodInfo) => ApplyRPC(ref rpcMethodInfo);

        public ushort RPCCount => m_IDManager.SerializedIdCount;
        public ushort RPCUpperBoundID => m_IDManager.UpperBoundID;

        [SerializeField][HideInInspector] private IDManager<SerializedRPC> m_IDManager = new IDManager<SerializedRPC>();

        private static string cachedRPCStubsPath = null;
        public static string RPCStubsPath
        {
            get
            {
                if (string.IsNullOrEmpty(cachedRPCStubsPath))
                    cachedRPCStubsPath = $"./RPCStubs.json";
                return cachedRPCStubsPath;
            }
        }

        public bool TryAddNewRPC (System.Type type, MethodInfo methodInfo, RPCExecutionStage rpcExecutionStage, out RPCMethodInfo rpcMethodInfo)
        {

            if (m_RPCLut.TryGetValue(methodInfo.MetadataToken, out var rpcId))
            {
                Debug.LogError($"Cannot add RPC method: \"{methodInfo.Name}\" from declaring type: \"{methodInfo.DeclaringType}\", it has already been registered!");
                rpcMethodInfo = default(RPCMethodInfo);
                /*
                if (!TryGetRPC(rpcId, out rpcMethodInfo))
                    return false;

                rpcMethodInfo.instanceCount++;
                ApplyRPC(ref rpcMethodInfo);
                */
                return true;
            }

            if(!methodInfo.GetParameters().All(paramterInfo => paramterInfo.ParameterType.IsValueType))
            {
                Debug.LogError($"Unable to register method: \"{methodInfo.Name}\" as an RPC, one or more of the parameters is not a value type!");
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            if (!m_IDManager.TryPopId(out rpcId))
            {
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            rpcMethodInfo = new RPCMethodInfo(rpcId, rpcExecutionStage, methodInfo);
            ApplyRPC(ref rpcMethodInfo);

            Debug.Log($"Registered method with UUID: \"{rpcId}\" for method: \"{ReflectionUtils.GetMethodSignature(methodInfo)}\" from type: \"{type.FullName}\" from assembly: \"{type.Assembly}\".");

            return true;
        }

        public void RemoveRPC (ushort rpcId)
        {
            if (!TryGetRPC(rpcId, out var rpcMethodInfo))
                return;

            RemoveRPC(ref rpcMethodInfo);
            /*
            rpcMethodInfo.instanceCount--;

            if (rpcMethodInfo.instanceCount == 0)
                RemoveRPC(ref rpcMethodInfo);
            else ApplyRPC(ref rpcMethodInfo);
            */

            Debug.Log($"Unregistered method: \"{rpcMethodInfo.methodInfo.Name}\" with UUID: \"{rpcMethodInfo.rpcId}\".");
        }

        private void SetDirtyAndRecompile ()
        {
            if (IsSerializing)
                return;

            Debug.Log($"Changed RPC Registry, recompiling...");
            EditorUtility.SetDirty(this);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            m_IsDirty = true;
        }

        public void Clear ()
        {
            m_IDManager.Clear();
            m_RPCLut.Clear();

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void Deserialize ()
        {
            if (!m_IDManager.HasSerializedData)
                return;

            for (ushort rpcIndex = 0; rpcIndex < m_IDManager.SerializedIdCount; rpcIndex++)
            {
                var serializedRpcMethodInfo = m_IDManager.GetDataByIndex(rpcIndex);
                var rpcId = serializedRpcMethodInfo.rpcId;

                Debug.Log($"Deserialized RPC Execution Stage: {serializedRpcMethodInfo.rpcExecutionStage}");
                if (!RPCSerializer.TryDeserializeMethodInfo(serializedRpcMethodInfo, out var rpcExecutionStage, out var methodInfo))
                {
                    Debug.LogError($"Unable to deserialize method: \"{serializedRpcMethodInfo.methodName}\", declared in type: \"{serializedRpcMethodInfo.declaryingTypeFullName}\".");
                    RemoveRPCOnDeserialize(rpcId);
                    continue;
                }

                /*
                if (!TryIncrementMethodReference(
                    methodInfo.DeclaringType, 
                    methodInfo, 
                    rpcExecutionStage, 
                    out var _))
                {
                    RemoveRPCOnDeserialize(rpcId);
                    continue;
                }
                */

                var deserializedRPCMethodInfo = new RPCMethodInfo(rpcId, rpcExecutionStage, methodInfo);
                ApplyRPC(ref deserializedRPCMethodInfo);

                Debug.Log($"Successfully deserialized method: \"{serializedRpcMethodInfo.methodName}\", declared in type: \"{serializedRpcMethodInfo.declaryingTypeFullName}\".");
            }
        }

        private void Serialize ()
        {
            if (!m_IsDirty)
                return;

            List<SerializedRPC> list = new List<SerializedRPC>(m_IDManager.SerializedIdCount);
            for (ushort i = 0; i < m_IDManager.UpperBoundID; i++)
                list.Add(m_IDManager.GetDataByIndex(i));

            RPCSerializer.TryWriteRPCStubs(RPCStubsPath, list.ToArray());
            m_IsDirty = false;
        }

        private bool m_IsSerializing = false;
        private bool m_IsDirty = false;

        public bool IsSerializing => m_IsSerializing;
        private void ToggleSerializationState(bool isSerializing) => this.m_IsSerializing = isSerializing;

        public void OnBeforeSerialize()
        {
            ToggleSerializationState(true);
            Serialize();
            ToggleSerializationState(false);
        }

        public void OnAfterDeserialize()
        {
            ToggleSerializationState(true);
            Deserialize();
            ToggleSerializationState(false);
        }
    }
}
