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

        [SerializeField] public bool declaringAssemblyIsPostProcessable;
        [SerializeField] public string declaringAssemblyName;
        [SerializeField] public string declaryingTypeFullName;

        [SerializeField] public string declaringReturnTypeAssemblyName;
        [SerializeField] public string returnTypeFullName;

        [SerializeField] public string methodName;

        [SerializeField] public string[] declaringParameterTypeAssemblyNames;
        [SerializeField] public string[] parameterTypeFullNames;
        [SerializeField] public string[] parameterNames;

        public int ParameterCount => parameterNames.Length;

        public (string declaringParameterTypeAssemblyName, string parameterTypeFullName, string parameterName) this[int parameterIndex]
        {
            get =>
                parameterTypeFullNames != null ? (
                    declaringParameterTypeAssemblyNames[parameterIndex],
                    parameterTypeFullNames[parameterIndex], 
                    parameterNames[parameterIndex]) 

                : (null, null, null);
        }

        public static SerializedMethod Create (MethodInfo methodInfo)
        {
            var declaringType = methodInfo.DeclaringType;
            var declaringAssembly = declaringType.Assembly;

            string declaringTypeStr = declaringType.FullName;
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
                    declaryingTypeFullName = declaringTypeStr,

                    declaringReturnTypeAssemblyName = returnTypeAssemblyName,
                    returnTypeFullName = returnType.FullName,

                    methodName = methodInfo.Name,

                    declaringParameterTypeAssemblyNames = new string[0],
                    parameterTypeFullNames = new string[0],
                    parameterNames = new string[0],
                };
            }

            returnType = methodInfo.ReturnType;
            returnTypeAssemblyName = returnType.Assembly.GetName().Name;

            var parameterTypeAssemblyNames = new string[parameters.Length];
            var parameterTypeNames = new string[parameters.Length];
            var parameterNames = new string[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypeAssemblyNames[i] = parameters[i].ParameterType.Assembly.GetName().Name;
                parameterTypeNames[i] = parameters[i].ParameterType.FullName;
                parameterNames[i] = parameters[i].Name;
            }

            return new SerializedMethod
            {
                declaringAssemblyName = declaringAssemblystr,
                declaryingTypeFullName = declaringTypeStr,

                declaringReturnTypeAssemblyName = returnTypeAssemblyName,
                returnTypeFullName = returnType.FullName,

                methodName = methodInfo.Name,

                declaringParameterTypeAssemblyNames = parameterTypeAssemblyNames,
                parameterTypeFullNames = parameterTypeNames,
                parameterNames = parameterNames,
            };
        }
    }
}
