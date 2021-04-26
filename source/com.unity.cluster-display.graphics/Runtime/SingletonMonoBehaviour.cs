using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    public class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
    {
        private static T instance;
        public static bool TryGetInstance (out T outInstance, bool displayError = true)
        {
            if (instance != null)
            {
                outInstance = instance;
                return true;
            }

            var instances = FindObjectsOfType<T>();
            if (instances.Length == 0)
            {
                if (displayError)
                    Debug.LogErrorFormat($"Unable to retrieve instance of: {typeof(T).FullName}, there are no instances of that type.");
                outInstance = null;
                return false;
            }

            if (instances.Length > 1)
            {
                if (displayError)
                    Debug.LogErrorFormat($"Unable to retrieve instance of: {typeof(T).FullName}, there is more than one instance of that type!");
                outInstance = null;
                return false;
            }

            outInstance = instance = instances[0];
            return true;
        }
    }
}
