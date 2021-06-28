using System.Linq;
using System;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Scripting;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
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
        private static readonly Dictionary<int, ushort> m_RPCLut = new Dictionary<int, ushort>();
        private static readonly Dictionary<ushort, RPCMethodInfo> m_RPCs = new Dictionary<ushort, RPCMethodInfo>();
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

        public static bool RPCRegistered(ushort rpcId) => m_RPCs.ContainsKey(rpcId);
        public static bool TryGetRPC(ushort rpcId, out RPCMethodInfo rpcMethodInfo) => m_RPCs.TryGetValue(rpcId, out rpcMethodInfo);

        public static void SetupInstances ()
        {
            foreach (var rpcIdAndInfo in m_RPCs)
            {
                if (!RPCInterfaceRegistry.TryCreateImplementationInstance(rpcIdAndInfo.Value.methodInfo.Module.Assembly, out var assemblyIndex))
                    continue;
                assemblyIndexLookUp.Add(rpcIdAndInfo.Key, assemblyIndex);
            }
        }

        private void RegisterAssembly (Assembly assembly)
        {
            var name = assembly.FullName;
            if (targetAssemblies.Contains(assembly))
                return;
            targetAssemblies.Add(assembly);
            WriteRegisteredAssemblies();
        }

        private void UnregisterAssembly (Assembly assembly)
        {
            var name = assembly.FullName;
            if (!targetAssemblies.Contains(assembly))
                return;
            targetAssemblies.Remove(assembly);
            WriteRegisteredAssemblies();
        }

        private bool AssemblyIsRegistered(Assembly assembly) => targetAssemblies.Contains(assembly);

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

            if (m_RPCLut.ContainsKey(rpcMethodInfo.methodInfo.MetadataToken))
                m_RPCLut[rpcMethodInfo.methodInfo.MetadataToken] = rpcMethodInfo.rpcId;
            else m_RPCLut.Add(rpcMethodInfo.methodInfo.MetadataToken, rpcMethodInfo.rpcId); 

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        public void UpdateRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            if (!RPCSerializer.TryCreateSerializableRPC(ref rpcMethodInfo, out var serializedRPC))
                return;

            m_RPCs[rpcMethodInfo.rpcId] = rpcMethodInfo;

            if (m_RPCLut.ContainsKey(rpcMethodInfo.methodInfo.MetadataToken))
                m_RPCLut[rpcMethodInfo.methodInfo.MetadataToken] = rpcMethodInfo.rpcId;
            else m_RPCLut.Add(rpcMethodInfo.methodInfo.MetadataToken, rpcMethodInfo.rpcId); 

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void UnregisterRPC (ref RPCMethodInfo rpcMethodInfo)
        {
            m_RPCs.Remove(rpcMethodInfo.rpcId);
            m_RPCLut.Remove(rpcMethodInfo.methodInfo.MetadataToken);
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
            RegisterRPC(ref rpcMethodInfo);

            return true;
        }

        public void RemoveRPC (ushort rpcId)
        {
            if (!TryGetRPC(rpcId, out var rpcMethodInfo))
                return;
            UnregisterRPC(ref rpcMethodInfo);
        }

        /// <summary>
        /// When we are in the editor and we've added and RPC in the UI, we need to trigger a recompile.
        /// </summary>
        private void SetDirtyAndRecompile ()
        {
            #if UNITY_EDITOR
            if (IsSerializing)
                return;

            // Debug.Log($"Changed RPC Registry, recompiling...");
            foreach (var assembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player))
                Debug.Log(assembly.name);

            EditorUtility.SetDirty(this);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            m_IsDirty = true;
            #endif
        }

        public void Clear ()
        {
            m_RPCLut.Clear();
            m_RPCs.Clear();
            m_IDManager.Clear();

            #if UNITY_EDITOR
            SetDirtyAndRecompile();
            #endif
        }

        private void WriteAll ()
        {
            WriteRegisteredAssemblies();
            WriteRPCStubs();
        }

        private void WriteRPCStubs ()
        {
            List<SerializedRPC> list = new List<SerializedRPC>();
            foreach (var keyValuePair in m_RPCs)
            {
                var rpc = keyValuePair.Value;
                if (!RPCSerializer.TryCreateSerializableRPC(ref rpc, out var serializedRPC))
                    continue;
                list.Add(serializedRPC);
            }

            RPCSerializer.TryWriteRPCStubs(RPCStubsPath, list.ToArray());
        }

        private void WriteRegisteredAssemblies ()
        {
            RPCSerializer.TryWriteRegisteredAssemblies(RegisteredAssembliesJsonPath, targetAssemblies.Where(assembly => assembly != null).Select(assembly => assembly.FullName).ToArray());
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
            if (RPCRegistered(rpcId))
                return false;

            var rpcMethodInfo = new RPCMethodInfo(rpcId, rpcExecutionStage, methodInfo);
            RegisterRPC(ref rpcMethodInfo);

            return true;
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

            List<Assembly> registeredAssemblies = new List<Assembly>();
            if (!RPCSerializer.TryReadRegisteredAssemblies(RegisteredAssembliesJsonPath, out registeredAssemblies))
                return;

            RPCSerializer.TryReadRPCStubs(RPCStubsPath, out var serializedRPCs);

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
                m_RPCLut.Clear();
                return;
            }

            rpcIdsToAdd.Sort();
            ushort largestId = rpcIdsToAdd.Last();
            m_IDManager.PushSetOfIds(rpcIdsToAdd.ToArray(), largestId);

            WriteAll();
        }

        private void Serialize ()
        {
            if (!m_IsDirty)
                return;

            WriteAll();
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
