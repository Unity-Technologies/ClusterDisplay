using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.ClusterDisplay
{
    public class SceneSingletonMonoBehaviourTryGetInstanceMarker : Attribute {}
    [ExecuteAlways]
    public abstract class SceneSingletonMonoBehaviour<T> : MonoBehaviour, ISerializationCallbackReceiver where T : SceneSingletonMonoBehaviour<T>
    {
        private static readonly Dictionary<string, T> sceneInstances = new Dictionary<string, T>();
        [SerializeField] private string serializedScenePath;

        protected virtual void Enabled () {}
        private void OnEnable()
        {
            serializedScenePath = gameObject.scene.path;
            Enabled();
        }

        protected virtual void Disabled () {}
        private void OnDisable() => Disabled();

        public static bool TryCreateNewInstance (Scene scene, out T instance)
        {
            instance = null;
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError($"Unable to create instance of: \"{typeof(T).Name}\", the save the scene first.");
                return false;
            }

            GameObject go = new GameObject("SceneObjectRegistry");
            SceneManager.SetActiveScene(scene);

            instance = go.AddComponent<T>();
            Register(scene.path, instance);

            return instance;
        }

        [SingletonScriptableObjectTryGetInstanceMarker]
        public static bool TryGetSceneInstance (string scenePath, out T instance, bool throwException = true)
        {
            if (!sceneInstances.TryGetValue(scenePath, out instance))
            {
                var instances = FindObjectsOfType<T>();
                if (instances.Length == 0)
                {
                    if (throwException)
                        throw new System.Exception($"There are no instances of type: \"{typeof(T).FullName} in scene: \"{scenePath}\".");
                    return false;
                }

                for (int i = 0; i < instances.Length; i++)
                {
                    if (instances[i].gameObject.scene.path != scenePath)
                        continue;

                    if (instance != null)
                    {
                        if (!Application.isPlaying)
                            GameObject.DestroyImmediate(instances[i].gameObject);
                        else GameObject.Destroy(instances[i].gameObject);
                        continue;
                    }

                    instance = instances[i];
                }

                return false;
            }

            return true;
        }

        protected virtual void Destroying () {}
        private void OnDestroy()
        {
            if (sceneInstances.TryGetValue(serializedScenePath, out var instance))
                sceneInstances.Remove(serializedScenePath);
            Destroying();
        }

        private static void Register (string serializedScenePath, T instance, bool throwException = true)
        {
            if (string.IsNullOrEmpty(serializedScenePath))
                throw new System.Exception($"Unable to register instance of: \"{typeof(T).FullName}\", it's serialized scene path is invalid!");

            if (sceneInstances.ContainsKey(serializedScenePath))
            {
                if (throwException)
                    throw new System.Exception($"Scene: \"{serializedScenePath}\" contains two instances of: \"{typeof(T).FullName}\".");
                return;
            }

            Debug.Log($"Registered instance of: \"{typeof(T).FullName}\" in scene: \"{serializedScenePath}\".");
            sceneInstances.Add(serializedScenePath, instance);
        }

        protected virtual void OnDeserialize () {}
        public void OnAfterDeserialize()
        {
            Register(serializedScenePath, this as T, throwException: false);
            OnDeserialize();
        }

        protected virtual void OnSerialize () {}
        public void OnBeforeSerialize() 
        {
            serializedScenePath = gameObject.scene.path;
            sceneInstances.Clear();
            OnSerialize();
        }
    }
}
