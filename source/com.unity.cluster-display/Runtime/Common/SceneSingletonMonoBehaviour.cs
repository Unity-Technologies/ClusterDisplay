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

        protected virtual void Enabled () {}
        private void OnEnable() => Enabled();
        protected virtual void Disabled () {}
        private void OnDisable() => Disabled();

        protected virtual void Destroying () {}
        private void OnDestroy()
        {
            if (sceneInstances.TryGetValue(gameObject.scene.path, out var instance))
                sceneInstances.Remove(gameObject.scene.path);
            Destroying();
        }

        public static bool TryCreateNewInstance (Scene scene, out SceneInstanceType instance)
        {
            instance = null;
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                ClusterDebug.LogError($"Unable to create instance of: \"{typeof(SceneInstanceType).Name}\", the save the scene first.");
                return false;
            }

            GameObject go = new GameObject("SceneObjectRegistry");
            // SceneManager.SetActiveScene(scene);
            go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

            instance = go.AddComponent<SceneInstanceType>();
            Register(instance);

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

            instance.gameObject.hideFlags = HideFlags.None;
            return instance != null;
        }

        private static void Register (SceneSingletonMonoBehaviour<SceneInstanceType> baseInstance, bool throwError = true)
        {
            var path = baseInstance.gameObject.scene.path;

            if (string.IsNullOrEmpty(path))
            {
                ClusterDebug.LogError($"Unable to register instance of: \"{typeof(SceneInstanceType).FullName}\", it's serialized scene path is invalid!");
                return;
            }

            if (sceneInstances.ContainsKey(path))
            {
                if (sceneInstances[path] == null)
                    sceneInstances[path] = baseInstance as SceneInstanceType;

                else if (throwError)
                {
                    ClusterDebug.LogError($"Scene: \"{path}\" contains two instances of: \"{typeof(SceneInstanceType).FullName}\".");
                    return;
                }

                return;
            }

            ClusterDebug.Log($"Registered instance of: \"{typeof(SceneInstanceType).FullName}\" in scene: \"{path}\".");
            sceneInstances.Add(path, baseInstance as SceneInstanceType);
        }
    }
}
