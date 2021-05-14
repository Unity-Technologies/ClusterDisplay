using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public class SingletonMonoBehaviourTryGetInstanceMarker : Attribute {}

    public class SingletonMonoBehaviour<T> : MonoBehaviour, ISerializationCallbackReceiver where T : SingletonMonoBehaviour<T>
    {

        private static T instance;

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
        public void OnAfterDeserialize()
        {
            if (instance != null)
            {
                Debug.LogError($"Unable to cache instance to: \"{typeof(T).FullName}\", an instance has already cache!");
                return;
            }

            Debug.Log($"Cached instance to singleton of type: \"{typeof(T).FullName}\".");
            instance = this as T;
            OnDeserialize();
        }

        protected virtual void OnSerialize() {}
        public void OnBeforeSerialize() => OnSerialize();
    }
}
