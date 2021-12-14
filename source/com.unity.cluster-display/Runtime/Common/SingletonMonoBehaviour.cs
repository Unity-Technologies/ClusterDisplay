using System;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public class SingletonMonoBehaviourTryGetInstanceMarker : Attribute {}

    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
    {
        private static T instance;
        protected abstract void OnAwake();
        private void Awake()
        {
            instance = this as T;
            OnAwake();
        }

        [SingletonMonoBehaviourTryGetInstanceMarker]
        public static bool TryGetInstance (out T outInstance, bool logError = true)
        {
            if (instance != null)
            {
                outInstance = instance;
                return true;
            }

            var instances = FindObjectsOfType<T>(includeInactive: true);
            if (instances.Length == 0)
            {
                if (logError)
                    ClusterDebug.LogError($"Unable to retrieve instance of: {typeof(T).FullName}, there are no instances of that type.");
                outInstance = null;
                return false;
            }

            if (instances.Length > 1)
            {
                if (logError)
                    ClusterDebug.LogError($"Unable to retrieve instance of: {typeof(T).FullName}, there is more than one instance of that type!");
                outInstance = null;
                return false;
            }

            outInstance = instance = instances[0];
            return true;
        }
    }
}
