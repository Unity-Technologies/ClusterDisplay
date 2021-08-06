using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

namespace Unity.ClusterDisplay.RPC
{
    [CreateAssetMenu(fileName = "RPCRegistry", menuName = "Cluster Display/RPC Registry")]
    #if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
    #endif
    [DefaultExecutionOrder(-1)]
    public partial class RPCRegistry : SingletonScriptableObject<RPCRegistry>, ISerializationCallbackReceiver
    {
        private static readonly Dictionary<ushort, RPCMethodInfo> m_RPCs = new Dictionary<ushort, RPCMethodInfo>();
        private static readonly Dictionary<MethodInfo, ushort> m_MethodUniqueIdToRPCId = new Dictionary<MethodInfo, ushort>();
        private static readonly Dictionary<System.Type, List<ushort>> m_TypeToRPCIds = new Dictionary<System.Type, List<ushort>>();
        public static int RPCCount => m_RPCs.Count;

        /// <summary>
        /// Specific assemblies need to be registered in order to perform IL post processing on
        /// them and those specific assemblies are stored here. We need to do this to serialize
        /// these assembly names into a text file so we can read them by the ILPostProcessor.
        /// </summary>
        private static readonly List<Assembly> m_TargetAssemblies = new List<Assembly>();
        public Assembly[] GetTargetAssemblies() => m_TargetAssemblies.ToArray();

        /// <summary>
        /// When we receive an RPC over the network, we need to identify which assembly were supposed to
        /// execute the RPC in. Foreach assembly a derrived instance of RPCInstanceRegistry is created and
        /// we use the assembly index to determine which delegate we call. See RPCInstanceRegistry's constructor.
        /// </summary>
        private static readonly Dictionary<ushort, ushort> m_AssemblyIndexLookUp = new Dictionary<ushort, ushort>();

        public static bool TryGetAssemblyIndex(ushort rpcId, out ushort assemblyIndex) => m_AssemblyIndexLookUp.TryGetValue(rpcId, out assemblyIndex);
        [SerializeField][HideInInspector] private IDManager m_IDManager = new IDManager();

        public const string k_RPCStubsPath = "./RPCStubs.json";

        /// <summary>
        /// This is where our assembly names are serialized to so they can be read
        /// by the ILPostProcessor when recompiling.
        /// </summary>
        public const string k_RegisteredAssembliesJsonPath = "./RegisteredAssemblies.txt";

        [SerializeField][HideInInspector] private string m_SerializedRegisteredAssemblies;
        [SerializeField][HideInInspector] private string m_JSONString;

        public static string MethodToUniqueId(MethodInfo methodInfo)
        {
            var assemblyName = methodInfo.Module.Assembly.GetName();
            return $"{assemblyName.Name}.{assemblyName.Version}.{methodInfo.Module.MetadataToken}.{methodInfo.DeclaringType.MetadataToken}.{methodInfo.MetadataToken}";
        }

        public delegate void OnTriggerRecompileDelegate();
        public static OnTriggerRecompileDelegate onTriggerRecompile;

        public delegate bool OnAddWrappableMethodDelegate(MethodInfo methodInfo);
        public delegate bool OnAddWrappablePropertyDelegate(PropertyInfo propertyInfo);

        public delegate bool OnRemoveMethodWrapperDelegate(MethodInfo methodInfo);
        public delegate bool OnRemovePropertyWrapperDelegate(PropertyInfo propertyInfo);

        public static OnAddWrappableMethodDelegate onAddWrappableMethod;
        public static OnAddWrappablePropertyDelegate onAddWrappableProperty;

        public static OnRemoveMethodWrapperDelegate onRemoveMethodWrapper;
        public static OnRemovePropertyWrapperDelegate onRemovePropertyWrapper;

        private bool m_Deserialized = false;
        public static bool Deserialized
        {
            get
            {
                if (!TryGetInstance(out var instance))
                    return false;
                return instance.m_Deserialized;
            }
        }

        public delegate void OnRegistryInitialized();
        public static OnRegistryInitialized onRegistryInitialized;

        private void Awake()
        {
            Deserialize();
            m_Deserialized = true;
            if (onRegistryInitialized != null)
                onRegistryInitialized();
        }

        public static bool MethodRegistered(ushort rpcId) => m_RPCs.ContainsKey(rpcId);
        public static bool MethodRegistered(MethodInfo methodInfo) => m_MethodUniqueIdToRPCId.ContainsKey(methodInfo);

        public static bool TryGetRPC(ushort rpcId, out RPCMethodInfo rpcMethodInfo) => m_RPCs.TryGetValue(rpcId, out rpcMethodInfo);
        public static bool TryGetRPC(MethodInfo methodInfo, out RPCMethodInfo rpcMethodInfo)
        {
            if (!m_MethodUniqueIdToRPCId.TryGetValue(methodInfo, out var rpcId))
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
                Debug.LogError($"There are no registered RPCs declared in type: \"{type.FullName}\".");
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

        private void RegisterAssembly (Assembly assembly)
        {
            var name = assembly.FullName;
            if (m_TargetAssemblies.Contains(assembly))
                return;
            m_TargetAssemblies.Add(assembly);
            SerializeRegisteredAssemblies();
        }

        private void UnregisterAssembly (Assembly assembly)
        {
            var name = assembly.FullName;
            if (!m_TargetAssemblies.Contains(assembly))
                return;
            m_TargetAssemblies.Remove(assembly);
            SerializeRegisteredAssemblies();
        }

        private bool AssemblyIsRegistered(Assembly assembly) => m_TargetAssemblies.Contains(assembly);

        private void RegisterRPCIdWithMethodUniqueId (MethodInfo methodInfo, ushort rpcId)
        {
            if (m_MethodUniqueIdToRPCId.ContainsKey(methodInfo))
            {
                m_MethodUniqueIdToRPCId[methodInfo] = rpcId;
                return;
            }

            m_MethodUniqueIdToRPCId.Add(methodInfo, rpcId);
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
        private bool TryRegisterRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            if (m_RPCs.TryGetValue(rpcMethodInfo.rpcId, out var registeredRPCMethodInfo))
            {
                Debug.LogError($"Unable to register RPC: \"{rpcMethodInfo.methodInfo.Name}\" with ID: \"{rpcMethodInfo.rpcId}\", there is an already RPC registered with that ID.");
                return false;
            }

            if (!m_TargetAssemblies.Contains(rpcMethodInfo.methodInfo.Module.Assembly))
                m_TargetAssemblies.Add(rpcMethodInfo.methodInfo.Module.Assembly);

            m_RPCs.Add(rpcMethodInfo.rpcId, rpcMethodInfo);
            RegisterRPCIdWithMethodUniqueId(rpcMethodInfo.methodInfo, rpcMethodInfo.rpcId);
            RegisterRPCIdWithType(rpcMethodInfo.methodInfo.DeclaringType, rpcMethodInfo.rpcId);

            return true;
        }

        public bool TryUpdateRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            m_RPCs[rpcMethodInfo.rpcId] = rpcMethodInfo;
            RegisterRPCIdWithMethodUniqueId(rpcMethodInfo.methodInfo, rpcMethodInfo.rpcId);
            RegisterRPCIdWithType(rpcMethodInfo.methodInfo.DeclaringType, rpcMethodInfo.rpcId);

            SetDirtyAndRecompile();
            return true;
        }

        private bool UnregisterRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            m_RPCs.Remove(rpcMethodInfo.rpcId);
            m_MethodUniqueIdToRPCId.Remove(rpcMethodInfo.methodInfo);
            m_IDManager.PushUnutilizedId(rpcMethodInfo.rpcId);

            if (WrapperUtils.IsWrapper(rpcMethodInfo.methodInfo.DeclaringType))
            {
                if (rpcMethodInfo.methodInfo.IsSpecialName)
                {
                    if (ReflectionUtils.TryGetPropertyViaAccessor(rpcMethodInfo.methodInfo, out var property))
                        if (onRemovePropertyWrapper != null)
                            onRemovePropertyWrapper(property);
                }

                else if (onRemoveMethodWrapper != null)
                onRemoveMethodWrapper(rpcMethodInfo.methodInfo);
            }

            return true;
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

        private bool TryStageNonPostProcessableMethod (MethodInfo methodInfo)
        {
            if (onAddWrappableMethod == null)
            {
                Debug.LogError($"No wrapper generator instances registered their callbacks with {nameof(RPCRegistry)}'s \"{nameof(OnAddWrappableMethodDelegate)}\" instance.");
                return false;
            }

            if (!onAddWrappableMethod(methodInfo))
                return false;

            StageRPCToRegister(methodInfo);
            return true;
        }

        private bool TryAddPostProcessableRPC (MethodInfo methodInfo)
        {
            var methodUniqueId = MethodToUniqueId(methodInfo);
            if (m_MethodUniqueIdToRPCId.TryGetValue(methodInfo, out var rpcId))
            {
                Debug.LogError($"Cannot add RPC method: \"{methodInfo.Name}\" from declaring type: \"{methodInfo.DeclaringType}\", it has already been registered!");
                return false;
            }

            if (!m_IDManager.TryPopId(out rpcId))
                return false;

            var rpcMethodInfo = new RPCMethodInfo(
                rpcId, 
                RPCExecutionStage.Automatic, 
                methodInfo, 
                usingWrapper: WrapperUtils.IsWrapper(methodInfo.DeclaringType));

            return TryRegisterRPC(ref rpcMethodInfo);
        }

        private bool TryStageNonPostProcessableMethod (PropertyInfo propertyInfo)
        {
            if (onAddWrappableProperty == null)
            {
                Debug.LogError($"No wrapper generator instances registered their callbacks with {nameof(RPCRegistry)}'s \"{nameof(OnAddWrappableMethodDelegate)}\" instance.");
                return false;
            }

            if (!onAddWrappableProperty(propertyInfo))
                return false;

            StageRPCToRegister(propertyInfo.SetMethod);
            return true;
        }

        public static bool MarkPropertyAsRPC (PropertyInfo propertyInfo)
        {
            if (!TryGetInstance(out var rpcRegistry))
                return false;

            if (!ReflectionUtils.DetermineIfMethodIsRPCCompatible(propertyInfo.SetMethod))
            {
                Debug.LogError($"Unable to register the property: \"{propertyInfo}\" set method: \"{propertyInfo.SetMethod.Name}\" declared in type: \"{propertyInfo.DeclaringType}\", the property's type is not a value type or one of the property type's members is not a value type.");
                return false;
            }

            if (!ReflectionUtils.IsAssemblyPostProcessable(propertyInfo.Module.Assembly))
            {
                if (!rpcRegistry.TryStageNonPostProcessableMethod(propertyInfo))
                    return false;
                goto success;
            }

            if (!rpcRegistry.TryAddPostProcessableRPC(propertyInfo.SetMethod))
                return false;

            success:
            rpcRegistry.SetDirtyAndRecompile();
            return true;
        }

        public static bool MarkMethodAsRPC (MethodInfo methodInfo)
        {
            if (!TryGetInstance(out var rpcRegistry))
                return false;

            if (!ReflectionUtils.DetermineIfMethodIsRPCCompatible(methodInfo))
            {
                Debug.LogError($"Unable to register method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType}\", one or more of the method's parameters is not a value type or one of the parameter's members is not a value type.");
                return false;
            }

            if (!ReflectionUtils.IsAssemblyPostProcessable(methodInfo.Module.Assembly))
            {
                if (!rpcRegistry.TryStageNonPostProcessableMethod(methodInfo))
                    return false;
                goto success;
            }

            if (!rpcRegistry.TryAddPostProcessableRPC(methodInfo))
                return false;

            success:
            rpcRegistry.SetDirtyAndRecompile();
            return true;
        }

        public static void UnmarkRPC (ushort rpcId)
        {
            if (!TryGetRPC(rpcId, out var rpcMethodInfo))
                return;

            if (!TryGetInstance(out var rpcRegistry))
                return;

            rpcRegistry.UnregisterRPC(ref rpcMethodInfo);
        }

        public static void UnmarkRPC (MethodInfo methodInfo)
        {
            var methodUniqueId = MethodToUniqueId(methodInfo);
            if (!m_MethodUniqueIdToRPCId.TryGetValue(methodInfo, out var rpcId))
                return;
            UnmarkRPC(rpcId);
        }

        /// <summary>
        /// When we are in the editor and we've added and RPC in the UI, we need to trigger a recompile.
        /// </summary>
        private void SetDirtyAndRecompile ()
        {
            #if UNITY_EDITOR
            if (IsSerializing || Application.isPlaying)
                return;

            UnityEditor.EditorUtility.SetDirty(this);

            if (onTriggerRecompile != null)
                onTriggerRecompile();

            UnityEditor.AssetDatabase.Refresh();
            if (!UnityEditor.EditorApplication.isCompiling)
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
            List<SerializedRPC> rpcs = new List<SerializedRPC>();
            foreach (var keyValuePair in m_RPCs)
            {
                var rpc = keyValuePair.Value;
                if (!rpc.IsValid)
                    continue;
                rpcs.Add(SerializedRPC.Create(ref rpc));
            }

            if (!RPCSerializer.TryPrepareRPCStubsForSerialization(rpcs.ToArray(), stagedMethods.Values.ToArray(), out m_JSONString))
                return;

            #if UNITY_EDITOR
            RPCSerializer.TryWriteRPCStubs(k_RPCStubsPath, m_JSONString);
            #endif

            m_IsDirty = true;
        }

        private void SerializeRegisteredAssemblies ()
        {
            RPCSerializer.PrepareRegisteredAssembliesForSerialization(m_TargetAssemblies, out m_SerializedRegisteredAssemblies);

            #if UNITY_EDITOR
            RPCSerializer.TryWriteRegisteredAssemblies(k_RegisteredAssembliesJsonPath, m_SerializedRegisteredAssemblies);
            #endif

            m_IsDirty = true;
        }

        private void InstanceRPCInstanceRegistry (Assembly assembly, ushort rpcId)
        {
            if (!RPCInterfaceRegistry.TryCreateImplementationInstance(assembly, out ushort assemblyIndex))
                return;

            if (!m_AssemblyIndexLookUp.ContainsKey(rpcId))
                m_AssemblyIndexLookUp.Add(rpcId, assemblyIndex);
        }

        private void DeserializeData (out List<Assembly> registeredAssemblies, out SerializedRPC[] serializedRPCs, out SerializedMethod[] serializedStagedMethods)
        {
            if (Application.isEditor)
                RPCSerializer.TryReadRegisteredAssemblies(k_RegisteredAssembliesJsonPath, out registeredAssemblies);
            else RPCSerializer.TryDeserializeRegisteredAssemblies(m_SerializedRegisteredAssemblies, out registeredAssemblies);

            if (Application.isEditor)
                RPCSerializer.ReadRPCStubs(k_RPCStubsPath, out serializedRPCs, out serializedStagedMethods);
            else RPCSerializer.TryDeserializeRPCStubsJson(m_JSONString, out serializedRPCs, out serializedStagedMethods);

            Debug.Log($"Deserialized: (Registered Assemblies: {(registeredAssemblies != null ? registeredAssemblies.Count : 0)}, RPCs: {(serializedRPCs != null ? serializedRPCs.Length : 0)})");
        }

        private bool TryRegisterDeserializedMethod (
            ushort rpcId, 
            RPCExecutionStage rpcExecutionStage, 
            MethodInfo methodInfo)
        {
            if (MethodRegistered(methodInfo))
                return false;

            var rpcMethodInfo = new RPCMethodInfo(
                rpcId, 
                rpcExecutionStage, 
                methodInfo, 
                usingWrapper: WrapperUtils.IsWrapper(methodInfo.DeclaringType));

            return TryRegisterRPC(ref rpcMethodInfo);
        }

        private bool TryRegisterStagedMethod (MethodInfo stagedMethod)
        {
            if (m_MethodUniqueIdToRPCId.TryGetValue(stagedMethod, out var rpcId))
            {
                Debug.LogError($"Attempted to re-register already registered method as an RPC: \"{stagedMethod.Name}\" delcared in: \"{stagedMethod.DeclaringType.FullName}\".");
                return false;
            }

            if (!m_IDManager.TryPopId(out rpcId))
                return false;

            var rpcMethodInfo = new RPCMethodInfo(
                rpcId, 
                RPCExecutionStage.Automatic, 
                stagedMethod, 
                usingWrapper: WrapperUtils.IsWrapper(stagedMethod.DeclaringType));

            return TryRegisterRPC(ref rpcMethodInfo);
        }

        private bool TryGetWrapperMethod (MethodInfo stagedMethod, out MethodInfo wrapperMethod)
        {
            var wrapperTypeName = WrapperUtils.GetWrapperFullName(stagedMethod.DeclaringType);
            var wrapperType = ReflectionUtils.GetTypeByName(wrapperTypeName);
            wrapperMethod = null;

            if (wrapperType == null)
            {
                Debug.LogError($"Unable to find wrapper type: \"{wrapperTypeName}\", verify whether the wrapper exists in the Assembly-CSharp assembly.");
                return false;
            }

            if (!ReflectionUtils.TryFindMethodWithMatchingSignature(wrapperType, stagedMethod, out wrapperMethod))
            {
                Debug.LogError($"Unable to find wrapper method: \"{stagedMethod.Name}\" in wrapper type: \"{stagedMethod.DeclaringType.Name}\", verify whether the wrapper method equals the target method.");
                return false;
            }

            return true;
        }

        private void DeserializeStagedMethods (
            List<Assembly> registeredAssemblies,
            SerializedMethod[] serializedStagedMethods)
        {
            for (int i = 0; i < serializedStagedMethods.Length; i++)
            {
                if (!RPCSerializer.TryDeserializeMethodInfo(serializedStagedMethods[i], out var stagedMethod))
                {
                    Debug.LogError($"Unable to deserialize method: \"{serializedStagedMethods[i].methodName}\", declared in type: \"{serializedStagedMethods[i].declaryingTypeName}\", if the method has renamed, you can use the {nameof(ClusterRPC)} attribute with the formarlySerializedAs parameter to insure that the method is deserialized properly.");
                    continue;
                }

                if (serializedStagedMethods[i].declaringAssemblyIsPostProcessable)
                {
                    TryRegisterStagedMethod(stagedMethod);
                    continue;
                }

                if (!TryGetWrapperMethod(stagedMethod, out var newStagedMethod))
                    continue;

                if (!TryRegisterStagedMethod(newStagedMethod))
                    continue;
            }
        }

        private MethodInfo[] cachedMethodsWithRPCAttribute = null;
        private void PollMethodsWithRPCAttribute (
            List<Assembly> registeredAssemblies,
            SerializedRPC[] serializedRPCs, 
            List<string> renamedRPCs)
        {
            if (cachedMethodsWithRPCAttribute == null)
            #if UNITY_EDITOR
                cachedMethodsWithRPCAttribute = UnityEditor.TypeCache.GetMethodsWithAttribute<ClusterRPC>().ToArray();
            #else
                ReflectionUtils.TryGetAllMethodsWithAttribute<ClusterRPC>(out cachedMethodsWithRPCAttribute);
            #endif

            foreach (var methodInfo in cachedMethodsWithRPCAttribute)
            {
                /*
                if (!registeredAssemblies.Contains(methodInfo.Module.Assembly))
                    continue;
                */

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
                        if (serializedRPCs[i].method.methodName != methodInfo.Name && serializedRPCs[i].method.methodName != rpcMethodAttribute.formarlySerializedAs)
                            continue;

                        nullableSerializedRPC = serializedRPCs[i];
                        break;
                    }
                }

                // If "formarlySerializedAs" has not beend defined, or if the current method matches the former name, then simply register the RPC.
                ushort rpcId = 0;

                // Are we overriding the RPC id in the method attribute?
                if (rpcMethodAttribute.rpcId != -1)
                    rpcId = (ushort)rpcMethodAttribute.rpcId;

                // Is this method already registered under a serialized RPC id? 
                else if (nullableSerializedRPC != null)
                    rpcId = nullableSerializedRPC.Value.rpcId;

                // Can we push this ID into the ID manager as an ID in use? If not, can we pop a new one?
                if (!m_IDManager.TryPushOrPopId(ref rpcId))
                    continue;

                var rpcExecutionStage = nullableSerializedRPC != null ? (RPCExecutionStage)nullableSerializedRPC.Value.rpcExecutionStage : rpcMethodAttribute.rpcExecutionStage;

                if (!TryRegisterDeserializedMethod(
                    rpcId, 
                    rpcExecutionStage, 
                    methodInfo))
                    continue;

                if (!string.IsNullOrEmpty(rpcMethodAttribute.formarlySerializedAs))
                    renamedRPCs.Add(rpcMethodAttribute.formarlySerializedAs);
            }
        }

        private void DeserializeRegisteredRPCMethods (
            List<Assembly> registeredAssemblies,
            SerializedRPC[] serializedRPCs,
            List<string> renamedRPCs)
        {
            for (int i = 0; i < serializedRPCs.Length; i++)
            {
                var rpcExecutionStage = RPCExecutionStage.ImmediatelyOnArrival;
                if (renamedRPCs.Contains(serializedRPCs[i].method.methodName))
                    return;

                if (!RPCSerializer.TryDeserializeMethodInfo(serializedRPCs[i], out rpcExecutionStage, out var methodInfo))
                    continue;

                if (!registeredAssemblies.Contains(methodInfo.Module.Assembly))
                    continue;

                var rpcMethodAttribute = methodInfo.GetCustomAttribute<ClusterRPC>();
                if (rpcMethodAttribute != null)
                    rpcExecutionStage = rpcMethodAttribute.rpcExecutionStage;

                // Can we push this ID into the ID manager as an ID in use? If not, can we pop a new one?
                var rpcId = serializedRPCs[i].rpcId;
                if (!m_IDManager.TryPushOrPopId(ref rpcId))
                        continue;

                if (!TryRegisterDeserializedMethod(
                    rpcId,
                    rpcExecutionStage,
                    methodInfo))
                    continue;
            }
        }

        private void AssociateRPCIdsWithAssemblies ()
        {
            foreach (var rpcIdAndInfo in m_RPCs)
            {
                if (!RPCInterfaceRegistry.TryCreateImplementationInstance(rpcIdAndInfo.Value.methodInfo.Module.Assembly, out var assemblyIndex))
                    continue;
                if (m_AssemblyIndexLookUp.ContainsKey(rpcIdAndInfo.Key))
                    m_AssemblyIndexLookUp[rpcIdAndInfo.Key] = assemblyIndex;
                else m_AssemblyIndexLookUp.Add(rpcIdAndInfo.Key, assemblyIndex);
            }
        }

        private void Deserialize ()
        {
            DeserializeData(out var registeredAssemblies, out var serializedRPCs, out var serializedStagedMethods);
            m_IDManager.Clear();

            List<string> renamedRPCs = new List<string>();
            PollMethodsWithRPCAttribute(
                registeredAssemblies,
                serializedRPCs,
                renamedRPCs);

            DeserializeRegisteredRPCMethods(
                registeredAssemblies,
                serializedRPCs,
                renamedRPCs);

            DeserializeStagedMethods(
                registeredAssemblies,
                serializedStagedMethods);

            if (m_RPCs.Count == 0)
            {
                m_MethodUniqueIdToRPCId.Clear();
                return;
            }

            else AssociateRPCIdsWithAssemblies();

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
