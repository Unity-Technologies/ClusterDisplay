using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
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
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    var range = Enumerable.Range(0, assemblies.Length);

                    List<Type>[] assemblyTypes = new List<Type>[assemblies.Length];

                    Parallel.ForEach(range, (assemblyIndex) =>
                    {
                        System.Type[] types = null;
                        try
                        {
                            types = assemblies[assemblyIndex].GetTypes();
                        }

                        catch (ReflectionTypeLoadException e)
                        {
                            types = e.Types;
                        }

                        for (int ti = 0; ti < types.Length; ti++)
                        {
                            if (types[ti] == null)
                                continue;

                            if (assemblyTypes[assemblyIndex] == null)
                            {
                                assemblyTypes[assemblyIndex] = new List<Type>() { types[ti] };
                                continue;
                            }

                            if (assemblyTypes[assemblyIndex].Contains(types[ti]))
                                continue;

                            assemblyTypes[assemblyIndex].Add(types[ti]);
                        }
                    });

                    cachedAllTypes = assemblyTypes
                        .Where(types => types != null)
                        .SelectMany(types => types).ToArray();
                }

                return cachedAllTypes;
            }
        }

        public static bool TryFindTypeByName (string typeName, out System.Type outType)
        {
            bool isArray = typeName.Contains("[]");
            if (isArray)
                typeName = typeName.Substring(0, typeName.Length - 2);
            
            System.Type foundType = null;
            ParallelForeachType((type) =>
            {
                if (type.Name != typeName)
                    return true;

                foundType = type;
                return false;
            });
            
            return (outType = isArray ? foundType.MakeArrayType() : foundType) != null;
        }
        
        public static bool TryFindTypeByNamespaceAndName (string namespaceStr, string typeName, out System.Type outType)
        {
            bool isArray = typeName.Contains("[]");
            if (isArray)
                typeName = typeName.Substring(0, typeName.Length - 2);

            System.Type foundType = null;
            ParallelForeachType((type) =>
            {
                if (type.Namespace != namespaceStr || type.Name != typeName)
                    return true;

                foundType = type;
                return false;
            });

            return (outType = isArray ? foundType.MakeArrayType() : foundType) != null;
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
                CodeGenDebug.LogException(exception);
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
                CodeGenDebug.LogException(exception);
            }
        }

        public static void ForeachNestedType(System.Type containerType, Func<System.Type, bool> callback) => RecursivelyForeachNestedType(containerType, callback);
    }
}
