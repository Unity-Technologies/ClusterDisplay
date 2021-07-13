using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.RPC
{
    [System.Serializable]
    public struct SerializedInstancePRCdata
    {
        // [SerializeField] public ushort pipeId;
        [SerializeField] public Component instance;
        [SerializeField] public PipeConfig pipeConfig;
    }

    public partial class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>, ISerializationCallbackReceiver
    {
        private bool m_SceneObjectsRegistered = false;

        private readonly List<Component> m_SceneInstances = new List<Component>();
        public SerializedInstancePRCdata[] m_SerializedInstances;

        public bool Registered(Component instance) => m_SceneInstances.Contains(instance);

        private void Awake() => Load();

        public void UpdateRPCConfig(ushort pipeId, ushort rpcId, ref RPCConfig rpcConfig)
        {
            SetRPCConfig(pipeId, rpcId, ref rpcConfig);
            Save();
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
                return;

            if (!m_SceneInstances.Contains(sceneObject))
                m_SceneInstances.Add(sceneObject);

            RegisterInstanceAccessor(sceneObject, rpcId);
            Dirty();
        }

        public void Register<InstanceType> (InstanceType sceneObject, ushort rpcId, ref RPCConfig instanceRPCConfig)
            where InstanceType : Component
        {
            if (sceneObject == null)
                return;

            if (!m_SceneInstances.Contains(sceneObject))
                m_SceneInstances.Add(sceneObject);

            RegisterInstanceAccessor(sceneObject, rpcId, ref instanceRPCConfig);
            Dirty();
        }

        public void Unregister<InstanceType> (InstanceType sceneObject)
            where InstanceType : Component
        {
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

        private void Save ()
        {
            if (m_SceneInstances.Count == 0)
                return;

            List<SerializedInstancePRCdata> serializedInstances = new List<SerializedInstancePRCdata>();

            for (int i = 0; i < m_SceneInstances.Count; i++)
            {
                if (m_SceneInstances[i] == null)
                    continue;

                if (!TryGetPipeId(m_SceneInstances[i], out var pipeId))
                    continue;

                var pipeConfig = GetPipeConfig(pipeId);
                serializedInstances.Add(new SerializedInstancePRCdata
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
            Save();
            IsSerializing = false;
            SerializeInstance();
        }

        public void OnAfterDeserialize()
        {
            // IsSerializing = true;
            // Load();
            // IsSerializing = false;
            DeserializeInstance();
        }

        private void Load ()
        {
            if (m_SceneObjectsRegistered)
                return;
            m_SceneObjectsRegistered = true;

            Debug.Log($"Loading serialized scene instance RPCs.");

            if (m_SerializedInstances == null)
                return;
            m_SceneInstances.Clear();

            for (int i = 0; i < m_SerializedInstances.Length; i++)
            {
                var rpcConfigs = m_SerializedInstances[i].pipeConfig.configs;

                var type = m_SerializedInstances[i].instance.GetType();

                if (!RPCRegistry.TryGetRPCsForType(type, out var rpcs))
                    continue;

                for (int ri = 0; ri < rpcs.Length; ri++)
                {
                    if (rpcConfigs == null || rpcs[ri].rpcId >= rpcConfigs.Length)
                    {
                        Register(m_SerializedInstances[i].instance, rpcs[ri].rpcId);
                        // Register(m_SerializedInstances[i].instance, m_SerializedInstances[i].pipeId, rpcs[ri].rpcId);
                        continue;
                    }

                    var instanceRPCConfig = rpcConfigs[rpcs[ri].rpcId];
                    Register(m_SerializedInstances[i].instance, rpcs[ri].rpcId, ref instanceRPCConfig);
                    // Register(m_SerializedInstances[i].instance, m_SerializedInstances[i].pipeId, rpcs[ri].rpcId, ref instanceRPCConfig);
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
