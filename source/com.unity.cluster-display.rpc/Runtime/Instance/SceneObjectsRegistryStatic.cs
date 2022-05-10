using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Unity.ClusterDisplay.RPC
{
    #if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
    #endif
    public partial class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>
    {
        internal class GetInstanceMarker : System.Attribute {}

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
                ClusterDebug.LogError($"There is no instance with pipe ID: \"{pipeId}\", verify whether your instance was destroyed or unregistered.");
                return null;
            }

            return obj;
        }

        readonly static IDManager m_PipeIdManager = new IDManager();

        readonly static Component[] m_Instances = new Component[IDManager.MaxIDCount];
        readonly static PipeConfig[] m_PipeIdToInstanceConfig = new PipeConfig[IDManager.MaxIDCount];
        readonly static Dictionary<int, ushort> m_InstanceIdToPipeId = new Dictionary<int, ushort>();

        internal static bool IsSerializing { set; get; }

        static bool UseDictionary 
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

        static void RegisterPipeIdWithInstanceId (Component instance, ushort pipeId)
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

        internal static bool TryPopPipeId(out ushort pipeId) => m_PipeIdManager.TryPopId(out pipeId);
        internal static void PushPipeId(ushort pipeId) => m_PipeIdManager.PushUnutilizedId(pipeId);

        internal static PipeConfig GetPipeConfig(ushort pipeId) => m_PipeIdToInstanceConfig[pipeId];
        internal static RPCConfig GetRPCConfig(ushort pipeId, ushort rpcId)
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

        internal static void SetRPCConfig(ushort pipeId, ushort rpcId, ref RPCConfig rpcConfig)
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

        static bool Validate<InstanceType> (InstanceType instance, out ushort pipeId)
            where InstanceType : Component
        {
            if (TryGetPipeId(instance, out pipeId))
                return true;

            if (!m_PipeIdManager.TryPopId(out pipeId))
            {
                ClusterDebug.LogError("Cannot register any more objects, no more ids available.");
                return false;
            }

            return true;
        }

        static bool TryRegisterInstanceAccessor<InstanceType> (InstanceType instance, ushort rpcId)
            where InstanceType : Component
        {
            if (!Validate(instance, out var pipeId))
                return false;

            m_Instances[pipeId] = instance;
            var newInstanceRPCConfig = new RPCConfig();
            newInstanceRPCConfig.enabled = true;

            SetRPCConfig(pipeId, rpcId, ref newInstanceRPCConfig);
            return true;
        }

        static bool TryRegisterInstanceAccessor<InstanceType> (InstanceType instance, ushort rpcId, ref RPCConfig instanceRPCConfig)
            where InstanceType : Component
        {
            if (!Validate(instance, out var pipeId))
                return false;

            m_Instances[pipeId] = instance;
            SetRPCConfig(pipeId, rpcId, ref instanceRPCConfig);
            return true;
        }

        static bool TryUnregisterInstanceAccessor<InstanceType> (InstanceType instance)
            where InstanceType : Component
        {
            if (!TryGetPipeId(instance, out var pipeId))
                return false;
            
            m_Instances[pipeId] = null;
            m_PipeIdManager.PushUnutilizedId(pipeId);
            return true;
        }

        internal static void ClearAccessors ()
        {
            m_PipeIdManager.Clear();
            m_InstanceIdToPipeId.Clear();
        }

        #if UNITY_EDITOR
        static SceneObjectsRegistry() => Prepare();
        static void OnSceneOpened(Scene scene, OpenSceneMode mode) => PollUnregisteredInstanceRPCs();
        static void Prepare ()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        /// <summary>
        /// This is executed when scripts are reloaded or if a scene is opened in the editor. What it does is 
        /// search the scene for instances of registered RPC declaring types and registers those instances with a
        /// scene instance of SceneObjectsRegistry. If a SceneObjectsRegistry does not exist, it will automatically be created.
        /// </summary>
        [UnityEditor.Callbacks.DidReloadScripts]
        static void PollUnregisteredInstanceRPCs()
        {
            if (Application.isPlaying)
                return;

            // RuntimeLogWriter.Log("Polling for unregistered instance RPCs.");
            if (!RPCRegistry.TryGetInstance(out var rpcRegistry, throwError: true))
                return;

            Dictionary<string, SceneObjectsRegistry> sceneObjectsRegistry = new Dictionary<string, SceneObjectsRegistry>();
            List<Component> components = new List<Component>();

            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                var loadedScene = SceneManager.GetSceneAt(si);
                if (!loadedScene.isLoaded)
                    continue;

                var rootGameObjects = loadedScene.GetRootGameObjects();
                for (int ri = 0; ri < rootGameObjects.Length; ri++)
                {
                    var rootGameObjectComponents = rootGameObjects[ri].GetComponents<Component>();
                    if (rootGameObjectComponents != null && rootGameObjectComponents.Length > 0)
                    {
                        for (int ci = 0; ci < rootGameObjectComponents.Length; ci++)
                        {
                            if (rootGameObjectComponents[ci] == null)
                                continue;
                            components.Add(rootGameObjectComponents[ci]);
                        }
                    }

                    var childComponents = rootGameObjects[ri].GetComponentsInChildren<Component>(includeInactive: true);
                    if (childComponents != null && childComponents.Length > 0)
                    {
                        for (int ci = 0; ci < childComponents.Length; ci++)
                        {
                            if (childComponents[ci] == null)
                                continue;
                            components.Add(childComponents[ci]);
                        }
                    }
                }
            }

            rpcRegistry.Foreach(((rpcMethodInfo) =>
            {
                List<Component> componentsWithRPC = new List<Component>();
                for (int ci = 0; ci < components.Count; ci++)
                {
                    var type = components[ci].GetType();

                    if (type != rpcMethodInfo.methodInfo.DeclaringType &&
                        !type.IsSubclassOf(rpcMethodInfo.methodInfo.DeclaringType))
                        continue;

                    componentsWithRPC.Add(components[ci]);
                }

                if (componentsWithRPC.Count == 0)
                    return;

                foreach (var component in componentsWithRPC)
                {
                    if (!sceneObjectsRegistry.TryGetValue(component.gameObject.scene.path, out var sceneRegistry))
                    {
                        var path = component.gameObject.scene.path;
                        if (!TryGetSceneInstance(path, out sceneRegistry, throwException: false))
                        {
                            if (!TryCreateNewInstance(component.gameObject.scene, out sceneRegistry))
                                continue;
                        }

                        sceneObjectsRegistry.Add(path, sceneRegistry);
                    }

                    if (sceneRegistry.Registered(component))
                        continue;

                    if (!RPCRegistry.TryGetRPCsForType(rpcMethodInfo.methodInfo.DeclaringType, out var rpcs))
                        continue;

                    for (int ri = 0; ri < rpcs.Length; ri++)
                        sceneRegistry.RegisterWithRPCId(component, rpcs[ri].rpcId);
                }
            }));
        }

        #endif
    }
}
