using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SingletonScriptableObject<T> : ScriptableObject where T : SingletonScriptableObject<T>
{
    private static T instance;
    public static bool TryGetInstance (out T outInstance, bool throwException = true)
    {
        if (instance == null)
        {
            var instances = Resources.LoadAll<T>("");
            if (instances.Length == 0)
            {
                if (throwException)
                    throw new System.Exception($"There is no instance of: \"{typeof(T)}\" in Resources.");
                outInstance = null;
                return false;
            }

            if (instances.Length > 1)
            {
                if (throwException)
                    throw new System.Exception($"There is more than one instance of: \"{typeof(T)}\" in Resources.");
                outInstance = null;
                return false;
            }

            instance = instances[0];
        }

        outInstance = instance;
        return true;
    }
}
