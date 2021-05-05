using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay
{
    public class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>
    {
        [SerializeField] private Object[] serializedObjects;

        public void RegisterObject<T> (T sceneObject) where T : Object
        {
            if (Application.isPlaying)
            {
                goto log;
            }

            if (serializedObjects != null)
            {
                if (serializedObjects.Contains(sceneObject))
                    throw new System.Exception($"Registry already has already registered the instance of: \"{typeof(T).FullName}\".");


    #if UNITY_EDITOR
                var copyOfSerializedObjects = new Object[serializedObjects.Length + 1];
                System.Array.Copy(serializedObjects, copyOfSerializedObjects, serializedObjects.Length);
                copyOfSerializedObjects[copyOfSerializedObjects.Length - 1] = sceneObject;
                serializedObjects = copyOfSerializedObjects;
    #endif
            }

            else
            {
    #if UNITY_EDITOR
                serializedObjects = new Object[1];
                serializedObjects[0] = sceneObject;
    #endif
            }

            EditorUtility.SetDirty(this);

            log:
            Debug.Log($"Registered scene object of type: \"{typeof(T).FullName}\".");
        }

        protected override void OnSerialize()
        {
            if (serializedObjects == null)
                return;

            var objects = new List<Object>();

            for (int i = 0; i < serializedObjects.Length; i++)
            {
                if (serializedObjects[i] == null)
                    continue;

                objects.Add(serializedObjects[i]);
            }

            serializedObjects = objects.ToArray();
        }

        protected override void Destroying()
        {
            if (!Application.isPlaying)
                return;

            if (!ClusterDisplayNetworkManager.TryGetInstance(out var clusterDisplayNetworkManager))
                return;

            clusterDisplayNetworkManager.ObjectRegistry.Unregister(serializedObjects);
        }

        private void OnLevelWasLoaded(int level)
        {
            if (!Application.isPlaying)
                return;

            if (!ClusterDisplayNetworkManager.TryGetInstance(out var clusterDisplayNetworkManager))
                return;

            clusterDisplayNetworkManager.ObjectRegistry.Register(serializedObjects);
        }
    }
}
