using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.RPC
{
    [System.Serializable]
    public struct SerializedInstanceRPCData
    {
        // [SerializeField] public ushort pipeId;
        [SerializeField] public Component instance;
        [SerializeField] public PipeConfig pipeConfig;
    }

    public partial class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>, ISerializationCallbackReceiver
    {
        private bool m_SceneObjectsRegistered = false;

        private readonly List<Component> m_SceneInstances = new List<Component>();
        public SerializedInstanceRPCData[] m_SerializedInstances;

        public bool Registered(Component instance) => m_SceneInstances.Contains(instance);

        private void Awake()
        {
            if (!RPCRegistry.Deserialized)
            {
                RPCRegistry.onRegistryInitialized -= RegisterRPCSceneInstances;
                RPCRegistry.onRegistryInitialized += RegisterRPCSceneInstances;
                return;
            }

            RegisterRPCSceneInstances();
        }

        public void UpdateRPCConfig(ushort pipeId, ushort rpcId, ref RPCConfig rpcConfig)
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

        public void Register<InstanceType> (InstanceType sceneObject, ushort rpcId)
            where InstanceType : Component
        {
            if (sceneObject == null)
            {
                Debug.LogError($"Refusing to register NULL instance with RPC ID: ({rpcId}).");
                return;
            }

            if (!m_SceneInstances.Contains(sceneObject))
                m_SceneInstances.Add(sceneObject);

            RegisterInstanceAccessor(sceneObject, rpcId);
            Dirty();
        }

        public void Register<InstanceType> (InstanceType sceneObject, ushort rpcId, ref RPCConfig instanceRPCConfig)
            where InstanceType : Component
        {
            if (sceneObject == null)
            {
                Debug.LogError($"Refusing to register NULL instance with RPC ID: ({rpcId}) with: \"{nameof(SceneObjectsRegistry)}\" in scene: \"{gameObject.scene.name}\".");
                return;
            }

            if (!m_SceneInstances.Contains(sceneObject))
                m_SceneInstances.Add(sceneObject);

            RegisterInstanceAccessor(sceneObject, rpcId, ref instanceRPCConfig);
            Dirty();
        }

        public void Unregister<InstanceType> (InstanceType sceneObject)
            where InstanceType : Component
        {
            if (sceneObject == null)
            {
                Debug.LogError($"Refusing to unregister NULL instance with: \"{nameof(SceneObjectsRegistry)}\" in scene: \"{gameObject.scene.name}\".");
                return;
            }

            m_SceneInstances.Remove(sceneObject);
            UnregisterInstanceAccessor(sceneObject);

            if (m_SceneInstances.Count == 0)
            {
                if (Application.isPlaying)
                    Destroy(this.gameObject);
                else DestroyImmediate(this.gameObject);
            }

            Dirty();
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

        public void OnBeforeSerialize() 
        {
            IsSerializing = true;
            SerializeRPCSceneInstances();
            IsSerializing = false;
            SerializeSceneSingletonInstance();
        }

        public void OnAfterDeserialize() => DeserializeSceneSingletonInstance();

        private void RegisterRPCSceneInstances ()
        {
            RPCRegistry.onRegistryInitialized -= RegisterRPCSceneInstances;

            if (m_SceneObjectsRegistered)
                return;

            m_SceneObjectsRegistered = true;

            if (m_SerializedInstances == null)
            {
                Debug.LogWarning($"There are no serialized RPC scene instances registered with: \"{nameof(SceneObjectsRegistry)}\" attached to: \"{gameObject.name}\" in scene: \"{gameObject.scene.name}\".");
                return;
            }

            Debug.Log($"Registering serialized RPC instances for scene: \"{gameObject.scene.name}\".");
            m_SceneInstances.Clear();

            for (int i = 0; i < m_SerializedInstances.Length; i++)
            {
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
                            Register(m_SerializedInstances[i].instance, rpcs[ri].rpcId);
                            goto next;
                        }

                        var instanceRPCConfig = rpcConfigs[rpcs[ri].rpcId];
                        Register(m_SerializedInstances[i].instance, rpcs[ri].rpcId, ref instanceRPCConfig);
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
                Unregister(m_SerializedInstances[i].instance);
        }
    }
}
