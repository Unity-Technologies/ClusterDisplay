using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public static partial class ReflectionUtils
    {
        private static Assembly cachedDefaultAssembly = null;
        public static bool TryGetDefaultAssembly(out Assembly defaultAssembly, bool logError = true)
        {
            if (cachedDefaultAssembly != null)
            {
                defaultAssembly = cachedDefaultAssembly;
                return true;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            defaultAssembly = cachedDefaultAssembly = assemblies.FirstOrDefault(assembly => assembly.GetName().Name == DefaultUserAssemblyName);
            if (defaultAssembly == null)
            {
                if (logError)
                    Debug.LogError($"Unable to find assembly with name: \"{DefaultUserAssemblyName}\".");
                return false;
            }

            return true;
        }

        private readonly static Dictionary<string, Assembly> cachedAssemblies = new Dictionary<string, Assembly>();
        public static bool TryGetAssemblyByName (string assemblyName, out Assembly outAssembly, bool logError = true)
        {
            bool somethingIsCached = cachedAssemblies.TryGetValue(assemblyName, out outAssembly);
            if (somethingIsCached && outAssembly != null)
                return true;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            outAssembly = assemblies.FirstOrDefault(assembly => assembly.GetName().Name == assemblyName);
            if (outAssembly == null)
            {
                if (logError)
                    Debug.LogError($"Unable to find assembly with name: \"{assemblyName}\".");
                return false;
            }

            if (!somethingIsCached)
                cachedAssemblies.Add(assemblyName, outAssembly);
            else cachedAssemblies[assemblyName] = outAssembly;
            return true;
        }
    }
}
