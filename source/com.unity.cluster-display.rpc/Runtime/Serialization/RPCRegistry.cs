using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Assembly = System.Reflection.Assembly;
using DeserializedRPCList = System.Collections.Generic.List<(string rpcHash, bool overrideRPCExecutionStage, Unity.ClusterDisplay.RPC.RPCExecutionStage rpcExecutionStage, System.Reflection.MethodInfo methodInfo)>; 

namespace Unity.ClusterDisplay.RPC
{
    /// <summary>
    /// This class operate as a cache and registry for managing, serializing and deserializing RPCs.
    /// </summary>
    [CreateAssetMenu(fileName = "RPCRegistry", menuName = "Cluster Display/RPC Registry")]
    [DefaultExecutionOrder(int.MinValue)]
    #if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
    #endif
    internal partial class RPCRegistry : SingletonScriptableObject<RPCRegistry>, ISerializationCallbackReceiver
    {
        public const int MaxRPCCount = 256;
        static RPCMethodInfo?[] m_RPCs = new RPCMethodInfo?[MaxRPCCount];

        static int m_RPCCount;
        public static int RPCCount => m_RPCCount;
        static int m_LargestRPCId;

        // LUT for retreiving and RPC ID by it's signature in in the form of a hash. 
        static readonly Dictionary<string, ushort> m_MethodUniqueIdToRPCId = new Dictionary<string, ushort>();

        // LUT for retrieving a list of RPC ID defined by type. 
        static readonly Dictionary<System.Type, List<ushort>> m_TypeToRPCIds = new Dictionary<System.Type, List<ushort>>();

        static readonly string[] rpcHashs = new string[ushort.MaxValue];

        public static bool RPCIdToRPCHash(ushort rpcId, out string rpcHash) => !string.IsNullOrEmpty(rpcHash = rpcHashs[rpcId]);
        public static bool RPCHashToRPCId(string rpcHash, out ushort rpcId)
        {
            if (string.IsNullOrEmpty(rpcHash))
            {
                rpcId = 0;
                return false;
            }
            
            return m_MethodUniqueIdToRPCId.TryGetValue(rpcHash, out rpcId);
        }
        
        public static string MethodInfoToRPCHash(MethodInfo methodInfo) => ReflectionUtils.ComputeMethodHash(methodInfo);
        
        public static bool TryGetRPCHash(ushort rpcId, out string rpcHash) => !string.IsNullOrEmpty(rpcHash = rpcHashs[rpcId]);

        // The RPC stubs file is a binary file where we store the serialized RPCs. This needs to be stored
        // as a file so it can be read by the ILPostProcessor which is a different process entirely.
        public const string k_RPCStubsFileName = "RPCStubs.bin";

        // We store this stubs file in resources so that it's included in the build as well.
        public readonly string k_RPCStubsResourcesPath = $"Assets/Resources/ClusterDisplay/{k_RPCStubsFileName}";
        public const string k_RPCStagedPath = "./Temp/ClusterDisplay/RPCStaged.bin";

        public delegate void OnTriggerRecompileDelegate();

        public delegate bool OnAddWrappableMethodDelegate(MethodInfo methodInfo);
        public delegate bool OnAddWrappablePropertyDelegate(PropertyInfo propertyInfo);

        public delegate bool OnRemoveMethodWrapperDelegate(MethodInfo methodInfo);
        public delegate bool OnRemovePropertyWrapperDelegate(PropertyInfo propertyInfo);

        public static OnTriggerRecompileDelegate onTriggerRecompile;
        public static OnAddWrappableMethodDelegate onAddWrappableMethod;
        public static OnAddWrappablePropertyDelegate onAddWrappableProperty;

        public static OnRemoveMethodWrapperDelegate onRemoveMethodWrapper;
        public static OnRemovePropertyWrapperDelegate onRemovePropertyWrapper;

        bool m_Deserialized = false;
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
        static InitializationDelegate onInitialized;

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
        internal static void Initialize ()
        {
            ClusterDebug.Log($"Initializing {nameof(RPCRegistry)}");

            if (!TryGetInstance(out var instance, throwError: true))
                return;

            if (!instance.m_Deserialized)
                instance.Deserialize();

            instance.AssociateRPCIdsWithAssemblies();

            onInitialized?.Invoke();
            onInitialized = null;
        }

        public static bool MethodRegistered(ushort rpcId)
        {
            if (rpcId < 0 || rpcId >= m_RPCs.Length)
            {
                ClusterDebug.LogError($"RPC ID: {rpcId} is out of range: (0 - {m_RPCs.Length})");
                return false;
            }
            
            return m_RPCs[rpcId] != null;
        }
        
        public static bool MethodRegistered(MethodInfo methodInfo) => m_MethodUniqueIdToRPCId.ContainsKey(MethodInfoToRPCHash(methodInfo));

        public static bool TryGetRPC(ushort rpcId, out RPCMethodInfo rpcMethodInfo)
        {
            if (!MethodRegistered(rpcId))
            {
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }
                
            rpcMethodInfo = m_RPCs[rpcId].Value;
            return true;
        }

        public static bool TryGetRPC(MethodInfo methodInfo, out RPCMethodInfo rpcMethodInfo)
        {
            if (!m_MethodUniqueIdToRPCId.TryGetValue(MethodInfoToRPCHash(methodInfo), out var rpcId))
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

        void RegisterRPCIdWithMethodUniqueId (string rpcHash, ushort rpcId)
        {
            if (m_MethodUniqueIdToRPCId.ContainsKey(rpcHash))
            {
                m_MethodUniqueIdToRPCId[rpcHash] = rpcId;
                return;
            }

            m_MethodUniqueIdToRPCId.Add(rpcHash, rpcId);
            rpcHashs[rpcId] = rpcHash;
        }
        
        void RegisterRPCIdWithType (System.Type type, ushort rpcId)
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
        bool TryRegisterRPC (ref RPCMethodInfo rpcMethodInfo)
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

            ClusterDebug.Log($"Registering new RPC: (Name: {rpcMethodInfo.methodInfo.Name}, RPC ID: {rpcMethodInfo.rpcId}, RPC Execution Stage: {rpcMethodInfo.rpcExecutionStage}).");

            m_RPCs[rpcMethodInfo.rpcId] = rpcMethodInfo;
            RegisterRPCIdWithMethodUniqueId(rpcMethodInfo.rpcHash, rpcMethodInfo.rpcId);
            RegisterRPCIdWithType(rpcMethodInfo.methodInfo.DeclaringType, rpcMethodInfo.rpcId);

            m_RPCCount++;
            return true;
        }

        public bool TryUpdateRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            ClusterDebug.Log($"Updating RPC: (Name: {rpcMethodInfo.methodInfo.Name}, RPC ID: {rpcMethodInfo.rpcId}, RPC Execution Stage: {rpcMethodInfo.rpcExecutionStage}).");
            
            m_RPCs[rpcMethodInfo.rpcId] = rpcMethodInfo;
            RegisterRPCIdWithMethodUniqueId(rpcMethodInfo.rpcHash, rpcMethodInfo.rpcId);
            RegisterRPCIdWithType(rpcMethodInfo.methodInfo.DeclaringType, rpcMethodInfo.rpcId);

            SetDirtyAndRecompile();
            return true;
        }

        bool UnregisterRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            m_RPCs[rpcMethodInfo.rpcId] = null;
            m_MethodUniqueIdToRPCId.Remove(rpcMethodInfo.rpcHash);

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

        void UnregisterRPCOnDeserialize (ushort rpcId)
        {
            m_RPCs[rpcId] = default(RPCMethodInfo);
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

        bool TryStageNonPostProcessableMethod (MethodInfo methodInfo)
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

        bool TryAddPostProcessableRPC (MethodInfo methodInfo)
        {
            var rpcHash = MethodInfoToRPCHash(methodInfo);
            if (m_MethodUniqueIdToRPCId.TryGetValue(rpcHash, out var rpcId))
            {
                ClusterDebug.LogError($"Cannot add RPC method: \"{methodInfo.Name}\" from declaring type: \"{methodInfo.DeclaringType}\", it has already been registered!");
                return false;
            }

            var rpcMethodInfo = new RPCMethodInfo(
                rpcHash, 
                rpcId,
                false,
                RPCExecutionStage.Automatic, 
                methodInfo, 
                usingWrapper: WrapperUtils.IsWrapper(methodInfo.DeclaringType));

            return TryRegisterRPC(ref rpcMethodInfo);
        }

        bool TryStageNonPostProcessableMethod (PropertyInfo propertyInfo)
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
            var rpcHash = MethodInfoToRPCHash(methodInfo);
            if (!m_MethodUniqueIdToRPCId.TryGetValue(rpcHash, out var rpcId))
                return;
            UnmarkRPC(rpcId);
        }

        /// <summary>
        /// When we are in the editor and we've added and RPC in the UI, we need to trigger a recompile.
        /// </summary>
        void SetDirtyAndRecompile ()
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

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        void SerializeAll () => SerializeRPCStubs();

        void SerializeRPCStubs ()
        {
            List<RPCStub> list = new List<RPCStub>();
            for (int rpcId = 0; rpcId <= m_LargestRPCId; rpcId++)
            {
                if (m_RPCs[rpcId] == null)
                    continue;

                var rpc = m_RPCs[rpcId].Value;
                list.Add(RPCStub.Create(ref rpc));
            }

            RPCSerializer rpcSerializer = new RPCSerializer(new CodeGenDebug("RPCRegistry", false));
            if (!rpcSerializer.SerializeRPCs(
                list.ToArray(), 
                stagedMethods.Values.ToArray(), 
                out var m_RPCStubBytes, 
                out var stagedRPCBytes))
                return;

            #if UNITY_EDITOR
            rpcSerializer.WriteSerializedRPCs(k_RPCStubsResourcesPath, k_RPCStagedPath, m_RPCStubBytes, stagedRPCBytes);
            #endif

            m_IsDirty = true;
        }

        void DeserializeData (RPCSerializer rpcSerializer, out RPCStub[] serializedRPCs, out RPMethodStub[] serializedStagedMethods)
        {
            serializedRPCs = new RPCStub[0];
            serializedStagedMethods = new RPMethodStub[0];

            byte[] serializedRPCBytes = null;
            byte[] serializedStagedMethodBytes = null;

            if (Application.isEditor)
            {
                try
                {
                    if (File.Exists(k_RPCStubsResourcesPath))
                        serializedRPCBytes = File.ReadAllBytes(k_RPCStubsResourcesPath);
                    
                    if (File.Exists(k_RPCStagedPath))
                        serializedStagedMethodBytes = File.ReadAllBytes(k_RPCStagedPath);
                }
                
                catch (Exception e)
                {
                    ClusterDebug.LogException(e);
                }
            }

            else
            {
                var textAsset = Resources.Load<TextAsset>(k_RPCStubsFileName);
                if (textAsset != null)
                    serializedRPCBytes = textAsset.bytes;
                
                textAsset = Resources.Load<TextAsset>(k_RPCStagedPath);
                if (textAsset != null)
                    serializedStagedMethodBytes = textAsset.bytes;
            }
            
            if (serializedRPCBytes != null && serializedRPCBytes.Length > 0)
                rpcSerializer.BytesToRPCs(serializedRPCBytes, out serializedRPCs);
            if (serializedStagedMethodBytes != null && serializedStagedMethodBytes.Length > 0)
                rpcSerializer.BytesToRPCs(serializedStagedMethodBytes, out serializedStagedMethods);
            
            /*
            if (Application.isEditor)
                RPCSerializer.ReadAllRPCs(
                    k_RPCStubsPath, 
                    k_RPCStagedPath, 
                    out serializedRPCs, 
                    out serializedStagedMethods,
                    logMissingFile: false);
            else
            {
                RPCSerializer.BytesToRPCs(m_RPCStubBytes, out serializedRPCs);
                serializedStagedMethods = null;
            }
            */

            ClusterDebug.Log($"Deserialized RPC count: {(serializedRPCs != null ? serializedRPCs.Length : 0)})");
        }

        bool TryRegisterDeserializedMethod (
            string rpcHash,
            ushort rpcId, 
            bool overrideRPCExecutionStage,
            RPCExecutionStage rpcExecutionStage, 
            MethodInfo methodInfo)
        {
            if (MethodRegistered(methodInfo))
                return false;

            var rpcMethodInfo = new RPCMethodInfo(
                rpcHash,
                rpcId, 
                overrideRPCExecutionStage,
                rpcExecutionStage, 
                methodInfo, 
                usingWrapper: WrapperUtils.IsWrapper(methodInfo.DeclaringType));

            return TryRegisterRPC(ref rpcMethodInfo);
        }

        bool TryCreateRPCMethodInfoForStagedMethod (MethodInfo stagedMethod, out RPCMethodInfo rpcMethodInfo)
        {
            if (m_MethodUniqueIdToRPCId.TryGetValue(MethodInfoToRPCHash(stagedMethod), out var rpcId))
            {
                ClusterDebug.LogError($"Attempted to re-register already registered method as an RPC: \"{stagedMethod.Name}\" delcared in: \"{stagedMethod.DeclaringType.FullName}\".");
                rpcMethodInfo = default(RPCMethodInfo);
                return false;
            }

            var rpcHash = rpcHashs[rpcId];

            rpcMethodInfo = new RPCMethodInfo(
                rpcHash,
                rpcId, 
                false,
                RPCExecutionStage.Automatic, 
                stagedMethod, 
                usingWrapper: WrapperUtils.IsWrapper(stagedMethod.DeclaringType));
            return true;
        }

        bool TryGetWrapperMethod (MethodInfo stagedMethod, out MethodInfo wrapperMethod)
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

        void DeserializeStagedMethods (
            RPCSerializer rpcSerializer,
            RPMethodStub[] serializedStagedMethods)
        {
            var list = new List<RPCMethodInfo>();
            for (int i = 0; i < serializedStagedMethods.Length; i++)
            {
                if (!rpcSerializer.TryDeserializeMethodInfo(serializedStagedMethods[i], out var stagedMethod))
                {
                    ClusterDebug.LogError($"Unable to deserialize method: \"{serializedStagedMethods[i].methodName}\", declared in type: \"{serializedStagedMethods[i].declaringTypeName}\", if the method has renamed, you can use the {nameof(ClusterRPC)} attribute with the formarlySerializedAs parameter to insure that the method is deserialized properly.");
                    continue;
                }

                RPCMethodInfo rpcMethodInfo;
                if (serializedStagedMethods[i].declaringAssemblyIsPostProcessable)
                {
                    if (TryCreateRPCMethodInfoForStagedMethod(stagedMethod, out rpcMethodInfo))
                        list.Add(rpcMethodInfo);
                }

                else if (TryGetWrapperMethod(stagedMethod, out var newStagedMethod))
                {
                    if (TryCreateRPCMethodInfoForStagedMethod(newStagedMethod, out rpcMethodInfo))
                        list.Add(rpcMethodInfo);
                }
            }

            var orderedList = list.OrderBy(item => item.rpcId);
            foreach (var rpcMethodInfo in orderedList)
            {
                var rpc = rpcMethodInfo;
                TryRegisterRPC(ref rpc);
            }
        }

        MethodInfo[] cachedMethodsWithRPCAttribute = null;
        void CacheMethodsWithRPCAttribute ()
        {
            if (cachedMethodsWithRPCAttribute != null)
                return;

            ReflectionUtils.TryGetAllMethodsWithAttribute<ClusterRPC>(out cachedMethodsWithRPCAttribute);
        }

        void SearchForSerializedRPCWithFormerName (
            RPCStub[] serializedRPCs, 
            MethodInfo methodInfo, 
            ClusterRPC rpcMethodAttribute, 
            out RPCStub ? nullableSerializedRPC)
        {
            // Search for the serialized RPC with the former name.
            if (serializedRPCs != null)
            {
                for (int i = 0; i < serializedRPCs.Length; i++)
                {
                    if (serializedRPCs[i].methodStub.methodName != methodInfo.Name && serializedRPCs[i].methodStub.methodName != rpcMethodAttribute.formarlySerializedAs)
                        continue;

                    nullableSerializedRPC = serializedRPCs[i];
                    return;
                }
            }

            nullableSerializedRPC = null;
        }

        RPCExecutionStage DetermineExecutionStage (ClusterRPC rpcMethodAttribute, RPCStub ? nullableSerializedRPC, out bool overrideRPCExecutionStage)
        {
            overrideRPCExecutionStage = nullableSerializedRPC != null && nullableSerializedRPC.Value.overrideRPCExecutionStage;
            return overrideRPCExecutionStage ? (RPCExecutionStage)nullableSerializedRPC.Value.rpcExecutionStage : rpcMethodAttribute.rpcExecutionStage;
        }

        bool ValidateRPCHash(MethodInfo methodInfo, string rpcHash)
        {
            if (!string.IsNullOrEmpty(rpcHash))
                return true;
            ClusterDebug.LogError($"The method: \"{methodInfo.Name}\" declared in: \"{methodInfo.DeclaringType.Name}\" has a {nameof(ClusterRPC)}. However, the hash for the method signature is empty which indicates that this method has NOT been IL post processed.");
            return false;
        }

        void TryPollMethodWithRPCAttribute (
            RPCStub[] serializedRPCs, 
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
            var rpcHash = rpcMethodAttribute.rpcHash;
            if (!ValidateRPCHash(methodInfo, rpcHash))
                return;
            
            if (m_MethodUniqueIdToRPCId.ContainsKey(rpcHash))
                return;

            var rpcExecutionStage = DetermineExecutionStage(rpcMethodAttribute, nullableSerializedRPC, out var overrideRPCExecutionStage);
            list.Add((rpcHash, overrideRPCExecutionStage, rpcExecutionStage, methodInfo));

            if (!string.IsNullOrEmpty(rpcMethodAttribute.formarlySerializedAs))
                renamedRPCs.Add(rpcMethodAttribute.formarlySerializedAs);
        }

        void RegisterDeserializedRPCs (DeserializedRPCList list)
        {
            // Sort the RPCs by the RPC ID.
            var orderedList = list.OrderBy(item => item.rpcHash);
            ushort rpcId = (ushort)m_LargestRPCId;
            foreach (var methodData in orderedList) // After we've sorted, loop through our methods and register them as RPCs.
            {
                if (m_MethodUniqueIdToRPCId.ContainsKey(methodData.rpcHash))
                    continue;
                    
                TryRegisterDeserializedMethod(
                    methodData.rpcHash,
                    rpcId++,
                    methodData.overrideRPCExecutionStage,
                    methodData.rpcExecutionStage,
                    methodData.methodInfo);
            }

            m_LargestRPCId = rpcId;
        }
        
        void PollMethodsWithRPCAttribute (
            RPCStub[] serializedRPCs, 
            List<string> renamedRPCs)
        {
            CacheMethodsWithRPCAttribute();

            DeserializedRPCList list = new DeserializedRPCList();
            foreach (var methodInfo in cachedMethodsWithRPCAttribute)
                TryPollMethodWithRPCAttribute(serializedRPCs, renamedRPCs, list, methodInfo);

            RegisterDeserializedRPCs(list);
        }

        void TryPollSerializedRPC (
            RPCSerializer rpcSerializer,
            ref RPCStub rpcStub,
            DeserializedRPCList list)
        {
            if (!rpcSerializer.TryDeserializeMethodInfo(rpcStub, out var rpcExecutionStage, out var methodInfo))
                return;

            var rpcMethodAttribute = methodInfo.GetCustomAttribute<ClusterRPC>();
            var rpcHash = rpcMethodAttribute.rpcHash;
            if (!ValidateRPCHash(methodInfo, rpcHash))
                return;
            
            rpcExecutionStage = DetermineExecutionStage(rpcMethodAttribute, rpcStub, out rpcStub.overrideRPCExecutionStage);
            list.Add((rpcHash, rpcStub.overrideRPCExecutionStage, rpcExecutionStage, methodInfo));
        }

        void DeserializeRegisteredRPCMethods (
            RPCSerializer rpcSerializer,
            RPCStub[] serializedRPCs,
            List<string> renamedRPCs)
        {
            DeserializedRPCList list = new DeserializedRPCList();

            for (int i = 0; i < serializedRPCs.Length; i++)
            {
                if (renamedRPCs.Contains(serializedRPCs[i].methodStub.methodName))
                    continue;

                TryPollSerializedRPC(rpcSerializer, ref serializedRPCs[i], list);
            }

            RegisterDeserializedRPCs(list);
        }

        void AssociateRPCIdsWithAssemblies ()
        {
            for (int rpcId = 0; rpcId <= m_LargestRPCId; rpcId++)
            {
                var rpc = m_RPCs[rpcId];
                if (rpc == null)
                    continue;

                var rpcMethodInfo = rpc.Value;
                if (rpcId == 52)
                    Debug.Log("TEST");

                if (!RPCAssemblyRegistry.AssociateRPCWithAssembly(rpcMethodInfo.methodInfo, rpcId))
                    continue;
            }
        }

        void Deserialize ()
        {
            RPCSerializer rpcSerializer = new RPCSerializer(new CodeGenDebug("RPCRegistry", false));
            DeserializeData(rpcSerializer, out var serializedRPCs, out var serializedStagedMethods);
            List<string> renamedRPCs = new List<string>();
            
            PollMethodsWithRPCAttribute(
                serializedRPCs,
                renamedRPCs);

            DeserializeRegisteredRPCMethods(
                rpcSerializer,
                serializedRPCs,
                renamedRPCs);

            DeserializeStagedMethods(
                rpcSerializer,
                serializedStagedMethods);

            AssociateRPCIdsWithAssemblies();

            if (RPCCount == 0)
            {
                m_MethodUniqueIdToRPCId.Clear();
                return;
            }

            if (Application.isEditor)
                SerializeAll();

            m_Deserialized = true;
        }

        void Serialize ()
        {
            if (!m_IsDirty)
                return;

            SerializeAll();
            m_IsDirty = false;
        }

        bool m_IsSerializing = false;
        bool m_IsDirty = false;

        public bool IsSerializing => m_IsSerializing;
        void ToggleSerializationState(bool isSerializing) => this.m_IsSerializing = isSerializing;

        // Where serialization of RPCs begins.
        public void OnBeforeSerialize()
        {
            ToggleSerializationState(true);
            Serialize();
            ToggleSerializationState(false);
        }

        // Where deserialization of RPCs begins.
        public void OnAfterDeserialize()
        {
            ToggleSerializationState(true);
            Deserialize();
            ToggleSerializationState(false);
        }
    }
}
