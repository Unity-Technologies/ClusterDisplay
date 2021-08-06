using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Unity.Collections;

namespace Unity.ClusterDisplay
{
    public static partial class ReflectionUtils
    {
        public static bool TypeIsInPostProcessableAssembly (Type type)
        {
            if (string.IsNullOrEmpty(scriptAssembliesPath))
                scriptAssembliesPath = System.IO.Path.Combine(Application.dataPath, "../Library/ScriptAssemblies").Replace('\\', '/');
            return System.IO.File.Exists($"{scriptAssembliesPath}/{type.Assembly.GetName().Name}.dll");
        }

        private static readonly Type NativeArrayType = typeof(NativeArray<>);
        public static bool RecursivelyDetermineIfTypeIsCompatibleRPCParameter (
            MethodInfo methodInfo, 
            ParameterInfo parameterInfo, 
            Type type,
            ref ushort structureDepth)
        {
            if (type.IsPrimitive)
                return true;

            else if (type.IsEnum)
                return true;

            if (type == typeof(string))
            {
                if (structureDepth > 0)
                    goto dynamicallySizedMemberTypeFailure;

                return true;
            }

            else if (type.IsArray)
            {
                if (structureDepth > 0)
                    goto dynamicallySizedMemberTypeFailure;

                ushort elementArgumentDepth = structureDepth;
                if (!RecursivelyDetermineIfTypeIsCompatibleRPCParameter(methodInfo, parameterInfo, type.GetElementType(), ref elementArgumentDepth))
                {
                    Debug.LogError($"Generic type argument: \"{type.GenericTypeArguments[0].Name}\" cannot be used as an RPC parameter.");
                    goto dynamicallySizedMemberTypeFailure;
                }
            }

            else if (type.IsGenericType)
            {
                if (type.Namespace == NativeArrayType.Namespace)
                {
                    if (type.GetGenericTypeDefinition() != NativeArrayType)
                    {
                        Debug.LogError($"Only the type: \"{NativeArrayType.Name}\" is supported as a RPC method parameter from: \"{NativeArrayType.Namespace}\" namespace.");
                        goto dynamicallySizedMemberTypeFailure;
                    }
                }

                if (structureDepth > 0)
                    goto dynamicallySizedMemberTypeFailure;

                ushort genericLenericArgumentDepth = structureDepth;
                if (!RecursivelyDetermineIfTypeIsCompatibleRPCParameter(methodInfo, parameterInfo, type.GenericTypeArguments[0], ref genericLenericArgumentDepth))
                {
                    Debug.LogError($"Generic type argument: \"{type.GenericTypeArguments[0].Name}\" cannot be used as an RPC parameter.");
                    goto dynamicallySizedMemberTypeFailure;
                }

                goto skipNativeArrayFieldsCheck;
            }

            else if (!type.IsValueType)
            {
                Debug.LogError($"Parameter: \"{parameterInfo.Name}\" is a reference type. Only primitive, strings, arrays, native arrays and structs are RPC compatible.");
                goto dynamicallySizedMemberTypeFailure;
            }

            ushort localDepth = structureDepth;

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool allCompatible = true;

            for (int fi = 0; fi < fields.Length; fi++)
                allCompatible &= RecursivelyDetermineIfTypeIsCompatibleRPCParameter(methodInfo, parameterInfo, fields[fi].FieldType, ref localDepth);

            return allCompatible;

            skipNativeArrayFieldsCheck:
            return true;

            dynamicallySizedMemberTypeFailure:
            Debug.LogError($"Method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType.Name}\" cannot be used as an RPC, the parameter: \"{parameterInfo.Name}\" is type: \"{parameterInfo.ParameterType.Name}\" which contains dynamically allocated type: \"{type.Name}\" somehwere in it's member hierarchy.");
            return false;
        }
    }
}
