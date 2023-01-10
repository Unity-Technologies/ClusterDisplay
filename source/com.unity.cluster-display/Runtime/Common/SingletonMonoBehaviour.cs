using System;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public class SingletonMonoBehaviourTryGetInstanceMarker : Attribute {}

    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
    {
        static T s_Instance;

        protected abstract void OnAwake();
        void Awake()
        {
            s_Instance = this as T;
            OnAwake();
        }

        [SingletonMonoBehaviourTryGetInstanceMarker]
        public static bool TryGetInstance (out T outInstance, bool logError = true, bool includeInactive = true)
        {
            if (s_Instance != null)
            {
                outInstance = s_Instance;
                return true;
            }

            var instances = FindObjectsOfType<T>(includeInactive);
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

            outInstance = s_Instance = instances[0];
            return true;
        }
    }
}
