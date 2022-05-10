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
        [SerializeField] SerializedInstanceRPCData[] m_SerializedInstances;
        readonly List<Component> m_SceneInstances = new List<Component>();

        public bool Registered(Component instance) => m_SceneInstances.Contains(instance);

        void Awake ()
        {
            ClusterDebug.Log($"Initializing {nameof(SceneObjectsRegistry)} for path: \"{gameObject.scene.path}\".");
            RPCRegistry.InitializeWhenReady(GatherSceneInstances);
        }

        internal void UpdateRPCConfig(ushort pipeId, ushort rpcId, ref RPCConfig rpcConfig)
        {
            SetRPCConfig(pipeId, rpcId, ref rpcConfig);
            SerializeRPCSceneInstances();
        }

        void Dirty ()
        {
            #if UNITY_EDITOR
            if (!EditorApplication.isPlaying && !IsSerializing)
                EditorUtility.SetDirty(this);
            #endif
        }

        public void Register (Component sceneObject, System.Type type)
        {
            if (sceneObject == null)
            {
                throw new System.ArgumentNullException($"Refusing to register NULL instance with: \"{nameof(SceneObjectsRegistry)}\" in scene: \"{gameObject.scene.name}\".");
            }
            
            if (!RPCRegistry.TryGetRPCsForType(type, out var rpcs, logError: false))
            {
                throw new System.ArgumentNullException($"Cannot register instance of: \"{type.Name}\" in scene: \"{gameObject.scene.name}\", there is no RPC associated with that type.");
            }

            for (int ri = 0; ri < rpcs.Length; ri++)
            {
                var rpcConfig = new RPCConfig();
                RegisterWithRPCIdAndConfig(sceneObject, rpcs[ri].rpcId, ref rpcConfig);
            }
        }

        /// <summary>
        /// Register an instance that contains an RPC so it can emit and receive cluster display events.
        /// </summary>
        public void Register<InstanceType>(InstanceType sceneObject)
            where InstanceType : Component => Register(sceneObject, typeof(InstanceType));
        
        /// <summary>
        /// Register an instance that contains an RPC so it can emit and receive cluster display events.
        /// </summary>
        public void Unregister<InstanceType> (InstanceType sceneObject)
            where InstanceType : Component
        {
            if (sceneObject == null)
            {
                throw new System.ArgumentNullException($"Refusing to unregister NULL instance with: \"{nameof(SceneObjectsRegistry)}\" in scene: \"{gameObject.scene.name}\".");
            }

            m_SceneInstances.Remove(sceneObject);
            UnregisterInstanceAccessor(sceneObject);

            if (m_SceneInstances.Count == 0)
            {
                if (Application.isPlaying)
                {
                    if (this != null)
                        Destroy(this.gameObject);
                }

                else
                {
                    if (this != null)
                        DestroyImmediate(this.gameObject);
                }
            }

            Dirty();
        }

        void RegisterWithRPCId<InstanceType> (InstanceType sceneObject, ushort rpcId)
            where InstanceType : Component
        {
            if (!m_SceneInstances.Contains(sceneObject))
            {
                m_SceneInstances.Add(sceneObject);
            }

            RegisterInstanceAccessor(sceneObject, rpcId);
            
            Dirty();
        }

        bool RegisterWithRPCIdAndConfig<InstanceType> (InstanceType sceneObject, ushort rpcId, ref RPCConfig instanceRPCConfig)
            where InstanceType : Component
        {
            if (!m_SceneInstances.Contains(sceneObject))
                m_SceneInstances.Add(sceneObject);

            RegisterInstanceAccessor(sceneObject, rpcId, ref instanceRPCConfig);
            
            Dirty();
            return true;
        }

        void SerializeRPCSceneInstances ()
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

        void GatherSceneInstances ()
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

        void UnregisterObjects ()
        {
            if (m_SerializedInstances == null)
                return;

            for (int i = 0; i < m_SerializedInstances.Length; i++)
                Unregister(m_SerializedInstances[i].instance);
        }
    }
}
