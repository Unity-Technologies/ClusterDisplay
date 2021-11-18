using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

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

        Debug.Log($"Type: {typeof(T)}");
        
        /*
        var rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        List<T> instances = new List<T>(); 
        
        for (int i = 0; i < rootGameObjects.Length; i++)
        {
            var component = rootGameObjects[i].GetComponent<T>();
            var components = rootGameObjects[i].GetComponents<T>();
            
            if (component != null)
                instances.Add(component);
            
            if (components != null && components.Length > 0)
                instances.AddRange(components);
        }
        */

        // instances = instances.Distinct().ToList();
        Debug.Log(new StackTrace().ToString());
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

        outInstance = instance = instances[0] as T;
        return true;
    }
}
