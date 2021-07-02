using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay
{
    [System.Serializable]
    public struct RPCConfig
    {
        [SerializeField] public bool enabled;
    }

    [System.Serializable]
    public struct PipeConfig
    {
        [SerializeField] public RPCConfig[] configs;
    }

    public partial class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>
    {
        private bool m_SceneObjectsRegistered = false;

        private readonly List<Component> m_SceneInstances = new List<Component>();

        [SerializeField] private Component[] m_SerializedInstances = new Component[0];
        [SerializeField] private PipeConfig[] m_SerializedInstanceConfigs = new PipeConfig[0];

        public bool Registered(Component instance) => m_SceneInstances.Contains(instance);

        private void Awake() => RegisterObjects();

        public void UpdateRPCConfig(ushort pipeId, ushort rpcId, ref RPCConfig rpcConfig)
        {
            SetRPCConfig(pipeId, rpcId, ref rpcConfig);
            Save(inSerializationState: false);
        }

        public void Register<InstanceType> (InstanceType sceneObject, ushort rpcId)
            where InstanceType : Component
        {
            if (sceneObject == null)
                return;

            if (!m_SceneInstances.Contains(sceneObject))
                m_SceneInstances.Add(sceneObject);

            RegisterInstanceAccessor(sceneObject, rpcId);
        }

        public void Register<InstanceType> (InstanceType sceneObject, ushort rpcId, ref RPCConfig instanceRPCConfig)
            where InstanceType : Component
        {
            if (sceneObject == null)
                return;

            if (!m_SceneInstances.Contains(sceneObject))
                m_SceneInstances.Add(sceneObject);

            RegisterInstanceAccessor(sceneObject, rpcId, ref instanceRPCConfig);
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

            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
        }

        private void Save (bool inSerializationState)
        {
            if (m_SceneInstances.Count == 0)
                return;

            List<Component> instanceList = new List<Component>();
            List<PipeConfig> pipeConfigList = new List<PipeConfig>();

            for (int i = 0; i < m_SceneInstances.Count; i++)
            {
                if (m_SceneInstances[i] == null)
                    continue;

                if (!TryGetPipeId(m_SceneInstances[i], out var pipeId))
                    continue;

                if (instanceList.Contains(m_SceneInstances[i]))
                    continue;

                var pipeConfig = GetPipeConfig(pipeId);
                instanceList.Add(m_SceneInstances[i]);
                pipeConfigList.Add(pipeConfig);
            }

            m_SerializedInstances = m_SceneInstances.ToArray();
            m_SerializedInstanceConfigs = pipeConfigList.ToArray();

            #if UNITY_EDITOR
            if (!inSerializationState)
                EditorUtility.SetDirty(this);
            #endif
        }

        protected override void OnSerialize()
        {
            IsSerializing = true;
            Save(inSerializationState: true);
            IsSerializing = false;
        }

        protected override void OnDeserialize() => RegisterObjects();

        private void RegisterObjects ()
        {
            if (m_SerializedInstances == null)
                return;
            m_SceneInstances.Clear();

            for (int ii = 0; ii < m_SerializedInstances.Length; ii++)
            {
                var type = m_SerializedInstances[ii].GetType();
                var pipeConfig = m_SerializedInstanceConfigs[ii];
                var rpcConfigs = pipeConfig.configs;

                if (!RPCRegistry.TryGetRPCsForType(type, out var rpcs))
                    continue;

                for (int ri = 0; ri < rpcs.Length; ri++)
                {
                    if (rpcConfigs == null || rpcs[ri].rpcId >= rpcConfigs.Length)
                    {
                        Register(m_SerializedInstances[ii], rpcs[ri].rpcId);
                        continue;
                    }

                    var instanceRPCConfig = rpcConfigs[rpcs[ri].rpcId];
                    Register(m_SerializedInstances[ii], rpcs[ri].rpcId, ref instanceRPCConfig);
                }
            }
        }

        private void UnregisterObjects ()
        {
            if (m_SerializedInstances == null)
                return;

            for (int i = 0; i < m_SerializedInstances.Length; i++)
                Unregister(m_SerializedInstances[i]);
        }
    }
}
