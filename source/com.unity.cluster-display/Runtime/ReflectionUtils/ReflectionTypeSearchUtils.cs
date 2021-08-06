using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay
{
    public static partial class ReflectionUtils
    {
        private static Type[] cachedAllTypes;

        public static Type[] GetAllTypes() => CachedTypes;
        private static Type[] CachedTypes
        {
            get
            {
                if (cachedAllTypes == null)
                    cachedAllTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly =>
                    {
                        IEnumerable<Type> types = null;
                        try
                        {
                            types = assembly.GetTypes().Where(type => type != null);
                        }

                        catch (ReflectionTypeLoadException e)
                        {
                            types = e.Types.Where(type => type != null);
                        }

                        return types;

                    }).ToArray();

                return cachedAllTypes;
            }
        }

        public static System.Type GetTypeByFullName (string typeFullName)
        {
            System.Type foundType = null;
            ParallelForeachType((type) =>
            {
                if (type.FullName != typeFullName)
                    return true;

                foundType = type;
                return false;
            });

            return foundType;
        }

        public static Type[] GetAllTypes (Assembly assembly)
        {
            if (cachedAssemblyTypes.TryGetValue(assembly, out var types))
                return types;
            types = assembly.GetTypes();
            cachedAssemblyTypes.Add(assembly, types);
            return types;
        }

        public static Type[] GetAllTypes (string filter, Assembly targetAssembly, bool includeGenerics = true)
        {
            var unityEngineObjectType = typeof(UnityEngine.Object);
            List<Type> filteredTypes = new List<Type>(100);

            string filterLower = !string.IsNullOrEmpty(filter) ? filter.ToLower() : null;
            if (filterLower == null)
                ParallelForeachType(targetAssembly, (type) =>
                {
                    if (type.IsSubclassOf(unityEngineObjectType) && 
                        (includeGenerics ? true : !type.IsGenericType))
                        lock (filteredTypes)
                            filteredTypes.Add(type);
                    return true;
                });

            else ParallelForeachType(targetAssembly, (type) =>
                {
                    if (type.IsSubclassOf(unityEngineObjectType) && 
                        type.FullName.ToLower().Contains(filterLower) &&
                        (includeGenerics ? true : !type.IsGenericType))
                        lock (filteredTypes)
                            filteredTypes.Add(type);
                    return true;
                });
                
            return filteredTypes.ToArray();
        }

        public static Type[] GetAllTypes (string filter, bool includeGenerics = true)
        {
            var unityEngineObjectType = typeof(UnityEngine.Object);
            List<Type> filteredTypes = new List<Type>(100);

            string filterLower = !string.IsNullOrEmpty(filter) ? filter.ToLower() : null;
            if (filterLower == null)
                ParallelForeachType((type) =>
                {
                    if (type.IsSubclassOf(unityEngineObjectType) && 
                        (includeGenerics ? true : !type.IsGenericType))
                        lock (filteredTypes)
                            filteredTypes.Add(type);
                    return true;
                });

            else ParallelForeachType((type) =>
                {
                    if (type.IsSubclassOf(unityEngineObjectType) && 
                        type.FullName.ToLower().Contains(filterLower) &&
                        (includeGenerics ? true : !type.IsGenericType))
                        lock (filteredTypes)
                            filteredTypes.Add(type);
                    return true;
                });
                
            return filteredTypes.ToArray();
        }

        public static bool TryFindFirstDerrivedTypeFromBaseType(Type baseType, out Type derrivedType) =>
            (derrivedType = CachedTypes.FirstOrDefault(type => type.IsSubclassOf(baseType))) != null;

        private static bool RecursivelyForeachNestedType (Type containerType, Func<System.Type, bool> callback)
        {
            var nestedTypes = containerType.GetNestedTypes();
            if (nestedTypes == null || nestedTypes.Length == 0)
                return true;

            for (int nti = 0; nti < nestedTypes.Length; nti++)
                if (!callback(nestedTypes[nti]) || !RecursivelyForeachNestedType(nestedTypes[nti], callback))
                    return false;

            return true;
        }

        public static void ParallelForeachType (Func<System.Type, bool> callback)
        {
            var types = CachedTypes;
            try
            {
                Parallel.ForEach(types, (type, loopState) =>
                {
                    if (!callback(type) || !RecursivelyForeachNestedType(type, callback))
                        loopState.Break();
                });
            }

            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static void ParallelForeachType (Assembly targetAssembly, Func<System.Type, bool> callback)
        {

            Type[] assemblyTypes = null;
            if (!cachedAssemblyTypes.TryGetValue(targetAssembly, out assemblyTypes))
            {
                assemblyTypes = targetAssembly.GetTypes();
                cachedAssemblyTypes.Add(targetAssembly, assemblyTypes);
            }
            try
            {
                Parallel.ForEach(assemblyTypes, (type, loopState) =>
                {
                    if (!callback(type) || !RecursivelyForeachNestedType(type, callback))
                        loopState.Break();
                });
            }

            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static void ForeachNestedType(System.Type containerType, Func<System.Type, bool> callback) => RecursivelyForeachNestedType(containerType, callback);
    }
}
