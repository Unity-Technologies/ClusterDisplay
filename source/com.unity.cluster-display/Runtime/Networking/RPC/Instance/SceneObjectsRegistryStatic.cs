using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Unity.ClusterDisplay
{
    public partial class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>
    {
        public class GetInstanceMarker : System.Attribute {}

        /// <summary>
        /// RPCIL references objects through this method to directly call methods on the instance. 
        /// </summary>
        /// <param name="pipeId">The ID of your instance containing and RPC method.</param>
        /// <returns></returns>
        [GetInstanceMarker] public static Object GetInstance(ushort pipeId)
        {
            // TODO: Wrap this null check in a verbose logging #if to improve performance.
            var obj = m_Instances[pipeId];
            if (obj == null)
            {
                Debug.LogError($"There is no instance with pipe ID: \"{pipeId}\", verify whether your instance was destroyed or unregistered.");
                return null;
            }

            return obj;
        }

        private readonly static IDManager m_PipeIdManager = new IDManager();

        private readonly static Component[] m_Instances = new Component[ushort.MaxValue];
        private readonly static Dictionary<int, ushort> m_InstanceIdToPipeId = new Dictionary<int, ushort>();
        private readonly static Dictionary<ushort, PipeConfig> m_PipeIdToInstanceConfig = new Dictionary<ushort, PipeConfig>();

        public static bool TryGetPipeId(int instanceId, out ushort pipeId) => m_InstanceIdToPipeId.TryGetValue(instanceId, out pipeId);
        public static bool TryGetPipeId(Component instance, out ushort pipeId) => m_InstanceIdToPipeId.TryGetValue(instance.GetInstanceID(), out pipeId);
        public static bool TryPopPipeId(out ushort pipeId) => m_PipeIdManager.TryPopId(out pipeId);
        public static void PushPipeId(ushort pipeId) => m_PipeIdManager.PushUnutilizedId(pipeId);

        public static bool TryGetInstanceConfig(ushort pipeId, out PipeConfig pipeConfig) => m_PipeIdToInstanceConfig.TryGetValue(pipeId, out pipeConfig);
        public static bool TryGetRPCConfig(ushort pipeId, ushort rpcId, out RPCConfig rpcConfig)
        {
            if (!m_PipeIdToInstanceConfig.TryGetValue(pipeId, out var pipeConfig))
            {
                rpcConfig = default(RPCConfig);
                return false;
            }

            rpcConfig = pipeConfig.configs[rpcId];
            return true;
        }

        public static bool TrySetRPCConfig(ushort pipeId, ushort rpcId, ref RPCConfig pipeConfig)
        {
            if (!m_PipeIdToInstanceConfig.ContainsKey(pipeId))
                return false;

            if (m_PipeIdToInstanceConfig[pipeId].configs == null || m_PipeIdToInstanceConfig[pipeId].configs.Length < rpcId + 1)
            {
                var instanceConfig = m_PipeIdToInstanceConfig[pipeId];

                var resizedConfigs = new RPCConfig[rpcId + 1];
                System.Array.Copy(instanceConfig.configs, resizedConfigs, instanceConfig.configs.Length);
                resizedConfigs[rpcId] = pipeConfig;

                instanceConfig.configs = resizedConfigs;
                return true;
            }

            m_PipeIdToInstanceConfig[pipeId].configs[rpcId] = pipeConfig;
            return true;
        }

        private static bool Validate<InstanceType> (InstanceType instance, out ushort pipeId)
            where InstanceType : Component
        {
            pipeId = 0;
            if (instance == null)
            {
                Debug.LogError($"Received NULL object to register.");
                return false;
            }

            if (m_InstanceIdToPipeId.ContainsKey(instance.GetInstanceID()))
                return false;

            if (!m_PipeIdManager.TryPopId(out pipeId))
            {
                Debug.LogError("Cannot register any more objects, no more ids available.");
                return false;
            }

            return true;
        }

        private static void SetConfig (ushort pipeId, ushort rpcId, ref RPCConfig instanceRPCConfig)
        {
            if (!m_PipeIdToInstanceConfig.ContainsKey(pipeId))
            {
                var newConfigs = new RPCConfig[rpcId + 1];
                newConfigs[rpcId] = instanceRPCConfig;
                m_PipeIdToInstanceConfig.Add(pipeId, new PipeConfig() { configs = newConfigs });
                return;
            }

            m_PipeIdToInstanceConfig[pipeId].configs[rpcId] = instanceRPCConfig;
        }

        private static void RegisterInstanceAccessor<InstanceType> (InstanceType instance, ushort rpcId)
            where InstanceType : Component
        {
            if (!Validate(instance, out var pipeId))
                return;

            m_Instances[pipeId] = instance;
            m_InstanceIdToPipeId.Add(instance.GetInstanceID(), pipeId);

            if (!m_PipeIdToInstanceConfig.TryGetValue(pipeId, out var _))
            {
                var newInstanceRPCConfig = new RPCConfig(); 
                SetConfig(pipeId, rpcId, ref newInstanceRPCConfig);
            }
        }

        private static void RegisterInstanceAccessor<InstanceType> (InstanceType instance, ushort rpcId, ref RPCConfig instanceRPCConfig)
            where InstanceType : Component
        {
            if (!Validate(instance, out var pipeId))
                return;

            m_Instances[pipeId] = instance;
            m_InstanceIdToPipeId.Add(instance.GetInstanceID(), pipeId);
            SetConfig(pipeId, rpcId, ref instanceRPCConfig);
        }

        private static void UnregisterInstanceAccessor<InstanceType> (InstanceType instance)
            where InstanceType : Component
        {
            if (instance == null)
                return;

            int instanceId = instance.GetInstanceID();
            if (!m_InstanceIdToPipeId.TryGetValue(instanceId, out var pipeId))
                return;

            m_Instances[pipeId] = null;

            m_InstanceIdToPipeId.Remove(instanceId);
            m_PipeIdToInstanceConfig.Remove(pipeId);

            m_PipeIdManager.PushUnutilizedId(pipeId);
        }

        public static void ClearAccessors ()
        {
            m_InstanceIdToPipeId.Clear();
            m_PipeIdManager.Clear();
            m_PipeIdToInstanceConfig.Clear();
        }

        #if UNITY_EDITOR
        static SceneObjectsRegistry ()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => PollUnregisteredInstanceRPCs();

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void PollUnregisteredInstanceRPCs()
        {
            if (Application.isPlaying)
                return;

            // Debug.Log("Polling for unregistered instance RPCs.");
            if (!RPCRegistry.TryGetInstance(out var rpcRegistry, throwException: false))
                return;

            Dictionary<string, SceneObjectsRegistry> sceneObjectsRegistry = new Dictionary<string, SceneObjectsRegistry>();
            Component[] components = FindObjectsOfType<Component>();

            rpcRegistry.Foreach((rpcMethodInfo) =>
            {
                var type = rpcMethodInfo.methodInfo.DeclaringType;
                if (type.IsAbstract)
                    return;

                var objs = components.Where(component => component.GetType().FullName == type.FullName).ToArray();
                // var objs = FindObjectsOfType(type);

                if (objs.Length == 0)
                    return;

                foreach (var obj in objs)
                {
                    var component = obj as Component;
                    if (component == null)
                        continue;

                    if (!sceneObjectsRegistry.TryGetValue(component.gameObject.scene.path, out var sceneRegistry))
                    {
                        var path = component.gameObject.scene.path;
                        if (!TryGetSceneInstance(path, out sceneRegistry, throwException: false))
                        {
                            if (!TryCreateNewInstance(component.gameObject.scene, out sceneRegistry))
                                continue;
                        }

                        sceneObjectsRegistry.Add(path, sceneRegistry);
                        sceneRegistry.OnDeserialize();
                    }

                    if (sceneRegistry.Registered(component))
                        continue;

                    if (!RPCRegistry.TryGetRPCsForType(type, out var rpcs))
                        continue;

                    for (int ri = 0; ri < rpcs.Length; ri++)
                        sceneRegistry.Register(component, rpcs[ri].rpcId);
                }
            });
        }
        #endif
    }
}
