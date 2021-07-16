using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Unity.ClusterDisplay.RPC
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
        private readonly static PipeConfig[] m_PipeIdToInstanceConfig = new PipeConfig[ushort.MaxValue];
        private readonly static Dictionary<int, ushort> m_InstanceIdToPipeId = new Dictionary<int, ushort>();

        public static bool IsSerializing { protected set; get; }

        private static bool UseDictionary 
        {
            get
            {
                #if UNITY_EDITOR
                if (IsSerializing)
                    return false;
                return UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
                #else
                return true;
                #endif
            }
        }

        private static void RegisterPipeIdWithInstanceId (Component instance, ushort pipeId)
        {
            if (!UseDictionary)
                return;

            var instanceId = instance.GetInstanceID();
            if (m_InstanceIdToPipeId.ContainsKey(instanceId))
                m_InstanceIdToPipeId[instanceId] = pipeId;
            else m_InstanceIdToPipeId.Add(instanceId, pipeId);
        }

        public static bool TryGetPipeId(Component instance, out ushort pipeId)
        {
            /*
            if (UseDictionary)
            {
                var instanceId = instance.GetInstanceID();
                return m_InstanceIdToPipeId.TryGetValue(instanceId, out pipeId);
            }
            */

            if (instance == null || m_Instances == null)
            {
                pipeId = 0;
                return false;
            }

            for (int i = 0; i < m_Instances.Length; i++)
            {
                if (instance != m_Instances[i])
                    continue;

                pipeId = (ushort)i;
                return true;
            }

            pipeId = 0;
            return false;
        }

        public static bool TryPopPipeId(out ushort pipeId) => m_PipeIdManager.TryPopId(out pipeId);
        public static void PushPipeId(ushort pipeId) => m_PipeIdManager.PushUnutilizedId(pipeId);

        public static PipeConfig GetPipeConfig(ushort pipeId) => m_PipeIdToInstanceConfig[pipeId];
        public static RPCConfig GetRPCConfig(ushort pipeId, ushort rpcId)
        {
            var configs = m_PipeIdToInstanceConfig[pipeId].configs;
            if (configs == null || rpcId >= configs.Length)
            {
                var newRPCConfig = new RPCConfig();
                newRPCConfig.enabled = true;
                return newRPCConfig;
            }

            return configs[rpcId];
        }

        public static void SetRPCConfig(ushort pipeId, ushort rpcId, ref RPCConfig rpcConfig)
        {
            var pipeConfig = m_PipeIdToInstanceConfig[pipeId];
            RPCConfig[] rpcConfigs = null;

            if (pipeConfig.configs == null)
            {
                rpcConfigs = new RPCConfig[rpcId + 1];
                for (int i = 0; i < rpcId + 1; i++)
                    rpcConfigs[i] = new RPCConfig();
            }

            else if (pipeConfig.configs.Length < rpcId + 1)
            {
                var resizedConfigs = new RPCConfig[rpcId + 1];
                System.Array.Copy(pipeConfig.configs, resizedConfigs, pipeConfig.configs.Length);
                rpcConfigs = resizedConfigs;
            }

            else rpcConfigs = pipeConfig.configs;

            rpcConfigs[rpcId] = rpcConfig;
            pipeConfig.configs = rpcConfigs;
            m_PipeIdToInstanceConfig[pipeId] = pipeConfig;
        }

        private static bool Validate<InstanceType> (InstanceType instance, out ushort pipeId)
            where InstanceType : Component
        {
            if (instance == null)
            {
                Debug.LogError($"Received NULL object to register.");
                pipeId = 0;
                return false;
            }

            if (TryGetPipeId(instance, out pipeId))
                return true;

            if (!m_PipeIdManager.TryPopId(out pipeId))
            {
                Debug.LogError("Cannot register any more objects, no more ids available.");
                return false;
            }

            return true;
        }

        private static void RegisterInstanceAccessor<InstanceType> (InstanceType instance, ushort rpcId)
            where InstanceType : Component
        {
            if (!Validate(instance, out var pipeId))
                return;

            m_Instances[pipeId] = instance;
            // RegisterPipeIdWithInstanceId(instance, pipeId);

            var newInstanceRPCConfig = new RPCConfig();
            newInstanceRPCConfig.enabled = true;

            SetRPCConfig(pipeId, rpcId, ref newInstanceRPCConfig);
        }

        private static void RegisterInstanceAccessor<InstanceType> (InstanceType instance, ushort rpcId, ref RPCConfig instanceRPCConfig)
            where InstanceType : Component
        {
            if (!Validate(instance, out var pipeId))
                return;

            m_Instances[pipeId] = instance;
            // RegisterPipeIdWithInstanceId(instance, pipeId);

            SetRPCConfig(pipeId, rpcId, ref instanceRPCConfig);
        }

        private static void UnregisterInstanceAccessor<InstanceType> (InstanceType instance)
            where InstanceType : Component
        {
            if (instance == null)
                return;

            if (!TryGetPipeId(instance, out var pipeId))
                return;
            m_Instances[pipeId] = null;
            m_PipeIdManager.PushUnutilizedId(pipeId);
        }

        public static void ClearAccessors ()
        {
            m_PipeIdManager.Clear();
            m_InstanceIdToPipeId.Clear();
        }

#if UNITY_EDITOR
        static SceneObjectsRegistry ()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;

            Debug.Log($"Initialized static: \"{nameof(SceneObjectsRegistry)}\".");
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => PollUnregisteredInstanceRPCs();

        /// <summary>
        /// This is executed when scripts are reloaded or if a scene is opened in the editor. What it does is 
        /// search the scene for instances of registered RPC declaring types and registers those instances with a
        /// scene instance of SceneObjectsRegistry. If a SceneObjectsRegistry does not exist, it will automatically be created.
        /// </summary>
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
                        sceneRegistry.DeserializeInstance();
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
