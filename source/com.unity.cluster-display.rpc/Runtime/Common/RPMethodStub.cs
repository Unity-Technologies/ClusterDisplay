using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.RPC
{
    /// <summary>
    /// RPCs are serialized into an array of these structs and written to a stubs file, see serialization and deserialization in RPCRegistry.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RPMethodStub
    {
        public bool declaringAssemblyIsPostProcessable;
        public bool isStatic;
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string      declaringAssemblyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string      declaringTypeNamespace;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string      declaringTypeName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string      declaringReturnTypeAssemblyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string      returnTypeNamespace;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string      returnTypeName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string      methodName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] string     declaringParameterTypeAssemblyNamesStr;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] string     parameterTypeNamespacesStr;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] string     parameterTypeNameStr;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] string     parameterNamesStr;

        public string[] declaringParameterTypeAssemblyNames => GetArray(ref declaringParameterTypeAssemblyNamesStr);
        public string[] parameterTypeNamespaces => GetArray(ref parameterTypeNamespacesStr);
        public string[] parameterTypeName => GetArray(ref parameterTypeNameStr);
        public string[] parameterNames => GetArray(ref parameterNamesStr);
            
        public int ParameterCount
        {
            get
            {
                var array = GetArray(ref parameterNamesStr);
                return array != null ? array.Length : 0;
            }
        }

        readonly static Dictionary<int, string[]> cache = new Dictionary<int, string[]>();

        string[] GetArray(ref string str)
        {
            int hashCode = str.GetHashCode();
            if (!cache.TryGetValue(hashCode, out var cacheArray) || cacheArray == null || cacheArray.Length == 0)
                cacheArray = str.Split('\n');
            return cacheArray;
        }

        public string CacheAndGet(ref string str, int parameterIndex)
        {
            int hashCode = str.GetHashCode();
            var cacheArray = GetArray(ref str);
            if (cacheArray == null || cacheArray.Length == 0)
            {
                cacheArray = str.Split('\n');
                cache.Add(hashCode, cacheArray);
            }
            
            if (parameterIndex >= cacheArray.Length)
                return null;
            
            return cacheArray[parameterIndex];
        }

        public (string declaringParameterTypeAssemblyName, string parameterTypeNamespace, string parameterTypeName, string parameterName) this[int parameterIndex] =>
                !string.IsNullOrEmpty(parameterNamesStr) != null ? 
                    (CacheAndGet(ref declaringParameterTypeAssemblyNamesStr, parameterIndex),
                    CacheAndGet(ref parameterTypeNamespacesStr, parameterIndex),
                    CacheAndGet(ref parameterTypeNameStr, parameterIndex),
                    CacheAndGet(ref parameterNamesStr, parameterIndex))
                    : (null, null, null, null);

        static string BuildTypeNamespace (System.Type type)
        {
            if (!type.IsNested)
                return type.Namespace;

            var containingType = type;
            string nestedAddressStr = $"{containingType.Name}";

            while (containingType.DeclaringType != null)
            {
                containingType = containingType.DeclaringType;
                nestedAddressStr = $"{containingType.Name}/{nestedAddressStr}";
            }

            return !string.IsNullOrEmpty(containingType.Namespace) ? $"{containingType.Namespace}.{nestedAddressStr}" : nestedAddressStr;
        }

        public static RPMethodStub Create (MethodInfo methodInfo)
        {
            var declaringType = methodInfo.DeclaringType;
            var declaringAssembly = declaringType.Assembly;

            string declaringAssemblystr = declaringAssembly.GetName().Name;

            var parameters = methodInfo.GetParameters();

            System.Type returnType;
            string returnTypeAssemblyName;

            if (parameters.Length == 0)
            {
                returnType = methodInfo.ReturnType;
                returnTypeAssemblyName = returnType.Assembly.GetName().Name;

                return new RPMethodStub
                {
                    declaringAssemblyName = declaringAssemblystr,
                    declaringTypeNamespace = BuildTypeNamespace(declaringType),
                    declaringTypeName = declaringType.Name,

                    declaringReturnTypeAssemblyName = returnTypeAssemblyName,
                    returnTypeNamespace = BuildTypeNamespace(returnType),
                    returnTypeName = returnType.Name,

                    methodName = methodInfo.Name,
                };
            }

            returnType = methodInfo.ReturnType;
            returnTypeAssemblyName = returnType.Assembly.GetName().Name;

            var parameterTypeAssemblyNames = parameters[0].ParameterType.Assembly.GetName().Name;
            var parameterTypeNamespaces = BuildTypeNamespace(parameters[0].ParameterType);
            var parameterTypeNames = parameters[0].ParameterType.Name;
            var parameterNames = parameters[0].Name;

            for (int i = 1; i < parameters.Length; i++)
            {
                parameterTypeAssemblyNames = $"{parameterTypeAssemblyNames}\n{parameters[i].ParameterType.Assembly.GetName().Name}";
                parameterTypeNamespaces = $"{parameterTypeNamespaces}\n{BuildTypeNamespace(parameters[i].ParameterType)}";
                parameterTypeNames = $"{parameterTypeNames}\n{parameters[i].ParameterType.Name}";
                parameterNames = $"{parameterNames}\n{parameters[i].Name}";
            }
            
            return new RPMethodStub
            {
                declaringAssemblyName = declaringAssemblystr,
                declaringTypeNamespace = declaringType.Namespace,
                declaringTypeName = declaringType.Name,

                declaringReturnTypeAssemblyName = returnTypeAssemblyName,
                returnTypeNamespace = returnType.Namespace,
                returnTypeName = returnType.Name,

                methodName = methodInfo.Name,

                declaringParameterTypeAssemblyNamesStr = parameterTypeAssemblyNames,
                parameterTypeNamespacesStr = parameterTypeNamespaces,
                parameterTypeNameStr = parameterTypeNames,
                parameterNamesStr = parameterNames,
            };
        }
    }
}
