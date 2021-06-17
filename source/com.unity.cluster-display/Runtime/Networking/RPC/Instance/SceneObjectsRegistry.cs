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

        private List<Object> sceneObjects = new List<Object>();
        [SerializeField] private Object[] serializedSceneObjects;

        private void Awake() => RegisterObjects();

        private void OnDestroy() => Clear();
        private void Clear ()
        {
            if (serializedSceneObjects != null)
                UnregisterInstanceAccessors(serializedSceneObjects);

            sceneObjects.Clear();
            serializedSceneObjects = null;

            sceneObjectsRegistered = false;
        }

        public void Register<T> (T sceneObject) where T : Object
        {
            if (sceneObjects.Contains(sceneObject))
                return;

            sceneObjects.Add(sceneObject);
            RegisterInstanceAccessor(sceneObject);
        }

        public void Unregister<T> (T sceneObject, ushort rpcId) where T : Object
        {
            sceneObjects.Remove(sceneObject);
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

                sceneObjects.Add(serializedSceneObjects[i]);
                RegisterInstanceAccessor(serializedSceneObjects[i]);
            }

            serializedSceneObjects = sceneObjects.ToArray();
        }

        protected override void Destroying()
        {
            if (!Application.isPlaying)
                return;

            if (serializedSceneObjects != null)
                UnregisterInstanceAccessors(serializedSceneObjects);
        }

        private void RegisterObjects ()
        {
            if (sceneObjectsRegistered)
                return;

            if (!Application.isPlaying)
                return;

            if (serializedSceneObjects != null)
                RegisterInstanceAccessors(serializedSceneObjects);

            sceneObjectsRegistered = true;
        }
    }
}
