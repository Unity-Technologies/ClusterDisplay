﻿using System.Linq;
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

        public int m_RPCCount = 0;
        public int RPCCount => m_RPCCount;

        private void ApplyRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            if (!IsSerializing)
            {
                if (!RPCSerializer.TrySerializeMethodInfo(ref rpcMethodInfo, out var serializedRPC))
                    return;
                else m_SerializedRPCsContainer.SetData(rpcMethodInfo.rpcId, serializedRPC);
            }

            m_RPCs[rpcMethodInfo.rpcId] = rpcMethodInfo;
            if (!m_RPCLut.ContainsKey(rpcMethodInfo.methodInfo.MetadataToken))
                m_RPCLut.Add(rpcMethodInfo.methodInfo.MetadataToken, rpcMethodInfo.rpcId);

            m_RPCCount++;

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void RemoveRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            m_RPCs[rpcMethodInfo.rpcId] = default(RPCMethodInfo);
            m_RPCLut.Remove(rpcMethodInfo.methodInfo.MetadataToken);
            m_SerializedRPCsContainer.SetData(rpcMethodInfo.rpcId, null);
            m_IDManager.PushUnutilizedId(rpcMethodInfo.rpcId);

            m_RPCCount--;

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void RemoveRPCOnDeserialize (ushort rpcId)
        {
            m_RPCs[rpcId] = default(RPCMethodInfo);
            m_SerializedRPCsContainer.SetData(rpcId, null);
            m_IDManager.PushUnutilizedId(rpcId);
        }

        public bool TryGetRPC(ushort rpcId, out RPCMethodInfo rpcMethodInfo) => (rpcMethodInfo = m_RPCs[rpcId]).IsValid;
        public void Foreach (System.Action<RPCMethodInfo> callback)
        {
            ushort rpcIndex = 0, rpcCount = 0;
            while (rpcIndex < ushort.MaxValue)
            {
                if (!m_RPCs[rpcIndex].IsValid)
                {
                    rpcIndex++;
                    continue;
                }

                callback(m_RPCs[rpcIndex++]);
                if (++rpcCount >= m_RPCCount)
                    break;
            }
        }

        public void SetRPC(ref RPCMethodInfo rpcMethodInfo) => ApplyRPC(ref rpcMethodInfo);

        [SerializeField][HideInInspector] private IDManager m_IDManager = new IDManager();
        [SerializeField][HideInInspector] private SerializedRPCsContainer m_SerializedRPCsContainer = new SerializedRPCsContainer();

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
            m_RPCLut.Clear();
            m_RPCCount = 0;

            List<ushort> rpcIdsToAdd = new List<ushort>();
            m_SerializedRPCsContainer.Foreach((serializedRPC) =>
            {
                Debug.Log($"Deserialized RPC Execution Stage: {serializedRPC.rpcExecutionStage}");
                if (!RPCSerializer.TryDeserializeMethodInfo(serializedRPC, out var rpcExecutionStage, out var methodInfo))
                {
                    Debug.LogError($"Unable to deserialize method: \"{serializedRPC.methodName}\", declared in type: \"{serializedRPC.declaryingTypeFullName}\".");
                    RemoveRPCOnDeserialize(serializedRPC.rpcId);
                    return;
                }

                var deserializedRPCMethodInfo = new RPCMethodInfo(serializedRPC.rpcId, rpcExecutionStage, methodInfo);
                ApplyRPC(ref deserializedRPCMethodInfo);
                rpcIdsToAdd.Add(serializedRPC.rpcId);

                Debug.Log($"Successfully deserialized method: \"{serializedRPC.methodName}\", declared in type: \"{serializedRPC.declaryingTypeFullName}\".");
            });

            var methodsWithRPCAttribute = ReflectionUtils.GetAllMethodsWithAttribute<RPCMethod>();
            foreach (var methodInfo in methodsWithRPCAttribute)
            {
                var rpcMethodAttribute = methodInfo.GetCustomAttribute<RPCMethod>();
                rpcIdsToAdd.Add(rpcMethodAttribute.rpcId);

                var rpcMethodInfo = new RPCMethodInfo(rpcMethodAttribute.rpcId, rpcMethodAttribute.rpcExecutionStage, methodInfo);
                ApplyRPC(ref rpcMethodInfo);

                Debug.Log($"Registered RPC with {nameof(RPCMethod)} attribute for method: \"{methodInfo.Name}\" with RPC Execution Stage: \"{rpcMethodAttribute.rpcExecutionStage}\" and RPC ID: \"{rpcMethodAttribute.rpcId}\".");
            }

            rpcIdsToAdd.Sort();
            ushort largestId = rpcIdsToAdd.Last();
            m_IDManager.PushSetOfIds(rpcIdsToAdd.ToArray(), largestId);
        }

        private void Serialize ()
        {
            if (!m_IsDirty)
                return;

            List<SerializedRPC> list = new List<SerializedRPC>(m_SerializedRPCsContainer.Count);
            m_SerializedRPCsContainer.Foreach((serializedRPC) => list.Add(serializedRPC));
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
