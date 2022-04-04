using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace Unity.ClusterDisplay
{
    public class SingletonScriptableObjectTryGetInstanceMarker : Attribute {}

    public abstract class SingletonScriptableObject : ScriptableObject
    {
        protected internal static readonly List<Type> singletonScriptableObjectTypes = new List<Type>();
        internal static Type[] GetSingleScriptableObjectTypes () => singletonScriptableObjectTypes.ToArray();

        public static bool TryGetInstance(Type type, out SingletonScriptableObject outInstance, bool throwError = true)
        {
            UnityEngine.Object[] instances = null;
            try
            {
                #if UNITY_EDITOR
                instances = AssetDatabase
                    .FindAssets($"t:{type.Name}")
                    .Select(guid => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type))
                    .ToArray();
                #else
                instances = Resources.LoadAll("", type);
                #endif
            }
            
            catch (Exception e)
            {
                ClusterDebug.LogError($"The following exception occurred while attempting to load singleton {nameof(ScriptableObject)} instance of type: \"{type.Name}\".");
                ClusterDebug.LogException(e);
                
                outInstance = null;
                return false;
            }
            
            if (instances.Length == 0)
            {
                if (throwError)
                {
                    ClusterDebug.LogError($"There is no instance of: \"{type.Name}\" in Resources.");
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
                    ClusterDebug.LogError($"There is more than one instance of: \"{type.Name}\" in Resources.");
                    outInstance = null;
                    return false;
                }

                outInstance = null;
                return false;
            }

            outInstance = instances[0] as SingletonScriptableObject;
            if (outInstance == null)
            {
                ClusterDebug.LogError($"Attempted to retrieved {nameof(SingletonScriptableObject)} instance, but the retrieved type is not derived from our desired type!.");
                return false;
            }

            return true;
        }
    }
    
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class SingletonScriptableObject<T> : SingletonScriptableObject where T : SingletonScriptableObject<T>
    {
        static SingletonScriptableObject () =>
            singletonScriptableObjectTypes.Add(typeof(T));
        
        protected virtual void OnAwake() {}
        private void Awake()
        {
            instance = this as T;
            OnAwake();
        }

        public static T instance;

        private static bool TryRetrieveInstance(T[] instances, out T outInstance, bool throwError)
        {
            if (instances.Length == 0)
            {
                if (throwError)
                {
                    ClusterDebug.LogError($"There is no instance of: \"{typeof(T)}\" in Resources.");
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
                    ClusterDebug.LogError($"There is more than one instance of: \"{typeof(T)}\" in Resources.");
                    outInstance = null;
                    return false;
                }

                outInstance = null;
                return false;
            }

            outInstance = instances[0];
            return true;
        }

#if UNITY_EDITOR
        internal static bool TryGetInstanceOrCreate (out T outInstance)
        {
            if (instance == null)
            {
                if (TryGetInstance(out outInstance, throwError: false))
                {
                    return true;
                }

                instance = CreateInstance<T>();

                var path = "Assets/Cluster Display/";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                AssetDatabase.CreateAsset(instance, $"{path}/{typeof(T).Name}.asset");
                AssetDatabase.SaveAssets();
            }

            outInstance = instance;
            return true;
        }
#endif

        [SingletonScriptableObjectTryGetInstanceMarker]
        public static bool TryGetInstance (out T outInstance, bool throwError = true)
        {
            if (instance == null)
            {
                T[] instances = null;
                try
                {
                    #if UNITY_EDITOR
                    instances = AssetDatabase
                        .FindAssets($"t:{typeof(T).Name}")
                        .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                        .ToArray();
                    #else
                    instances = Resources.LoadAll<T>("");
                    #endif
                }
                
                catch (Exception e)
                {
                    ClusterDebug.LogError($"The following exception occurred while attempting to load singleton {nameof(ScriptableObject)} instance of type: \"{typeof(T).Name}\".");
                    ClusterDebug.LogException(e);
                    outInstance = null;
                    return false;
                }
                
                if (!TryRetrieveInstance(instances, out instance, throwError))
                {
                    outInstance = null;
                    return false;
                }
            }
            
            outInstance = instance;
            return true;
        }
    }
}
