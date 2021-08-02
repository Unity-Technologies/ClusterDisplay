using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public class SingletonMonoBehaviourTryGetInstanceMarker : Attribute {}

    /*
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    */
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour, ISerializationCallbackReceiver where T : SingletonMonoBehaviour<T>
    {
        /*
#if UNITY_EDITOR
        static SingletonMonoBehaviour ()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged (UnityEditor.PlayModeStateChange playModeStateChange) => FlushInstanceReference();
        private static void FlushInstanceReference () => instance = null;
#endif
        */

        private static T instance;
        protected abstract void OnAwake();
        private void Awake()
        {
            instance = this as T;
            OnAwake();
        }

        [SingletonMonoBehaviourTryGetInstanceMarker]
        public static bool TryGetInstance (out T outInstance, bool throwException = true)
        {
            if (instance != null)
            {
                outInstance = instance;
                return true;
            }

            var instances = FindObjectsOfType<T>();
            if (instances.Length == 0)
            {
                if (throwException)
                    throw new System.Exception($"Unable to retrieve instance of: {typeof(T).FullName}, there are no instances of that type.");
                outInstance = null;
                return false;
            }

            if (instances.Length > 1)
            {
                if (throwException)
                    throw new System.Exception($"Unable to retrieve instance of: {typeof(T).FullName}, there is more than one instance of that type!");
                outInstance = null;
                return false;
            }

            outInstance = instance = instances[0];
            return true;
        }

        protected virtual void OnDeserialize() {}
        public void OnAfterDeserialize() => OnDeserialize();
        protected virtual void OnSerialize() {}
        public void OnBeforeSerialize() => OnSerialize();
    }
}
