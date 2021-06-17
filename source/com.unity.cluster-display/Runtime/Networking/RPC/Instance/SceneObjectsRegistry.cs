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
        private bool sceneObjectsRegistered = false;

        private readonly List<Object> sceneObjects = new List<Object>();
        [SerializeField] private Object[] serializedSceneObjects;

        private void Awake()
        {
            RegisterObjects();
        }

        protected override void Destroying() => Clear();

        private void Clear ()
        {
            if (!Application.isPlaying)
                return;

            UnregisterObjects();

            sceneObjects.Clear();
            serializedSceneObjects = null;
            sceneObjectsRegistered = false;
        }

        public void Register<T> (T sceneObject, bool isSerializing = false) where T : Object
        {
            if (sceneObject == null)
                return;

            if (sceneObjects.Contains(sceneObject))
                goto registerInstanceAccessor;

            sceneObjects.Add(sceneObject);

            registerInstanceAccessor:
            if (!isSerializing)
                RegisterInstanceAccessor(sceneObject);

        }

        public void Unregister<T> (T sceneObject, bool isSerializing = false) where T : Object
        {
            sceneObjects.Remove(sceneObject);

            if (!isSerializing)
                UnregisterInstanceAccessor(sceneObject);

            if (sceneObjects.Count == 0)
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
            if (sceneObjects.Count == 0)
                return;
            serializedSceneObjects = sceneObjects.ToArray();
        }

        protected override void OnDeserialize()
        {
            if (serializedSceneObjects == null)
                return;

            for (int i = 0; i < serializedSceneObjects.Length; i++)
            {
                if (serializedSceneObjects[i] == null)
                    continue;

                Register(serializedSceneObjects[i], isSerializing: true);
            }

            serializedSceneObjects = sceneObjects.ToArray();
        }

        private void RegisterObjects ()
        {
            if (sceneObjectsRegistered)
                return;

            if (!Application.isPlaying)
                return;

            if (serializedSceneObjects != null)
            {
                for (int i = 0; i < serializedSceneObjects.Length; i++)
                {
                    if (serializedSceneObjects[i] == null)
                        return;

                    Register(serializedSceneObjects[i]);
                }
            }

            sceneObjectsRegistered = true;
        }

        private void UnregisterObjects ()
        {
            if (serializedSceneObjects != null)
                for (int i = 0; i < serializedSceneObjects.Length; i++)
                    Unregister(serializedSceneObjects[i]);
        }
    }
}
