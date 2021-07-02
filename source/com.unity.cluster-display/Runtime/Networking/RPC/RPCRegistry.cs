using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace Unity.ClusterDisplay
{
    public enum RPCType
    {
        Undefined,
        Core,
        PostProcessable
    }

    public struct RPCMethodInfo
    {
        public readonly MethodInfo methodInfo;
        public readonly string methodUniqueId;
        public readonly ushort rpcId;
        public RPCExecutionStage rpcExecutionStage;

        public bool IsValid => methodInfo != null;
        public bool IsStatic => methodInfo != null ? methodInfo.IsStatic : false;

        public RPCMethodInfo (
            ushort rpcId, 
            RPCExecutionStage rpcExecutionStage, 
            MethodInfo methodInfo, 
            string methodUniqueId)
        {
            this.rpcId = rpcId;
            this.rpcExecutionStage = rpcExecutionStage;
            this.methodInfo = methodInfo;
            this.methodUniqueId = methodUniqueId;
        }
    }

    [CreateAssetMenu(fileName = "RPCRegistry", menuName = "Cluster Display/RPC Registry")]
    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
    public partial class RPCRegistry : SingletonScriptableObject<RPCRegistry>, ISerializationCallbackReceiver
    {
        private static readonly Dictionary<ushort, RPCMethodInfo> m_RPCs = new Dictionary<ushort, RPCMethodInfo>();
        private static readonly Dictionary<string, ushort> m_MethodUniqueIdToRPCId = new Dictionary<string, ushort>();
        private static readonly Dictionary<System.Type, List<ushort>> m_TypeToRPCIds = new Dictionary<System.Type, List<ushort>>();
        public static int RPCCount => m_RPCs.Count;

        /// <summary>
        /// Specific assemblies need to be registered in order to perform IL post processing on
        /// them and those specific assemblies are stored here. We need to do this to serialize
        /// these assembly names into a text file so we can read them by the ILPostProcessor.
        /// </summary>
        private static readonly List<Assembly> targetAssemblies = new List<Assembly>();
        public Assembly[] GetTargetAssemblies() => targetAssemblies.ToArray();

        /// <summary>
        /// When we receive an RPC over the network, we need to identify which assembly were supposed to
        /// execute the RPC in. Foreach assembly a derrived instance of RPCInstanceRegistry is created and
        /// we use the assembly index to determine which delegate we call. See RPCInstanceRegistry's constructor.
        /// </summary>
        private static readonly Dictionary<ushort, ushort> assemblyIndexLookUp = new Dictionary<ushort, ushort>();

        public static bool TryGetAssemblyIndex(ushort rpcId, out ushort assemblyIndex) => assemblyIndexLookUp.TryGetValue(rpcId, out assemblyIndex);
        [SerializeField][HideInInspector] private IDManager m_IDManager = new IDManager();

        public const string RPCStubsPath = "./RPCStubs.json";

        /// <summary>
        /// This is where our assembly names are serialized to so they can be read
        /// by the ILPostProcessor when recompiling.
        /// </summary>
        public const string RegisteredAssembliesJsonPath = "./RegisteredAssemblies.txt";

        [SerializeField][HideInInspector] private string serializedRegisteredAssemblies;
        [SerializeField][HideInInspector] private string serializedRPCStubs;

        public static string MethodToUniqueId(MethodInfo methodInfo)
        {
            var assemblyName = methodInfo.Module.Assembly.GetName();
            return $"{assemblyName.Name}.{assemblyName.Version}.{methodInfo.Module.MetadataToken}.{methodInfo.DeclaringType.MetadataToken}.{methodInfo.MetadataToken}";
        }

        public delegate void OnTriggerRecompileDelegate();
        public static OnTriggerRecompileDelegate onTriggerRecompile;

        public static bool MethodRegistered(ushort rpcId) => m_RPCs.ContainsKey(rpcId);
        public static bool MethodRegistered(MethodInfo methodInfo) => m_MethodUniqueIdToRPCId.ContainsKey(MethodToUniqueId(methodInfo));
        public static bool MethodRegistered(string methodUniqueId) => m_MethodUniqueIdToRPCId.ContainsKey(methodUniqueId);

        public static bool TryGetRPC(ushort rpcId, out RPCMethodInfo rpcMethodInfo) => m_RPCs.TryGetValue(rpcId, out rpcMethodInfo);
        public static bool TryGetRPC(MethodInfo methodInfo, out RPCMethodInfo rpcMethodInfo)
        {
            if (!m_MethodUniqueIdToRPCId.TryGetValue(MethodToUniqueId(methodInfo), out var rpcId))
            {
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            return m_RPCs.TryGetValue(rpcId, out rpcMethodInfo);
        }

        public static bool TryGetRPCsForType(System.Type type, out RPCMethodInfo[] rpcs)
        {
            if (!m_TypeToRPCIds.TryGetValue(type, out var rpcIds))
            {
                rpcs = null;
                return false;
            }

            List<RPCMethodInfo> list = new List<RPCMethodInfo>();
            for (int i = 0; i < rpcIds.Count; i++)
                list.Add(m_RPCs[rpcIds[i]]);

            rpcs = list.ToArray();
            return true;
        }

        public static RPCMethodInfo[] CopyRPCs () => m_RPCs.Values.ToArray();

        public static bool Setup ()
        {
            if (!TryGetInstance(out var rpcRegistry))
            {
                Debug.LogError($"Unable to retrieve instance of \"{nameof(RPCRegistry)}\".");
                return false;
            }

            if (m_RPCs.Count == 0)
                rpcRegistry.Deserialize();

            return true;
        }

        private void RegisterAssembly (Assembly assembly)
        {
            var name = assembly.FullName;
            if (targetAssemblies.Contains(assembly))
                return;
            targetAssemblies.Add(assembly);
            SerializeRegisteredAssemblies();
        }

        private void UnregisterAssembly (Assembly assembly)
        {
            var name = assembly.FullName;
            if (!targetAssemblies.Contains(assembly))
                return;
            targetAssemblies.Remove(assembly);
            SerializeRegisteredAssemblies();
        }

        private bool AssemblyIsRegistered(Assembly assembly) => targetAssemblies.Contains(assembly);

        private void RegisterRPCIdWithMethodUniqueId (string methodUniqueId, ushort rpcId)
        {
            if (m_MethodUniqueIdToRPCId.ContainsKey(methodUniqueId))
            {
                m_MethodUniqueIdToRPCId[methodUniqueId] = rpcId;
                return;
            }

            m_MethodUniqueIdToRPCId.Add(methodUniqueId, rpcId);
        }

        private void RegisterRPCIdWithType (System.Type type, ushort rpcId)
        {
            if (m_TypeToRPCIds.ContainsKey(type))
            {
                m_TypeToRPCIds[type].Add(rpcId);
                return;
            }

            m_TypeToRPCIds.Add(type, new List<ushort>() { rpcId });
        }

        /// <summary>
        /// Register an RPC.
        /// </summary>
        /// <param name="rpcMethodInfo">The RPC information.</param>
        /// <param name="serialize">If the RPC is flagged using the [RPC] attribute, then we don't need to serialize it.</param>
        private void RegisterRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            if (m_RPCs.TryGetValue(rpcMethodInfo.rpcId, out var registeredRPCMethodInfo))
            {
                Debug.LogError($"Unable to register RPC: \"{rpcMethodInfo.methodInfo.Name}\" with ID: \"{rpcMethodInfo.rpcId}\", there is an already RPC registered with that ID.");
                return;
            }

            if (!RPCSerializer.TryCreateSerializableRPC(ref rpcMethodInfo, out var serializedRPC))
                return;

            if (!targetAssemblies.Contains(rpcMethodInfo.methodInfo.Module.Assembly))
                targetAssemblies.Add(rpcMethodInfo.methodInfo.Module.Assembly);

            m_RPCs.Add(rpcMethodInfo.rpcId, rpcMethodInfo);
            RegisterRPCIdWithMethodUniqueId(rpcMethodInfo.methodUniqueId, rpcMethodInfo.rpcId);
            RegisterRPCIdWithType(rpcMethodInfo.methodInfo.DeclaringType, rpcMethodInfo.rpcId);

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        public void UpdateRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            if (!RPCSerializer.TryCreateSerializableRPC(ref rpcMethodInfo, out var serializedRPC))
                return;

            m_RPCs[rpcMethodInfo.rpcId] = rpcMethodInfo;
            RegisterRPCIdWithMethodUniqueId(MethodToUniqueId(rpcMethodInfo.methodInfo), rpcMethodInfo.rpcId);
            RegisterRPCIdWithType(rpcMethodInfo.methodInfo.DeclaringType, rpcMethodInfo.rpcId);

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void UnregisterRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            m_RPCs.Remove(rpcMethodInfo.rpcId);
            m_MethodUniqueIdToRPCId.Remove(rpcMethodInfo.methodUniqueId);
            m_IDManager.PushUnutilizedId(rpcMethodInfo.rpcId);

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void UnregisterRPCOnDeserialize (ushort rpcId)
        {
            m_RPCs[rpcId] = default(RPCMethodInfo);
            m_IDManager.PushUnutilizedId(rpcId);
        }

        public void Foreach (System.Action<RPCMethodInfo> callback)
        {
            var keys = m_RPCs.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
                callback(m_RPCs[keys[i]]);
        }

        public static bool TryAddNewRPC (System.Type type, MethodInfo methodInfo, RPCExecutionStage rpcExecutionStage, out RPCMethodInfo rpcMethodInfo)
        {
            if (!TryGetInstance(out var rpcRegistry))
                goto failure;

            if (!ReflectionUtils.DetermineIfMethodIsRPCCompatible(methodInfo))
            {
                Debug.LogError($"Unable to register method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType}\", one or more of the method's parameters is not a value type or one of the parameter's members is not a value type.");
                goto failure;
            }

            var methodUniqueId = MethodToUniqueId(methodInfo);
            if (m_MethodUniqueIdToRPCId.TryGetValue(methodUniqueId, out var rpcId))
            {
                Debug.LogError($"Cannot add RPC method: \"{methodInfo.Name}\" from declaring type: \"{methodInfo.DeclaringType}\", it has already been registered!");
                goto failure;
            }

            if (!rpcRegistry.m_IDManager.TryPopId(out rpcId))
                goto failure;

            rpcMethodInfo = new RPCMethodInfo(
                rpcId, 
                rpcExecutionStage, 
                methodInfo, 
                methodUniqueId);

            rpcRegistry.RegisterRPC(ref rpcMethodInfo);
            return true;

            failure:
            rpcMethodInfo = default(RPCMethodInfo);
            return false;
        }

        public static void RemoveRPC (ushort rpcId)
        {
            if (!TryGetRPC(rpcId, out var rpcMethodInfo))
                return;

            if (!TryGetInstance(out var rpcRegistry))
                return;

            rpcRegistry.UnregisterRPC(ref rpcMethodInfo);
        }

        public static void RemoveRPC (MethodInfo methodInfo)
        {
            var methodUniqueId = MethodToUniqueId(methodInfo);
            if (!m_MethodUniqueIdToRPCId.TryGetValue(methodUniqueId, out var rpcId))
                return;
            RemoveRPC(rpcId);
        }

        /// <summary>
        /// When we are in the editor and we've added and RPC in the UI, we need to trigger a recompile.
        /// </summary>
        private void SetDirtyAndRecompile ()
        {
            #if UNITY_EDITOR
            if (IsSerializing)
                return;

            EditorUtility.SetDirty(this);

            if (onTriggerRecompile != null)
                onTriggerRecompile();

            AssetDatabase.Refresh();
            if (!EditorApplication.isCompiling)
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            m_IsDirty = true;
            #endif
        }

        public void Clear ()
        {
            m_MethodUniqueIdToRPCId.Clear();
            m_RPCs.Clear();
            m_IDManager.Clear();

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void SerializeAll ()
        {
            SerializeRegisteredAssemblies();
            SerializeRPCStubs();
        }

        private void SerializeRPCStubs ()
        {
            List<SerializedRPC> list = new List<SerializedRPC>();
            foreach (var keyValuePair in m_RPCs)
            {
                var rpc = keyValuePair.Value;
                if (!RPCSerializer.TryCreateSerializableRPC(ref rpc, out var serializedRPC))
                    continue;
                list.Add(serializedRPC);
            }

            if (!RPCSerializer.TryPrepareRPCStubsForSerialization(list.ToArray(), out serializedRPCStubs))
                return;

            #if UNITY_EDITOR
            RPCSerializer.TryWriteRPCStubs(RPCStubsPath, serializedRPCStubs);
            #endif

            m_IsDirty = true;
        }

        private void SerializeRegisteredAssemblies ()
        {
            RPCSerializer.PrepareRegisteredAssembliesForSerialization(targetAssemblies, out serializedRegisteredAssemblies);

            #if UNITY_EDITOR
            RPCSerializer.TryWriteRegisteredAssemblies(RegisteredAssembliesJsonPath, serializedRegisteredAssemblies);
            #endif

            m_IsDirty = true;
        }

        private void InstanceRPCInstanceRegistry (Assembly assembly, ushort rpcId)
        {
            if (!RPCInterfaceRegistry.TryCreateImplementationInstance(assembly, out ushort assemblyIndex))
                return;

            if (!assemblyIndexLookUp.ContainsKey(rpcId))
                assemblyIndexLookUp.Add(rpcId, assemblyIndex);
        }

        private bool TryRegisterSerializeRPC (
            Assembly assembly, 
            ushort rpcId, 
            RPCExecutionStage rpcExecutionStage, 
            MethodInfo methodInfo)
        {
            var methodUniqueId = MethodToUniqueId(methodInfo);
            if (MethodRegistered(methodUniqueId))
                return false;

            var rpcMethodInfo = new RPCMethodInfo(
                rpcId, 
                rpcExecutionStage, 
                methodInfo, 
                methodUniqueId);

            RegisterRPC(ref rpcMethodInfo);

            return true;
        }

        private void DeserializeData (out List<Assembly> registeredAssemblies, out SerializedRPC[] serializedRPCs)
        {
            registeredAssemblies = null;
            serializedRPCs = null;

            if (Application.isEditor)
                RPCSerializer.TryReadRegisteredAssemblies(RegisteredAssembliesJsonPath, out registeredAssemblies);
            else RPCSerializer.TryDeserializeRegisteredAssemblies(serializedRegisteredAssemblies, out registeredAssemblies);

            if (Application.isEditor)
                RPCSerializer.TryReadRPCStubs(RPCStubsPath, out serializedRPCs);
            else RPCSerializer.TryDeserializeRPCStubsJson(serializedRPCStubs, out serializedRPCs);

            Debug.Log($"Deserialized: (Registered Assemblies: {(registeredAssemblies != null ? registeredAssemblies.Count : 0)}, RPCs: {(serializedRPCs != null ? serializedRPCs.Length : 0)})");
        }

        private MethodInfo[] cachedMethodsWithRPCAttribute = null;
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
            /*
            if (!ReflectionUtils.TryGetDefaultAssembly(out var _, logError: false))
                return;
            */

            DeserializeData(out var registeredAssemblies, out var serializedRPCs);

            List<ushort> rpcIdsToAdd = new List<ushort>();
            List<string> renamedRPCs = new List<string>();

            if (cachedMethodsWithRPCAttribute == null)
                ReflectionUtils.TryGetAllMethodsWithAttribute<ClusterRPC>(out cachedMethodsWithRPCAttribute);

            if (cachedMethodsWithRPCAttribute != null)
            {
                foreach (var methodInfo in cachedMethodsWithRPCAttribute)
                {
                    if (!registeredAssemblies.Contains(methodInfo.Module.Assembly))
                        continue;

                    var rpcMethodAttribute = methodInfo.GetCustomAttribute<ClusterRPC>();

                    if (!ReflectionUtils.DetermineIfMethodIsRPCCompatible(methodInfo))
                    {
                        Debug.LogError($"Unable to register method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType}\", one or more of the method's parameters is not a value type or one of the parameter's members is not a value type.");
                        continue;
                    }

                    SerializedRPC ? nullableSerializedRPC = null;
                    // Search for the serialized RPC with the former name.
                    if (serializedRPCs != null)
                    {
                        for (int i = 0; i < serializedRPCs.Length; i++)
                        {
                            if (serializedRPCs[i].methodName != methodInfo.Name && serializedRPCs[i].methodName != rpcMethodAttribute.formarlySerializedAs)
                                continue;

                            nullableSerializedRPC = serializedRPCs[i];
                            break;
                        }
                    }

                    // If "formarlySerializedAs" has not beend defined, or if the current method matches the former name, then simply register the RPC.
                    var rpcId = nullableSerializedRPC != null ? nullableSerializedRPC.Value.rpcId : rpcMethodAttribute.rpcId;
                    var rpcExecutionStage = nullableSerializedRPC != null ? (RPCExecutionStage)nullableSerializedRPC.Value.rpcExecutionStage : rpcMethodAttribute.rpcExecutionStage;

                    if (!TryRegisterSerializeRPC(
                        methodInfo.Module.Assembly, 
                        rpcId, 
                        rpcExecutionStage, 
                        methodInfo))
                        continue;

                    rpcIdsToAdd.Add(rpcMethodAttribute.rpcId);
                    if (!string.IsNullOrEmpty(rpcMethodAttribute.formarlySerializedAs))
                        renamedRPCs.Add(rpcMethodAttribute.formarlySerializedAs);
                }
            }

            for (int i = 0; i < serializedRPCs.Length; i++)
            {
                var rpcExecutionStage = RPCExecutionStage.ImmediatelyOnArrival;
                if (renamedRPCs.Contains(serializedRPCs[i].methodName))
                    return;

                if (!RPCSerializer.TryDeserializeMethodInfo(serializedRPCs[i], out rpcExecutionStage, out var methodInfo))
                {
                    Debug.LogError($"Unable to deserialize method: \"{serializedRPCs[i].methodName}\", declared in type: \"{serializedRPCs[i].declaryingTypeFullName}\", if the method has renamed, you can use the {nameof(ClusterRPC)} attribute with the formarlySerializedAs parameter to insure that the method is deserialized properly.");
                    continue;
                }

                if (!registeredAssemblies.Contains(methodInfo.Module.Assembly))
                    continue;

                var rpcMethodAttribute = methodInfo.GetCustomAttribute<ClusterRPC>();
                if (rpcMethodAttribute != null)
                    rpcExecutionStage = rpcMethodAttribute.rpcExecutionStage;

                if (!TryRegisterSerializeRPC(
                    methodInfo.Module.Assembly,
                    serializedRPCs[i].rpcId,
                    rpcExecutionStage,
                    methodInfo))
                    continue;

                rpcIdsToAdd.Add(serializedRPCs[i].rpcId);
            }

            if (rpcIdsToAdd.Count == 0)
            {
                m_RPCs.Clear();
                m_MethodUniqueIdToRPCId.Clear();
                return;
            }

            else
            {
                rpcIdsToAdd.Sort();
                ushort largestId = rpcIdsToAdd.Last();
                m_IDManager.PushSetOfIds(rpcIdsToAdd.ToArray(), largestId);

                foreach (var rpcIdAndInfo in m_RPCs)
                {
                    if (!RPCInterfaceRegistry.TryCreateImplementationInstance(rpcIdAndInfo.Value.methodInfo.Module.Assembly, out var assemblyIndex))
                        continue;
                    if (assemblyIndexLookUp.ContainsKey(rpcIdAndInfo.Key))
                        assemblyIndexLookUp[rpcIdAndInfo.Key] = assemblyIndex;
                    else assemblyIndexLookUp.Add(rpcIdAndInfo.Key, assemblyIndex);
                }
            }

            if (Application.isEditor)
                SerializeAll();
        }

        private void Serialize ()
        {
            if (!m_IsDirty)
                return;

            SerializeAll();
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
