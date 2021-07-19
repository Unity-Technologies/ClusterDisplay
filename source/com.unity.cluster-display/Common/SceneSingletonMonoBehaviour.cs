using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.ClusterDisplay
{
    public class SceneSingletonMonoBehaviourTryGetInstanceMarker : Attribute {}
    [ExecuteAlways]
    public abstract class SceneSingletonMonoBehaviour<SceneInstanceType> : MonoBehaviour
        where SceneInstanceType : SceneSingletonMonoBehaviour<SceneInstanceType>
    {
        private static readonly Dictionary<string, SceneInstanceType> sceneInstances = new Dictionary<string, SceneInstanceType>();
        [SerializeField] private string serializedScenePath;

        protected virtual void Enabled () {}
        private void OnEnable()
        {
            serializedScenePath = gameObject.scene.path;
            Enabled();
        }

        protected virtual void Disabled () {}
        private void OnDisable() => Disabled();

        public static bool TryCreateNewInstance (Scene scene, out SceneInstanceType instance)
        {
            instance = null;
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError($"Unable to create instance of: \"{typeof(SceneInstanceType).Name}\", the save the scene first.");
                return false;
            }

            GameObject go = new GameObject("SceneObjectRegistry");
            SceneManager.SetActiveScene(scene);

            instance = go.AddComponent<SceneInstanceType>();
            Register(scene.path, instance);

            return instance;
        }

        [SingletonScriptableObjectTryGetInstanceMarker]
        public static bool TryGetSceneInstance (string scenePath, out SceneInstanceType instance, bool throwException = true)
        {
            if (!sceneInstances.TryGetValue(scenePath, out instance))
            {
                var instances = FindObjectsOfType<SceneInstanceType>();
                if (instances.Length == 0)
                {
                    if (throwException)
                        throw new System.Exception($"There are no instances of type: \"{typeof(SceneInstanceType).FullName} in scene: \"{scenePath}\".");
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

                    return (instance = instances[i]) != null;
                }

                return false;
            }

            return instance != null;
        }

        protected virtual void Destroying () {}
        private void OnDestroy()
        {
            if (sceneInstances.TryGetValue(serializedScenePath, out var instance))
                sceneInstances.Remove(serializedScenePath);
            Destroying();
        }

        private static void Register (string serializedScenePath, SceneInstanceType instance, bool throwException = true)
        {
            if (string.IsNullOrEmpty(serializedScenePath))
                throw new System.Exception($"Unable to register instance of: \"{typeof(SceneInstanceType).FullName}\", it's serialized scene path is invalid!");

            if (sceneInstances.ContainsKey(serializedScenePath))
            {
                if (sceneInstances[serializedScenePath] == null)
                    sceneInstances[serializedScenePath] = instance;

                else if (throwException)
                    throw new System.Exception($"Scene: \"{serializedScenePath}\" contains two instances of: \"{typeof(SceneInstanceType).FullName}\".");

                return;
            }

            Debug.Log($"Registered instance of: \"{typeof(SceneInstanceType).FullName}\" in scene: \"{serializedScenePath}\".");
            sceneInstances.Add(serializedScenePath, instance);
        }

        protected void DeserializeSceneSingletonInstance () 
        {
            Register(serializedScenePath, this as SceneInstanceType, throwException: false);
        }

        protected void SerializeSceneSingletonInstance () 
        {
            serializedScenePath = gameObject.scene.path;
            sceneInstances.Clear();
        }
    }
}
