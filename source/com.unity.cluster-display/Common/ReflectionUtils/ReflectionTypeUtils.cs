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

        public static bool RecursivelyDetermineIfTypeIsCompatibleRPCParameter (
            MethodInfo methodInfo, 
            ParameterInfo parameterInfo, 
            Type type,
            ref ushort structureDepth)
        {
            if (type.IsPrimitive)
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

                type = type.GetElementType();
            }

            else if (type.IsGenericType)
            {
                if (structureDepth > 0)
                    goto dynamicallySizedMemberTypeFailure;

                type = type.GenericTypeArguments[0];
            }

            ushort localDepth = structureDepth;

            return
                type.IsValueType &&
                type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .All(fieldInfo => RecursivelyDetermineIfTypeIsCompatibleRPCParameter(methodInfo, parameterInfo, fieldInfo.FieldType, ref localDepth));

            dynamicallySizedMemberTypeFailure:
            Debug.LogError($"Method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType.FullName}\" cannot be used as an RPC, the parameter: \"{parameterInfo.Name}\" is type: \"{parameterInfo.ParameterType.FullName}\" which contains dynamically allocated type: \"{type.FullName}\" somehwere in it's member hierarchy.");
            return false;
        }
    }
}
