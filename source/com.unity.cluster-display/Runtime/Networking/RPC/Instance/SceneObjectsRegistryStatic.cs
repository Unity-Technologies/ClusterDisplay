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
        [GetInstanceMarker] public static Object GetInstance(ushort pipeId) => instances[pipeId];

        private readonly static IDManager pipeIdManager = new IDManager();

        private readonly static Object[] instances = new Object[ushort.MaxValue];
        private readonly static Dictionary<Object, ushort> pipeIdLookUp = new Dictionary<Object, ushort>();
        private readonly static Dictionary<ushort, bool[]> rpcStates = new Dictionary<ushort, bool[]>();

        public static bool TryGetPipeId(Object obj, out ushort pipeId) => pipeIdLookUp.TryGetValue(obj, out pipeId);
        public static bool TryPopPipeId(out ushort pipeId) => pipeIdManager.TryPopId(out pipeId);
        public static void PushPipeId(ushort pipeId) => pipeIdManager.PushUnutilizedId(pipeId);

        public static bool TryGetRPCState(ushort pipeId, ushort rpcId) => rpcStates.TryGetValue(pipeId, out var states) && states[rpcId];
        public static bool TrySetRPCState(ushort pipeId, ushort rpcId, bool state)
        {
            if (!rpcStates.TryGetValue(pipeId, out var states))
                return false;
            states[rpcId] = state;
            return true;
        }

        private static void RegisterInstanceAccessor<T> (T obj) where T : Object
        {
            if (!Application.isPlaying)
                return;

            if (obj == null)
                throw new System.Exception($"Received NULL object to register.");

            if (pipeIdLookUp.ContainsKey(obj))
                return;

            if (!pipeIdManager.TryPopId(out var pipeId))
                throw new System.Exception("Cannot register any more objects, no more ids available.");

            instances[pipeId] = obj;
            pipeIdLookUp.Add(obj, pipeId);
            rpcStates.Add(pipeId, new bool[ushort.MaxValue]);
        }

        private static void UnregisterInstanceAccessor (Object obj)
        {
            if (!Application.isPlaying)
                return;

            if (obj == null)
                return;

            if (!pipeIdLookUp.TryGetValue(obj, out var pipeId))
                return;

            instances[pipeId] = null;

            pipeIdLookUp.Remove(obj);
            rpcStates.Remove(pipeId);

            pipeIdManager.PushUnutilizedId(pipeId);
        }

        public static void ClearAccessors ()
        {
            pipeIdLookUp.Clear();
            pipeIdManager.Clear();
            rpcStates.Clear();
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
                    }

                    sceneRegistry.Register(component);
                }
            });
        }
        #endif
    }
}
