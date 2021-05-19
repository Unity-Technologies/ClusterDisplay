using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay
{
    public partial class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>
    {
        private readonly Dictionary<Object, List<ushort>> objects = new Dictionary<Object, List<ushort>>();

        [SerializeField] private Object[] serializedObjects;
        [SerializeField] private ushort[] serializedRPCCounts;
        [SerializeField] private ushort[] serializedRPCIds;

        private bool objectsRegistered = false;

        public void RegisterObject<T> (T sceneObject, ushort rpcId) where T : Object
        {
            if (Application.isPlaying)
                return;

            if (!objects.TryGetValue(sceneObject, out var listOfRPCIds))
                objects.Add(sceneObject, new List<ushort>() { rpcId });
            else listOfRPCIds.Add(rpcId);

            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif

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

            if (RPCRegistry.TryGetInstance(out var rpcRegistry, throwException: true))
                rpcRegistry.DeincrementMethodReference(rpcId);

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

        private void Awake() => RegisterObjects();
        private void OnLevelWasLoaded(int level) => RegisterObjects();
    }
}
