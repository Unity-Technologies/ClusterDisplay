using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.RPC
{
    [System.Serializable]
    internal struct SerializedInstanceRPCData
    {
        // [SerializeField] public ushort pipeId;
        [SerializeField] public Component instance;
        [SerializeField] public PipeConfig pipeConfig;
    }

    public partial class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>, ISerializationCallbackReceiver
    {
        [SerializeField] private SerializedInstanceRPCData[] m_SerializedInstances;
        private readonly List<Component> m_SceneInstances = new List<Component>();

        public bool Registered(Component instance) => m_SceneInstances.Contains(instance);

        private void Awake ()
        {
            ClusterDebug.Log($"Initializing {nameof(SceneObjectsRegistry)} for path: \"{gameObject.scene.path}\".");
            RPCRegistry.InitializeWhenReady(GatherSceneInstances);
        }

        internal void UpdateRPCConfig(ushort pipeId, ushort rpcId, ref RPCConfig rpcConfig)
        {
            SetRPCConfig(pipeId, rpcId, ref rpcConfig);
            SerializeRPCSceneInstances();
        }

        private void Dirty ()
        {
            #if UNITY_EDITOR
            if (!IsSerializing)
                EditorUtility.SetDirty(this);
            #endif
        }

        public bool TryRegister (Component sceneObject, System.Type type)
        {
            if (sceneObject == null)
            {
                ClusterDebug.LogError($"Refusing to register NULL instance with: \"{nameof(SceneObjectsRegistry)}\" in scene: \"{gameObject.scene.name}\".");
                return false;
            }
            
            if (!RPCRegistry.TryGetRPCsForType(type, out var rpcs, logError: false))
                return false;

            for (int ri = 0; ri < rpcs.Length; ri++)
            {
                var rpcConfig = new RPCConfig();
                RegisterWithRPCIdAndConfig(sceneObject, rpcs[ri].rpcId, ref rpcConfig);
            }

            return true;
        }

        /// <summary>
        /// Register an instance that contains an RPC so it can emit and receive cluster display events.
        /// </summary>
        public bool TryRegister<InstanceType>(InstanceType sceneObject)
            where InstanceType : Component => TryRegister(sceneObject, typeof(InstanceType));
        
        /// <summary>
        /// Register an instance that contains an RPC so it can emit and receive cluster display events.
        /// </summary>
        public bool TryUnregister<InstanceType> (InstanceType sceneObject)
            where InstanceType : Component
        {
            if (sceneObject == null)
            {
                ClusterDebug.LogError($"Refusing to unregister NULL instance with: \"{nameof(SceneObjectsRegistry)}\" in scene: \"{gameObject.scene.name}\".");
                return false;
            }

            m_SceneInstances.Remove(sceneObject);
            if (!TryUnregisterInstanceAccessor(sceneObject))
                return false;

            if (m_SceneInstances.Count == 0)
            {
                if (Application.isPlaying)
                    Destroy(this.gameObject);
                else DestroyImmediate(this.gameObject);
            }

            Dirty();
            return true;
        }

        private bool RegisterWithRPCId<InstanceType> (InstanceType sceneObject, ushort rpcId)
            where InstanceType : Component
        {
            if (!m_SceneInstances.Contains(sceneObject))
                m_SceneInstances.Add(sceneObject);

            if (!TryRegisterInstanceAccessor(sceneObject, rpcId))
                return false;
            
            Dirty();
            return true;
        }

        private bool RegisterWithRPCIdAndConfig<InstanceType> (InstanceType sceneObject, ushort rpcId, ref RPCConfig instanceRPCConfig)
            where InstanceType : Component
        {
            if (!m_SceneInstances.Contains(sceneObject))
                m_SceneInstances.Add(sceneObject);

            if (!TryRegisterInstanceAccessor(sceneObject, rpcId, ref instanceRPCConfig))
                return false;
            
            Dirty();
            return true;
        }

        private void SerializeRPCSceneInstances ()
        {
            if (m_SceneInstances.Count == 0)
                return;

            List<SerializedInstanceRPCData> serializedInstances = new List<SerializedInstanceRPCData>();

            for (int i = 0; i < m_SceneInstances.Count; i++)
            {
                if (m_SceneInstances[i] == null)
                    continue;

                if (!TryGetPipeId(m_SceneInstances[i], out var pipeId))
                    continue;

                var pipeConfig = GetPipeConfig(pipeId);
                serializedInstances.Add(new SerializedInstanceRPCData
                {
                    // pipeId = pipeId,
                    instance = m_SceneInstances[i],
                    pipeConfig = pipeConfig
                });
            }

            m_SerializedInstances = serializedInstances.ToArray();
            Dirty();
        }
        
        public void OnAfterDeserialize() {}
        public void OnBeforeSerialize() 
        {
            IsSerializing = true;
            SerializeRPCSceneInstances();
            IsSerializing = false;
        }

        private void GatherSceneInstances ()
        {
            if (m_SerializedInstances == null)
                return;

            ClusterDebug.Log($"Gathering scene instances in scene: \"{gameObject.scene.path}\".");
            m_SceneInstances.Clear();

            for (int i = 0; i < m_SerializedInstances.Length; i++)
            {
                if (m_SerializedInstances[i].instance == null)
                    continue;
                            
                var rpcConfigs = m_SerializedInstances[i].pipeConfig.configs;
                var type = m_SerializedInstances[i].instance.GetType();

                var baseType = type;
                // Walk up inheritance tree getting RPCs for each type in the chain.
                while (baseType != null)
                {
                    if (!RPCRegistry.TryGetRPCsForType(baseType, out var rpcs, logError: false))
                        goto next;

                    for (int ri = 0; ri < rpcs.Length; ri++)
                    {
                        if (rpcConfigs == null || rpcs[ri].rpcId >= rpcConfigs.Length)
                        {
                            RegisterWithRPCId(m_SerializedInstances[i].instance, rpcs[ri].rpcId);
                            goto next;
                        }

                        var instanceRPCConfig = rpcConfigs[rpcs[ri].rpcId];
                        RegisterWithRPCIdAndConfig(m_SerializedInstances[i].instance, rpcs[ri].rpcId, ref instanceRPCConfig);
                    }

                    next:
                    baseType = baseType.BaseType;
                }
            }

            Dirty();
        }

        private void UnregisterObjects ()
        {
            if (m_SerializedInstances == null)
                return;

            for (int i = 0; i < m_SerializedInstances.Length; i++)
                TryUnregister(m_SerializedInstances[i].instance);
        }
    }
}
