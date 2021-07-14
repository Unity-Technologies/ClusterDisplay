using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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

        public static System.Type GetTypeByFullName (string typeFullName) => CachedTypes.FirstOrDefault(type => type.FullName == typeFullName);

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
            Type[] types = new Type[1] {
                typeof(UnityEngine.Object)
            };

            string filterLower = !string.IsNullOrEmpty(filter) ? filter.ToLower() : null;

            Type[] assemblyTypes = null;
            if (!cachedAssemblyTypes.TryGetValue(targetAssembly, out assemblyTypes))
            {
                assemblyTypes = targetAssembly.GetTypes();
                cachedAssemblyTypes.Add(targetAssembly, assemblyTypes);
            }

            return filterLower == null ?

                assemblyTypes
                    .Where(type =>
                    {
                        bool found = false;
                        for (int i = 0; i < types.Length; i++)
                            found |= type.IsSubclassOf(types[i]);
                        return found;
                    })
                    .Where(type => includeGenerics ? true : !type.IsGenericType)
                    .ToArray() :

                assemblyTypes
                    .Where(type =>
                    {
                        bool found = false;
                        for (int i = 0; i < types.Length; i++)
                            found |= type.IsSubclassOf(types[i]);
                        return found;
                    })
                    .Where(type => type.FullName.ToLower().Contains(filterLower))
                    .Where(type => includeGenerics ? true : !type.IsGenericType)
                    .ToArray();
        }

        public static Type[] GetAllTypes (string filter, bool includeGenerics = true)
        {
            Type[] types = new Type[1] {
                typeof(UnityEngine.Object)
            };

            string filterLower = !string.IsNullOrEmpty(filter) ? filter.ToLower() : null;
            return filterLower == null ?

                CachedTypes
                    .Where(type =>
                    {
                        bool found = false;
                        for (int i = 0; i < types.Length; i++)
                            found |= type.IsSubclassOf(types[i]);
                        return found;
                    })
                    .Where(type => includeGenerics ? true : !type.IsGenericType)
                    .ToArray() :

                CachedTypes
                    .Where(type =>
                    {
                        bool found = false;
                        for (int i = 0; i < types.Length; i++)
                            found |= type.IsSubclassOf(types[i]);
                        return found;
                    })
                    .Where(type => type.FullName.ToLower().Contains(filterLower))
                    .Where(type => includeGenerics ? true : !type.IsGenericType)
                    .ToArray();
        }
    }
}
