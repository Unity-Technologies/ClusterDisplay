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

    #if UNITY_EDITOR
    [InitializeOnLoad]
    [ExecuteAlways]
    #endif
    public partial class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>
    {
        private bool sceneObjectsRegistered = false;

        private readonly List<Component> sceneInstances = new List<Component>();

        [SerializeField] private Component[] serializedInstances;
        [SerializeField] private PipeConfig[] serializedInstanceConfigs;

        public bool Registered(Component instance) => sceneInstances.Contains(instance);

        private void Awake()
        {
            RegisterObjects();
        }

        private void Clear ()
        {
            if (!Application.isPlaying)
                return;

            UnregisterObjects();

            sceneInstances.Clear();
            serializedInstances = null;
            sceneObjectsRegistered = false;
        }

        public void Register<InstanceType> (InstanceType sceneObject, ushort rpcId)
            where InstanceType : Component
        {
            if (sceneObject == null)
                return;

            if (!sceneInstances.Contains(sceneObject))
                sceneInstances.Add(sceneObject);

            RegisterInstanceAccessor(sceneObject, rpcId);
        }

        public void Register<InstanceType> (InstanceType sceneObject, ushort rpcId, ref RPCConfig instanceRPCConfig)
            where InstanceType : Component
        {
            if (sceneObject == null)
                return;

            if (!sceneInstances.Contains(sceneObject))
                sceneInstances.Add(sceneObject);

            RegisterInstanceAccessor(sceneObject, rpcId, ref instanceRPCConfig);
        }

        public void Unregister<InstanceType> (InstanceType sceneObject)
            where InstanceType : Component
        {
            sceneInstances.Remove(sceneObject);
            UnregisterInstanceAccessor(sceneObject);

            if (sceneInstances.Count == 0)
            {
                if (Application.isPlaying)
                    Destroy(this.gameObject);
                else DestroyImmediate(this.gameObject);
            }

            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
        }

        protected override void OnSerialize()
        {
            if (sceneInstances.Count == 0)
                return;

            List<Component> instanceList = new List<Component>();
            List<PipeConfig> instanceConfigList = new List<PipeConfig>();

            for (int i = 0; i < sceneInstances.Count; i++)
            {
                if (serializedInstances[i] == null)
                    continue;

                if (!TryGetPipeId(serializedInstances[i], out var pipeId))
                    continue;

                if (!TryGetInstanceConfig(pipeId, out var instanceConfig))
                    continue;

                instanceConfigList.Add(instanceConfig);
                instanceList.Add(sceneInstances[i]);
            }

            serializedInstanceConfigs = instanceConfigList.ToArray();
        }

        protected override void OnDeserialize() => RegisterObjects();

        private void RegisterObjects ()
        {
            if (serializedInstances != null)
            {
                for (int ii = 0; ii < serializedInstances.Length; ii++)
                {
                    if (serializedInstances[ii] == null)
                        continue;

                    var type = serializedInstances[ii].GetType();
                    if (!RPCRegistry.TryGetRPCsForType(type, out var rpcs))
                        continue;

                    for (int ri = 0; ri < rpcs.Length; ri++)
                    {
                        var instanceRPCConfigs = serializedInstanceConfigs[ii].configs;
                        if (instanceRPCConfigs == null || rpcs[ri].rpcId >= instanceRPCConfigs.Length)
                        {
                            Register(serializedInstances[ii], rpcs[ri].rpcId);
                            continue;
                        }

                        var instanceRPCConfig = instanceRPCConfigs[rpcs[ri].rpcId];
                        Register(serializedInstances[ii], rpcs[ri].rpcId, ref instanceRPCConfig);
                    }

                    sceneInstances.Add(serializedInstances[ii]);
                }
            }
        }

        private void UnregisterObjects ()
        {
            if (serializedInstances != null)
                for (int i = 0; i < serializedInstances.Length; i++)
                    Unregister(serializedInstances[i]);
        }
    }
}
