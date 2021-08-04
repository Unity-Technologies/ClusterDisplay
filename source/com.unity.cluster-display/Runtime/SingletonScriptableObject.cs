using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public class SingletonScriptableObjectTryGetInstanceMarker : Attribute {}
    public class SingletonScriptableObject<T> : ScriptableObject where T : SingletonScriptableObject<T>
    {
        public static T instance;
        [SingletonScriptableObjectTryGetInstanceMarker]
        public static bool TryGetInstance (out T outInstance, bool throwError = true)
        {
            if (instance == null)
            {
                var instances = Resources.LoadAll<T>("");
                if (instances.Length == 0)
                {
                    if (throwError)
                    {
                        Debug.LogError($"There is no instance of: \"{typeof(T)}\" in Resources.");
                        outInstance = null;
                        return false;
                    }

                    outInstance = null;
                    return false;
                }

                if (instances.Length > 1)
                {
                    if (throwError)
                    {
                        Debug.LogError($"There is more than one instance of: \"{typeof(T)}\" in Resources.");
                        outInstance = null;
                        return false;
                    }

                    outInstance = null;
                    return false;
                }

                instance = instances[0];
            }

            outInstance = instance;
            return true;
        }
    }
}
