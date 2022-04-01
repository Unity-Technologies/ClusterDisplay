using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace Unity.ClusterDisplay
{
    internal static partial class ReflectionUtils
    {
        public const string DefaultUserAssemblyName = "Assembly-CSharp";

        private readonly static Dictionary<Assembly, Type[]> cachedAssemblyTypes = new Dictionary<Assembly, Type[]>();
        public static bool TryGetAssemblyLocation (string name, out string location)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
            if (assembly == null)
            {
                location = null;
                return false;
            }

            location = assembly.Location;
            return true;
        }

        private static string scriptAssembliesPath;
        public static bool IsAssemblyPostProcessable (string rootFolder, Assembly assembly)
        {
            if (string.IsNullOrEmpty(scriptAssembliesPath))
                scriptAssembliesPath = System.IO.Path.Combine(rootFolder, "../Library/ScriptAssemblies").Replace('\\', '/');
            return System.IO.File.Exists($"{scriptAssembliesPath}/{assembly.GetName().Name}.dll");
        }

    }
}
