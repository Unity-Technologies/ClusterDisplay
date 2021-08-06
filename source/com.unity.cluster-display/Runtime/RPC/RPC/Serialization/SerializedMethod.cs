using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    [System.Serializable]
    public struct SerializedMethod
    {
        [SerializeField] public bool isStatic;

        // Where the assembly is IL post processable or not.
        [SerializeField] public bool        declaringAssemblyIsPostProcessable;
        [SerializeField] public string      declaringAssemblyName;

        // The type that declares the method -->
        [SerializeField] public string      declaringTypeNamespace;
        [SerializeField] public string      declaryingTypeName;

        [SerializeField] public string      declaringReturnTypeAssemblyName;
        [SerializeField] public string      returnTypeNamespace;
        [SerializeField] public string      returnTypeName;

        [SerializeField] public string      methodName;

        [SerializeField] public string[]    declaringParameterTypeAssemblyNames;
        [SerializeField] public string[]    parameterTypeNamespaces;
        [SerializeField] public string[]    parameterTypeName;
        [SerializeField] public string[]    parameterNames;

        public int ParameterCount => parameterNames.Length;

        public (string declaringParameterTypeAssemblyName, string parameterTypeNamespace, string parameterTypeName, string parameterName) this[int parameterIndex]
        {
            get =>
                parameterTypeName != null ? (
                    declaringParameterTypeAssemblyNames[parameterIndex],
                    parameterTypeNamespaces[parameterIndex], 
                    parameterTypeName[parameterIndex], 
                    parameterNames[parameterIndex]) 

                : (null, null, null, null);
        }

        private static string BuildTypeNamespace (System.Type type)
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

        public static SerializedMethod Create (MethodInfo methodInfo)
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

                return new SerializedMethod
                {
                    declaringAssemblyName = declaringAssemblystr,
                    declaringTypeNamespace = BuildTypeNamespace(declaringType),
                    declaryingTypeName = declaringType.Name,

                    declaringReturnTypeAssemblyName = returnTypeAssemblyName,
                    returnTypeNamespace = BuildTypeNamespace(returnType),
                    returnTypeName = returnType.Name,

                    methodName = methodInfo.Name,

                    declaringParameterTypeAssemblyNames = new string[0],
                    parameterTypeNamespaces = new string[0],
                    parameterTypeName = new string[0],
                    parameterNames = new string[0],
                };
            }

            returnType = methodInfo.ReturnType;
            returnTypeAssemblyName = returnType.Assembly.GetName().Name;

            var parameterTypeAssemblyNames = new string[parameters.Length];
            var parameterTypeNamespaces = new string[parameters.Length];
            var parameterTypeNames = new string[parameters.Length];
            var parameterNames = new string[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypeAssemblyNames[i] = parameters[i].ParameterType.Assembly.GetName().Name;
                parameterTypeNamespaces[i] = BuildTypeNamespace(parameters[i].ParameterType);
                parameterTypeNames[i] = parameters[i].ParameterType.Name;
                parameterNames[i] = parameters[i].Name;
            }

            return new SerializedMethod
            {
                declaringAssemblyName = declaringAssemblystr,
                declaringTypeNamespace = declaringType.Namespace,
                declaryingTypeName = declaringType.Name,

                declaringReturnTypeAssemblyName = returnTypeAssemblyName,
                returnTypeNamespace = returnType.Namespace,
                returnTypeName = returnType.Name,

                methodName = methodInfo.Name,

                declaringParameterTypeAssemblyNames = parameterTypeAssemblyNames,
                parameterTypeNamespaces = parameterTypeNamespaces,
                parameterTypeName = parameterTypeNames,
                parameterNames = parameterNames,
            };
        }
    }
}
