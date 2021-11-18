using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

using DeserializedRPCList = System.Collections.Generic.List<(ushort rpcId, bool overrideRPCExecutionStage, Unity.ClusterDisplay.RPC.RPCExecutionStage rpcExecutionStage, System.Reflection.MethodInfo methodInfo)>; 

namespace Unity.ClusterDisplay.RPC
{
    [CreateAssetMenu(fileName = "RPCRegistry", menuName = "Cluster Display/RPC Registry")]
    [DefaultExecutionOrder(int.MinValue)]
    #if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
    #endif
    public partial class RPCRegistry : SingletonScriptableObject<RPCRegistry>, ISerializationCallbackReceiver
    {
        public const int MaxRPCCount = 256;
        private static RPCMethodInfo?[] m_RPCs = new RPCMethodInfo?[MaxRPCCount];

        private static int m_RPCCount;
        public static int RPCCount => m_RPCCount;
        private static int m_LargestRPCId;

        private static readonly Dictionary<MethodInfo, ushort> m_MethodUniqueIdToRPCId = new Dictionary<MethodInfo, ushort>();
        private static readonly Dictionary<System.Type, List<ushort>> m_TypeToRPCIds = new Dictionary<System.Type, List<ushort>>();

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
        private static readonly ushort[] m_AssemblyIndexLookUp = new ushort[ushort.MaxValue];

        public static bool TryGetAssemblyIndex(ushort rpcId, out ushort assemblyIndex)
        {
            var storedAssemblyIndex = m_AssemblyIndexLookUp[rpcId];
            assemblyIndex = (ushort)(storedAssemblyIndex - 1);

            if (storedAssemblyIndex == 0)
                return false;

            ClusterDebug.Log($"Retrieved assembly: {m_TargetAssemblies[assemblyIndex].GetName().Name} at index: {assemblyIndex} for RPC ID: {rpcId}");
            return true;
        }

        public static Assembly GetAssembly(ushort rpcId) =>
            m_TargetAssemblies[m_AssemblyIndexLookUp[rpcId] - 1];

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

        public delegate void InitializationDelegate();
        private static InitializationDelegate onInitialized;

        public static void InitializeWhenReady (InitializationDelegate _onInitialized)
        {
            if (!TryGetInstance(out var instance, throwError: true))
                return;

            if (instance.m_Deserialized)
            {
                _onInitialized();
                return;
            }

            ClusterDebug.Log($"{nameof(RPCRegistry)} has not deserialized yet, we will initialize when later when we deserialize.");

            onInitialized -= _onInitialized;
            onInitialized += _onInitialized;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnLoad ()
        {
            ClusterDisplayManager.awake -= Initialize;
            ClusterDisplayManager.awake += Initialize;
        }

        private static void Initialize ()
        {
            ClusterDebug.Log($"Initializing {nameof(RPCRegistry)}");

            if (!TryGetInstance(out var instance, throwError: true))
                return;

            if (!instance.m_Deserialized)
                instance.Deserialize();

            instance.AssociateRPCIdsWithAssemblies();

            onInitialized?.Invoke();
            onInitialized = null;

            ClusterDisplayManager.awake -= Initialize;
        }

        public static bool MethodRegistered(ushort rpcId) => m_RPCs[rpcId] != null;
        public static bool MethodRegistered(MethodInfo methodInfo) => m_MethodUniqueIdToRPCId.ContainsKey(methodInfo);

        public static bool TryGetRPC(ushort rpcId, out RPCMethodInfo rpcMethodInfo)
        {
            if (rpcId < 0 || rpcId >= m_RPCs.Length)
            {
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            if (m_RPCs[rpcId] == null)
            {
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            rpcMethodInfo = m_RPCs[rpcId].Value;
            return true;
        }

        public static bool TryGetRPC(MethodInfo methodInfo, out RPCMethodInfo rpcMethodInfo)
        {
            if (!m_MethodUniqueIdToRPCId.TryGetValue(methodInfo, out var rpcId))
            {
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            return TryGetRPC(rpcId, out rpcMethodInfo);
        }

        public static bool TryGetRPCsForType(System.Type type, out RPCMethodInfo[] rpcs, bool logError = true)
        {
            if (!m_TypeToRPCIds.TryGetValue(type, out var rpcIds))
            {
                if (logError)
                    ClusterDebug.LogError($"There are no registered RPCs declared in type: \"{type.FullName}\".");
                rpcs = null;
                return false;
            }

            List<RPCMethodInfo> list = new List<RPCMethodInfo>();
            for (int i = 0; i < rpcIds.Count; i++)
            {
                if (m_RPCs[rpcIds[i]] == null)
                    continue;
                list.Add(m_RPCs[rpcIds[i]].Value);
            }

            rpcs = list.ToArray();
            return true;
        }

        public static RPCMethodInfo[] CopyRPCs () => m_RPCs.Select(rpcMethodInfo => rpcMethodInfo.Value).ToArray();

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

        public class AssemblyOrder : IComparer<Assembly>
        {
            public int Compare(Assembly x, Assembly y) => x.Location.CompareTo(y.Location);
        }

        /// <summary>
        /// Register an RPC.
        /// </summary>
        /// <param name="rpcMethodInfo">The RPC information.</param>
        /// <param name="serialize">If the RPC is flagged using the [RPC] attribute, then we don't need to serialize it.</param>
        private bool TryRegisterRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            if (rpcMethodInfo.rpcId > MaxRPCCount)
            {
                ClusterDebug.LogError($"Unable to register RPC with ID: {rpcMethodInfo.rpcId}, this ID is larger the max ID of: {MaxRPCCount}");
                return false;
            }

            if (TryGetRPC(rpcMethodInfo.rpcId, out var registeredRPCMethodInfo))
            {
                ClusterDebug.LogError($"Unable to register RPC: \"{rpcMethodInfo.methodInfo.Name}\" with ID: \"{rpcMethodInfo.rpcId}\", there is an already RPC registered with that ID.");
                return false;
            }

            if (!m_TargetAssemblies.Contains(rpcMethodInfo.methodInfo.Module.Assembly))
            {
                m_TargetAssemblies.Add(rpcMethodInfo.methodInfo.Module.Assembly);
                m_TargetAssemblies.Sort(new AssemblyOrder()); // Everytime we add an assembly, we need to sort so we have assembly order parity between editor instances.
            }
            
            ClusterDebug.Log($"Registering new RPC: (Name: {rpcMethodInfo.methodInfo.Name}, RPC ID: {rpcMethodInfo.rpcId}, RPC Execution Stage: {rpcMethodInfo.rpcExecutionStage}).");

            m_RPCs[rpcMethodInfo.rpcId] = rpcMethodInfo;
            RegisterRPCIdWithMethodUniqueId(rpcMethodInfo.methodInfo, rpcMethodInfo.rpcId);
            RegisterRPCIdWithType(rpcMethodInfo.methodInfo.DeclaringType, rpcMethodInfo.rpcId);

            if (rpcMethodInfo.rpcId > m_LargestRPCId)
                m_LargestRPCId = rpcMethodInfo.rpcId;

            m_RPCCount++;
            return true;
        }

        public bool TryUpdateRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            ClusterDebug.Log($"Updating RPC: (Name: {rpcMethodInfo.methodInfo.Name}, RPC ID: {rpcMethodInfo.rpcId}, RPC Execution Stage: {rpcMethodInfo.rpcExecutionStage}).");
            
            m_RPCs[rpcMethodInfo.rpcId] = rpcMethodInfo;
            RegisterRPCIdWithMethodUniqueId(rpcMethodInfo.methodInfo, rpcMethodInfo.rpcId);
            RegisterRPCIdWithType(rpcMethodInfo.methodInfo.DeclaringType, rpcMethodInfo.rpcId);

            SetDirtyAndRecompile();
            return true;
        }

        private bool UnregisterRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            m_RPCs[rpcMethodInfo.rpcId] = null;
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

            m_RPCCount--;
            return true;
        }

        private void UnregisterRPCOnDeserialize (ushort rpcId)
        {
            m_RPCs[rpcId] = default(RPCMethodInfo);
            m_IDManager.PushUnutilizedId(rpcId);
        }

        public void Foreach (System.Action<RPCMethodInfo> callback)
        {
            for (int rpcId = 0; rpcId <= m_LargestRPCId; rpcId++)
            {
                if (m_RPCs[rpcId] == null)
                    continue;

                callback(m_RPCs[rpcId].Value);
            }
        }

        private bool TryStageNonPostProcessableMethod (MethodInfo methodInfo)
        {
            if (onAddWrappableMethod == null)
            {
                ClusterDebug.LogError($"No wrapper generator instances registered their callbacks with {nameof(RPCRegistry)}'s \"{nameof(OnAddWrappableMethodDelegate)}\" instance.");
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
                ClusterDebug.LogError($"Cannot add RPC method: \"{methodInfo.Name}\" from declaring type: \"{methodInfo.DeclaringType}\", it has already been registered!");
                return false;
            }

            if (!m_IDManager.TryPopId(out rpcId))
                return false;

            var rpcMethodInfo = new RPCMethodInfo(
                rpcId, 
                false,
                RPCExecutionStage.Automatic, 
                methodInfo, 
                usingWrapper: WrapperUtils.IsWrapper(methodInfo.DeclaringType));

            return TryRegisterRPC(ref rpcMethodInfo);
        }

        private bool TryStageNonPostProcessableMethod (PropertyInfo propertyInfo)
        {
            if (onAddWrappableProperty == null)
            {
                ClusterDebug.LogError($"No wrapper generator instances registered their callbacks with {nameof(RPCRegistry)}'s \"{nameof(OnAddWrappableMethodDelegate)}\" instance.");
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
                ClusterDebug.LogError($"Unable to register the property: \"{propertyInfo}\" set method: \"{propertyInfo.SetMethod.Name}\" declared in type: \"{propertyInfo.DeclaringType}\", the property's type is not a value type or one of the property type's members is not a value type.");
                return false;
            }

            if (!ReflectionUtils.IsAssemblyPostProcessable(Application.dataPath, propertyInfo.Module.Assembly))
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
                ClusterDebug.LogError($"Unable to register method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType}\", one or more of the method's parameters is not a value type or one of the parameter's members is not a value type.");
                return false;
            }

            if (!ReflectionUtils.IsAssemblyPostProcessable(Application.dataPath, methodInfo.Module.Assembly))
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
            m_RPCs = new RPCMethodInfo?[MaxRPCCount];
            m_MethodUniqueIdToRPCId.Clear();
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
            for (int rpcId = 0; rpcId <= m_LargestRPCId; rpcId++)
            {
                if (m_RPCs[rpcId] == null)
                    continue;

                var rpc = m_RPCs[rpcId].Value;
                list.Add(SerializedRPC.Create(ref rpc));
            }

            var orderedRPCs = list.OrderBy(rpc => rpc.method.methodName);
            var orderedStagedMethods = stagedMethods.Values.OrderBy(value => value.methodName);

            if (!RPCSerializer.TryPrepareRPCStubsForSerialization(orderedRPCs.ToArray(), orderedStagedMethods.ToArray(), out m_JSONString))
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

        private void DeserializeData (out List<Assembly> registeredAssemblies, out SerializedRPC[] serializedRPCs, out SerializedMethod[] serializedStagedMethods)
        {
            if (Application.isEditor)
                RPCSerializer.TryReadRegisteredAssemblies(k_RegisteredAssembliesJsonPath, out registeredAssemblies);
            else RPCSerializer.TryDeserializeRegisteredAssemblies(m_SerializedRegisteredAssemblies, out registeredAssemblies);

            if (Application.isEditor)
                RPCSerializer.ReadRPCStubs(k_RPCStubsPath, out serializedRPCs, out serializedStagedMethods);
            else RPCSerializer.TryDeserializeRPCStubsJson(m_JSONString, out serializedRPCs, out serializedStagedMethods);

            ClusterDebug.Log($"Deserialized: (Registered Assemblies: {(registeredAssemblies != null ? registeredAssemblies.Count : 0)}, RPCs: {(serializedRPCs != null ? serializedRPCs.Length : 0)})");
        }

        private bool TryRegisterDeserializedMethod (
            ushort rpcId, 
            bool overrideRPCExecutionStage,
            RPCExecutionStage rpcExecutionStage, 
            MethodInfo methodInfo)
        {
            if (MethodRegistered(methodInfo))
                return false;

            var rpcMethodInfo = new RPCMethodInfo(
                rpcId, 
                overrideRPCExecutionStage,
                rpcExecutionStage, 
                methodInfo, 
                usingWrapper: WrapperUtils.IsWrapper(methodInfo.DeclaringType));

            return TryRegisterRPC(ref rpcMethodInfo);
        }

        private bool TryCreateRPCMethodInfoForStagedMethod (MethodInfo stagedMethod, out RPCMethodInfo rpcMethodInfo)
        {
            if (m_MethodUniqueIdToRPCId.TryGetValue(stagedMethod, out var rpcId))
            {
                ClusterDebug.LogError($"Attempted to re-register already registered method as an RPC: \"{stagedMethod.Name}\" delcared in: \"{stagedMethod.DeclaringType.FullName}\".");
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            if (!m_IDManager.TryPopId(out rpcId))
            {
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            rpcMethodInfo = new RPCMethodInfo(
                rpcId, 
                false,
                RPCExecutionStage.Automatic, 
                stagedMethod, 
                usingWrapper: WrapperUtils.IsWrapper(stagedMethod.DeclaringType));
            return true;
        }

        private bool TryGetWrapperMethod (MethodInfo stagedMethod, out MethodInfo wrapperMethod)
        {
            var wrapperTypeName = WrapperUtils.GetWrapperFullName(stagedMethod.DeclaringType);
            wrapperMethod = null;

            if (!ReflectionUtils.TryFindTypeByNamespaceAndName(WrapperUtils.WrapperNamespace, WrapperUtils.GetWrapperName(stagedMethod.DeclaringType), out var wrapperType))
            {
                ClusterDebug.LogError($"Unable to find wrapper type: \"{wrapperTypeName}\", verify whether the wrapper exists in the Assembly-CSharp assembly.");
                return false;
            }

            if (!ReflectionUtils.TryFindMethodWithMatchingSignature(wrapperType, stagedMethod, out wrapperMethod))
            {
                ClusterDebug.LogError($"Unable to find wrapper method: \"{stagedMethod.Name}\" in wrapper type: \"{stagedMethod.DeclaringType.Name}\", verify whether the wrapper method equals the target method.");
                return false;
            }

            return true;
        }

        private void DeserializeStagedMethods (
            List<Assembly> registeredAssemblies,
            SerializedMethod[] serializedStagedMethods)
        {
            var list = new List<RPCMethodInfo>();
            for (int i = 0; i < serializedStagedMethods.Length; i++)
            {
                if (!RPCSerializer.TryDeserializeMethodInfo(serializedStagedMethods[i], out var stagedMethod))
                {
                    ClusterDebug.LogError($"Unable to deserialize method: \"{serializedStagedMethods[i].methodName}\", declared in type: \"{serializedStagedMethods[i].declaryingTypeName}\", if the method has renamed, you can use the {nameof(ClusterRPC)} attribute with the formarlySerializedAs parameter to insure that the method is deserialized properly.");
                    continue;
                }

                RPCMethodInfo rpcMethodInfo;
                if (serializedStagedMethods[i].declaringAssemblyIsPostProcessable)
                    if (TryCreateRPCMethodInfoForStagedMethod(stagedMethod, out rpcMethodInfo))
                        list.Add(rpcMethodInfo);

                else if (TryGetWrapperMethod(stagedMethod, out var newStagedMethod))
                    if (TryCreateRPCMethodInfoForStagedMethod(newStagedMethod, out rpcMethodInfo))
                        list.Add(rpcMethodInfo);
            }

            var orderedList = list.OrderBy(item => item.rpcId);
            foreach (var rpcMethodInfo in orderedList)
            {
                var rpc = rpcMethodInfo;
                TryRegisterRPC(ref rpc);
            }
        }

        private MethodInfo[] cachedMethodsWithRPCAttribute = null;
        private void CacheMethodsWithRPCAttribute ()
        {
            if (cachedMethodsWithRPCAttribute != null)
                return;

            ReflectionUtils.TryGetAllMethodsWithAttribute<ClusterRPC>(out cachedMethodsWithRPCAttribute);
        }

        private void SearchForSerializedRPCWithFormerName (
            SerializedRPC[] serializedRPCs, 
            MethodInfo methodInfo, 
            ClusterRPC rpcMethodAttribute, 
            out SerializedRPC ? nullableSerializedRPC)
        {
            // Search for the serialized RPC with the former name.
            if (serializedRPCs != null)
            {
                for (int i = 0; i < serializedRPCs.Length; i++)
                {
                    if (serializedRPCs[i].method.methodName != methodInfo.Name && serializedRPCs[i].method.methodName != rpcMethodAttribute.formarlySerializedAs)
                        continue;

                    nullableSerializedRPC = serializedRPCs[i];
                    return;
                }
            }

            nullableSerializedRPC = null;
        }

        private bool TryDetermineRPCId (ClusterRPC rpcMethodAttribute, SerializedRPC ? nullableSerializedRPC, out ushort rpcId)
        {
            rpcId = 0;

            if (rpcMethodAttribute.rpcId != -1) // Are we overriding the RPC id in the method attribute?
                rpcId = (ushort)rpcMethodAttribute.rpcId;

            else if (nullableSerializedRPC != null) // Is this method already registered under a serialized RPC id?
                rpcId = nullableSerializedRPC.Value.rpcId;

            // Can we push this ID into the ID manager as an ID in use? If it's being used, can we pop a new one?
            if (!m_IDManager.TryPushOrPopId(ref rpcId))
                return false;

            return true;
        }

        private RPCExecutionStage DetermineExecutionStage (ClusterRPC rpcMethodAttribute, SerializedRPC ? nullableSerializedRPC, out bool overrideRPCExecutionStage)
        {
            overrideRPCExecutionStage = nullableSerializedRPC != null && nullableSerializedRPC.Value.overrideRPCExecutionStage;
            return overrideRPCExecutionStage ? (RPCExecutionStage)nullableSerializedRPC.Value.rpcExecutionStage : rpcMethodAttribute.rpcExecutionStage;
        }

        private void TryPollMethodWithRPCAttribute (
            SerializedRPC[] serializedRPCs, 
            List<string> renamedRPCs, 
            DeserializedRPCList list, 
            MethodInfo methodInfo)
        {
            var rpcMethodAttribute = methodInfo.GetCustomAttribute<ClusterRPC>();

            if (!ReflectionUtils.DetermineIfMethodIsRPCCompatible(methodInfo))
            {
                ClusterDebug.LogError($"Unable to register method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType}\", one or more of the method's parameters is not a value type or one of the parameter's members is not a value type.");
                return;
            }

            SearchForSerializedRPCWithFormerName(serializedRPCs, methodInfo, rpcMethodAttribute, out var nullableSerializedRPC);
            if (!TryDetermineRPCId(rpcMethodAttribute, nullableSerializedRPC, out var rpcId))
                return;

            var rpcExecutionStage = DetermineExecutionStage(rpcMethodAttribute, nullableSerializedRPC, out var overrideRPCExecutionStage);
            list.Add((rpcId, overrideRPCExecutionStage, rpcExecutionStage, methodInfo));

            if (!string.IsNullOrEmpty(rpcMethodAttribute.formarlySerializedAs))
                renamedRPCs.Add(rpcMethodAttribute.formarlySerializedAs);
        }

        private void RegisterDeserializedRPCs (DeserializedRPCList list)
        {
            // Sort the RPCs by the RPC ID.
            var orderedList = list.OrderBy(item => item.rpcId);
            foreach (var methodData in orderedList) // After we've sorted, loop through our methods and register them as RPCs.
                TryRegisterDeserializedMethod(
                    methodData.rpcId,
                    methodData.overrideRPCExecutionStage,
                    methodData.rpcExecutionStage,
                    methodData.methodInfo);
        }
        
        private void PollMethodsWithRPCAttribute (
            SerializedRPC[] serializedRPCs, 
            List<string> renamedRPCs)
        {
            CacheMethodsWithRPCAttribute();

            var list = new DeserializedRPCList();
            foreach (var methodInfo in cachedMethodsWithRPCAttribute)
                TryPollMethodWithRPCAttribute(serializedRPCs, renamedRPCs, list, methodInfo);

            RegisterDeserializedRPCs(list);
        }

        private void TryPollSerializedRPC (
            List<Assembly> registeredAssemblies,
            ref SerializedRPC serializedRPC,
            DeserializedRPCList list)
        {
            if (!RPCSerializer.TryDeserializeMethodInfo(serializedRPC, out var rpcExecutionStage, out var methodInfo))
                return;

            if (!registeredAssemblies.Contains(methodInfo.Module.Assembly))
                return;

            var rpcMethodAttribute = methodInfo.GetCustomAttribute<ClusterRPC>();
            if (!TryDetermineRPCId(rpcMethodAttribute, serializedRPC, out var rpcId))
                return;

            rpcExecutionStage = DetermineExecutionStage(rpcMethodAttribute, serializedRPC, out serializedRPC.overrideRPCExecutionStage);
            list.Add((rpcId, serializedRPC.overrideRPCExecutionStage, rpcExecutionStage, methodInfo));
        }

        private void DeserializeRegisteredRPCMethods (
            List<Assembly> registeredAssemblies,
            SerializedRPC[] serializedRPCs,
            List<string> renamedRPCs)
        {
            var list = new DeserializedRPCList();

            for (int i = 0; i < serializedRPCs.Length; i++)
            {
                if (renamedRPCs.Contains(serializedRPCs[i].method.methodName))
                    return;

                TryPollSerializedRPC(registeredAssemblies, ref serializedRPCs[i], list);
            }

            RegisterDeserializedRPCs(list);
        }

        private void AssociateRPCIdsWithAssemblies ()
        {
            for (int rpcId = 0; rpcId <= m_LargestRPCId; rpcId++)
            {
                var rpc = m_RPCs[rpcId];
                if (rpc == null)
                    continue;

                var rpcMethodInfo = rpc.Value;

                if (!RPCInterfaceRegistry.TryCreateImplementationInstance(rpcMethodInfo.methodInfo.Module.Assembly, out var assemblyIndex))
                    continue;

                ClusterDebug.Log($"Associating method: (RPC ID: {rpcId}, Name: {rpcMethodInfo.methodInfo.Name}, Type: {rpcMethodInfo.methodInfo.DeclaringType.Name}) with assembly: \"{rpcMethodInfo.methodInfo.Module.Assembly.GetName().Name}\" at index: {assemblyIndex}");
                m_AssemblyIndexLookUp[rpcId] = (ushort)(assemblyIndex + 1);
            }
        }

        private void Deserialize ()
        {
            DeserializeData(out var registeredAssemblies, out var serializedRPCs, out var serializedStagedMethods);
            m_IDManager.Clear();

            List<string> renamedRPCs = new List<string>();
            
            PollMethodsWithRPCAttribute(
                serializedRPCs,
                renamedRPCs);

            DeserializeRegisteredRPCMethods(
                registeredAssemblies,
                serializedRPCs,
                renamedRPCs);

            DeserializeStagedMethods(
                registeredAssemblies,
                serializedStagedMethods);

            if (RPCCount == 0)
            {
                m_MethodUniqueIdToRPCId.Clear();
                return;
            }

            if (Application.isEditor)
                SerializeAll();

            m_Deserialized = true;
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
