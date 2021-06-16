using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Scripting;

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
    [InitializeOnLoad]
    public partial class RPCRegistry : SingletonScriptableObject<RPCRegistry>, ISerializationCallbackReceiver
    {
        private readonly Dictionary<int, ushort> m_RPCLut = new Dictionary<int, ushort>();
        private readonly Dictionary<ushort, RPCMethodInfo> m_RPCs = new Dictionary<ushort, RPCMethodInfo>();

        public bool IsValidRPCId(ushort rpcId) => m_RPCs.ContainsKey(rpcId);

        public int RPCCount => m_RPCs.Count;

        private void ApplyRPC (ref RPCMethodInfo rpcMethodInfo, bool serialize = true)
        {
            if (serialize)
            {
                if (!RPCSerializer.TryCreateSerializableRPC(ref rpcMethodInfo, out var serializedRPC))
                    return;
                else m_SerializedRPCsContainer.SetData(rpcMethodInfo.rpcId, serializedRPC);
            }

            if (!m_RPCs.ContainsKey(rpcMethodInfo.rpcId))
                m_RPCs.Add(rpcMethodInfo.rpcId, rpcMethodInfo);
            else m_RPCs[rpcMethodInfo.rpcId] = rpcMethodInfo;

            if (!m_RPCLut.ContainsKey(rpcMethodInfo.methodInfo.MetadataToken))
                m_RPCLut.Add(rpcMethodInfo.methodInfo.MetadataToken, rpcMethodInfo.rpcId);
            else m_RPCLut[rpcMethodInfo.methodInfo.MetadataToken] = rpcMethodInfo.rpcId;

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void RemoveRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            m_RPCs.Remove(rpcMethodInfo.rpcId);
            m_RPCLut.Remove(rpcMethodInfo.methodInfo.MetadataToken);
            m_SerializedRPCsContainer.SetData(rpcMethodInfo.rpcId, null);
            m_IDManager.PushUnutilizedId(rpcMethodInfo.rpcId);

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

        public bool TryGetRPC(ushort rpcId, out RPCMethodInfo rpcMethodInfo) => m_RPCs.TryGetValue(rpcId, out rpcMethodInfo);
        public void Foreach (System.Action<RPCMethodInfo> callback)
        {
            var keys = m_RPCs.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
                callback(m_RPCs[keys[i]]);
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
            if (!ReflectionUtils.DetermineIfMethodIsRPCCompatible(methodInfo))
            {
                Debug.LogError($"Unable to register method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType}\", one or more of the method's parameters is not a value type or one of the parameter's members is not a value type.");
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            if (m_RPCLut.TryGetValue(methodInfo.MetadataToken, out var rpcId))
            {
                Debug.LogError($"Cannot add RPC method: \"{methodInfo.Name}\" from declaring type: \"{methodInfo.DeclaringType}\", it has already been registered!");
                rpcMethodInfo = default(RPCMethodInfo);
                return true;
            }

            if (!m_IDManager.TryPopId(out rpcId))
            {
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            rpcMethodInfo = new RPCMethodInfo(rpcId, rpcExecutionStage, methodInfo);
            ApplyRPC(ref rpcMethodInfo);

            // Debug.Log($"Registered method with UUID: \"{rpcId}\" for method: \"{ReflectionUtils.GetMethodSignature(methodInfo)}\" from type: \"{type.FullName}\" from assembly: \"{type.Assembly}\".");

            return true;
        }

        public void RemoveRPC (ushort rpcId)
        {
            if (!TryGetRPC(rpcId, out var rpcMethodInfo))
                return;
            RemoveRPC(ref rpcMethodInfo);

            // Debug.Log($"Unregistered method: \"{rpcMethodInfo.methodInfo.Name}\" with UUID: \"{rpcMethodInfo.rpcId}\".");
        }

        private void SetDirtyAndRecompile ()
        {
            #if UNITY_EDITOR
            if (IsSerializing)
                return;

            // Debug.Log($"Changed RPC Registry, recompiling...");
            EditorUtility.SetDirty(this);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            m_IsDirty = true;
            #endif
        }

        public void Clear ()
        {
            m_RPCLut.Clear();
            m_RPCs.Clear();
            m_SerializedRPCsContainer.Clear();
            m_IDManager.Clear();

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void WriteRPCStubs ()
        {
            List<SerializedRPC> list = new List<SerializedRPC>(m_SerializedRPCsContainer.Count == 0 ? 10 : m_SerializedRPCsContainer.Count);
            m_SerializedRPCsContainer.Foreach((serializedRPC) => list.Add(serializedRPC));
            RPCSerializer.TryWriteRPCStubs(RPCStubsPath, list.ToArray());
        }

        private void Deserialize ()
        {
            // Unfortunately, there seems to be a werid state where:
            // 1. Managed debugging is turned on in the editor.
            // 2. You reboot the editor.
            // 4. Managed debugging will turn off.
            // 5. Reflection will not be able to retrieve the Assembly-CSharp assembly, so deserialization of methods will fail.
            // 6. Deserialize seems to be called again after Assembly-CSharp is recompiled and reflection on it will work again.
            // The order of when Deserialize() is called in between state change of ManagedDebugging.isDebug is unclear, but
            // something on the lines of this seems to be happening. Therefore, if we simply check whether the assembly exists
            // we can just not deserialize anything and wait until the assembly becomes available.
            if (!ReflectionUtils.TryGetDefaultAssembly(out var _, logError: false))
                return;

            List<ushort> rpcIdsToAdd = new List<ushort>();
            List<string> renamedRPCs = new List<string>();

            if (ReflectionUtils.TryGetAllMethodsWithAttribute<RPC>(out var methodsWithRPCAttribute))
            {
                foreach (var methodInfo in methodsWithRPCAttribute)
                {
                    var rpcMethodAttribute = methodInfo.GetCustomAttribute<RPC>();

                    if (!ReflectionUtils.DetermineIfMethodIsRPCCompatible(methodInfo))
                    {
                        Debug.LogError($"Unable to register method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType}\", one or more of the method's parameters is not a value type or one of the parameter's members is not a value type.");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(rpcMethodAttribute.formarlySerializedAs) && methodInfo.Name != rpcMethodAttribute.formarlySerializedAs)
                    {
                        SerializedRPC ? nullableSerializedRPC = null;
                        m_SerializedRPCsContainer.Foreach((serializedRPC) =>
                        {
                            if (serializedRPC.methodName != rpcMethodAttribute.formarlySerializedAs)
                                return;

                            nullableSerializedRPC = serializedRPC;
                        });

                        if (nullableSerializedRPC == null)
                            goto registerNonSerializedRPC;

                        var serilizedRPC = nullableSerializedRPC.Value;
                        rpcIdsToAdd.Add(rpcMethodAttribute.rpcId);

                        var rpcMethodInfo = new RPCMethodInfo(
                            nullableSerializedRPC.Value.rpcId, 
                            (RPCExecutionStage)nullableSerializedRPC.Value.rpcExecutionStage, 
                            methodInfo);

                        ApplyRPC(ref rpcMethodInfo, serialize: false);
                        renamedRPCs.Add(rpcMethodAttribute.formarlySerializedAs);

                        continue;
                    }

                    registerNonSerializedRPC:
                    {
                        rpcIdsToAdd.Add(rpcMethodAttribute.rpcId);
                        var rpcMethodInfo = new RPCMethodInfo(rpcMethodAttribute.rpcId, rpcMethodAttribute.rpcExecutionStage, methodInfo);
                        ApplyRPC(ref rpcMethodInfo, serialize: false);
                    }

                    // Debug.Log($"Registered RPC with {nameof(RPC)} attribute for method: \"{methodInfo.Name}\" with RPC Execution Stage: \"{rpcMethodAttribute.rpcExecutionStage}\" and RPC ID: \"{rpcMethodAttribute.rpcId}\".");
                }
            }

            m_SerializedRPCsContainer.Foreach((serializedRPC) =>
            {
                var rpcExecutionStage = RPCExecutionStage.ImmediatelyOnArrival;

                if (renamedRPCs.Contains(serializedRPC.methodName))
                    return;

                if (!RPCSerializer.TryDeserializeMethodInfo(serializedRPC, out rpcExecutionStage, out var methodInfo))
                {
                    Debug.LogError($"Unable to deserialize method: \"{serializedRPC.methodName}\", declared in type: \"{serializedRPC.declaryingTypeFullName}\", if the method has renamed, you can use the {nameof(RPC)} attribute with the formarlySerializedAs parameter to insure that the method is deserialized properly.");
                    m_SerializedRPCsContainer.SetData(serializedRPC.rpcId, null);
                    return;
                }

                var rpcMethodAttribute = methodInfo.GetCustomAttribute<RPC>();
                if (rpcMethodAttribute != null)
                    rpcExecutionStage = rpcMethodAttribute.rpcExecutionStage;

                var deserializedRPCMethodInfo = new RPCMethodInfo(serializedRPC.rpcId, rpcExecutionStage, methodInfo);

                ApplyRPC(ref deserializedRPCMethodInfo, serialize: true);
                rpcIdsToAdd.Add(serializedRPC.rpcId);

                // Debug.Log($"Successfully deserialized method: \"{serializedRPC.methodName}\", declared in type: \"{serializedRPC.declaryingTypeFullName}\".");
            });

            if (rpcIdsToAdd.Count == 0)
            {
                m_RPCs.Clear();
                m_RPCLut.Clear();
                return;
            }

            rpcIdsToAdd.Sort();
            ushort largestId = rpcIdsToAdd.Last();
            m_IDManager.PushSetOfIds(rpcIdsToAdd.ToArray(), largestId);

            WriteRPCStubs();
        }

        private void Serialize ()
        {
            if (m_SerializedRPCsContainer.Count == -1)
                return;

            if (!m_IsDirty)
                return;

            WriteRPCStubs();
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
