using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Unity.ClusterDisplay
{
    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
    public partial class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>
    {
        private readonly Dictionary<Object, List<ushort>> objects = new Dictionary<Object, List<ushort>>();

        [SerializeField] private Object[] serializedObjects;
        [SerializeField] private ushort[] serializedRPCCounts;
        [SerializeField] private ushort[] serializedRPCIds;

        private bool objectsRegistered = false;

        private void Awake() => RegisterObjects();
        // private void OnLevelWasLoaded(int level) => RegisterObjects();

        private void OnDestroy() => Clear();
        private void Clear ()
        {
            objects.Clear();
            serializedObjects = null;
            serializedRPCCounts = null;
            serializedRPCIds = null;
        }

        private void RegisterObject<T> (T sceneObject, ushort rpcId) where T : Object
        {
            if (!objects.TryGetValue(sceneObject, out var listOfRPCIds))
                objects.Add(sceneObject, new List<ushort>() { rpcId });
            else listOfRPCIds.Add(rpcId);

            Debug.Log($"Registered scene object of type: \"{typeof(T).FullName}\".");
        }

        public void Unregister<T> (T sceneObject, ushort rpcId) where T : Object
        {
            if (!objects.TryGetValue(sceneObject, out var rpcIdList))
                return;

            rpcIdList.Remove(rpcId);
            if (rpcIdList.Count == 0)
                objects.Remove(sceneObject);

            if (ObjectRegistry.TryGetInstance(out var objectRegistry, throwException: true))
                objectRegistry.Unregister(sceneObject);

            if (objects.Count == 0)
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
            if (objects.Count == 0)
                return;

            if (serializedObjects == null || serializedObjects.Length != objects.Count)
            {
                serializedObjects = new Object[objects.Count];
                serializedRPCCounts = new ushort[objects.Count];
            }

            serializedObjects = objects.Keys.ToArray();

            List<ushort> listOfRPCIds = new List<ushort>();
            for (int i = 0; i < serializedObjects.Length; i++)
            {
                objects.TryGetValue(serializedObjects[i], out var list);
                serializedRPCCounts[i] = (ushort)list.Count;
                listOfRPCIds.AddRange(list);
            }

            serializedRPCIds = listOfRPCIds.ToArray();
        }

        protected override void OnDeserialize()
        {
            if (serializedObjects == null)
                return;

            int startRPCIndexRange = 0;
            for (int i = 0; i < serializedObjects.Length; i++)
            {
                int rpcCount = serializedRPCCounts[i];

                ushort[] array = new ushort[serializedRPCCounts[i]];
                System.Array.Copy(serializedRPCIds, startRPCIndexRange, array, 0, rpcCount);

                objects.Add(serializedObjects[i], array.ToList());

                startRPCIndexRange += rpcCount;
            }
        }

        protected override void Destroying()
        {
            if (!Application.isPlaying)
                return;

            if (ObjectRegistry.TryGetInstance(out var objectRegistry, throwException: true))
                objectRegistry.Unregister(serializedObjects);
        }

        private void RegisterObjects ()
        {
            if (objectsRegistered)
                return;

            if (!Application.isPlaying)
                return;

            if (ObjectRegistry.TryGetInstance(out var objectRegistry, throwException: true))
                objectRegistry.Register(serializedObjects);

            objectsRegistered = true;
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
            // Debug.Log("Polling for unregistered instance RPCs.");
            if (!RPCRegistry.TryGetInstance(out var rpcRegistry, throwException: false))
                return;

            Dictionary<string, SceneObjectsRegistry> sceneObjectsRegistry = new Dictionary<string, SceneObjectsRegistry>();
            rpcRegistry.Foreach((rpcMethodInfo) =>
            {
                var type = rpcMethodInfo.methodInfo.DeclaringType;
                if (type.IsAbstract)
                    return;

                var objs = FindObjectsOfType(type);

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

                        else sceneRegistry.Clear();
                        sceneObjectsRegistry.Add(path, sceneRegistry);
                    }

                    sceneRegistry.RegisterObject(component, rpcMethodInfo.rpcId);
                }
            });
        }
        #endif
    }
}
