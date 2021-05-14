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
                if (throwException)
                    throw new System.Exception($"There is no isntance of type: \"{typeof(T).FullName} in scene: \"{scenePath}\".");
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

        private static void Register (string serializedScenePath, T instance)
        {
            if (string.IsNullOrEmpty(serializedScenePath))
                throw new System.Exception($"Unable to register instance of: \"{typeof(T).FullName}\", it's serialized scene path is invalid!");

            if (sceneInstances.ContainsKey(serializedScenePath))
                throw new System.Exception($"Scene: \"{serializedScenePath}\" contains two instances of: \"{typeof(T).FullName}\".");

            Debug.Log($"Registered instance of: \"{typeof(T).FullName}\" in scene: \"{serializedScenePath}\".");
            sceneInstances.Add(serializedScenePath, instance);
        }

        protected virtual void OnDeserialize () {}
        public void OnAfterDeserialize()
        {
            Register(serializedScenePath, this as T);
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
