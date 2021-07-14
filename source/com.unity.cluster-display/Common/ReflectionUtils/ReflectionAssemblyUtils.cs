using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public static partial class ReflectionUtils
    {
        public const string DefaultUserAssemblyName = "Assembly-CSharp";

        private readonly static Dictionary<Assembly, Type[]> cachedAssemblyTypes = new Dictionary<Assembly, Type[]>();
        public static string GetAssemblyLocation (string name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name).Location;

        private static string scriptAssembliesPath;
        public static bool IsAssemblyPostProcessable (Assembly assembly)
        {
            if (string.IsNullOrEmpty(scriptAssembliesPath))
                scriptAssembliesPath = System.IO.Path.Combine(Application.dataPath, "../Library/ScriptAssemblies").Replace('\\', '/');
            return System.IO.File.Exists($"{scriptAssembliesPath}/{assembly.GetName().Name}.dll");
        }

    }
}
