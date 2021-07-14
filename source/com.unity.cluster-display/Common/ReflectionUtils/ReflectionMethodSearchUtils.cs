using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public static partial class ReflectionUtils
    {
        public static MethodInfo[] GetMethodsWithRPCCompatibleParamters (
            System.Type type, 
            string filter) =>
                string.IsNullOrEmpty(filter) ?
                    type
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Where(method => DetermineIfMethodIsRPCCompatible(method)).ToArray()
                    :
                    type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Where(method =>
                                (method.Name.Contains(filter)) &&
                                DetermineIfMethodIsRPCCompatible(method)).ToArray();

        public static FieldInfo[] GetAllFieldsFromType (
            System.Type type, 
            string filter, 
            bool valueTypesOnly = false,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static) =>
                string.IsNullOrEmpty(filter) ?

                    type.GetFields(bindingFlags)
                        .Where(field =>
                        {
                            return
                                (valueTypesOnly ? field.FieldType.IsValueType : true);
                        })
                        .ToArray()

                    :

                    type.GetFields(bindingFlags)
                        .Where(field =>
                        {
                            return
                                (valueTypesOnly ? field.FieldType.IsValueType : true) &&
                                field.Name.Contains(filter);

                        }).ToArray();

        public static MethodInfo[] GetAllMethodsFromType (
            System.Type type, 
            string filter, 
            bool valueTypeParametersOnly = false,
            bool includeGenerics = true,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static) => 
                string.IsNullOrEmpty(filter) ?

                    type.GetMethods(bindingFlags)
                        .Where(method =>
                        {
                            return
                                !method.IsSpecialName &&
                                (!includeGenerics ? !method.IsGenericMethod : true) &&
                                (valueTypeParametersOnly ? method.GetParameters().All(parameterInfo => parameterInfo.ParameterType.IsValueType) : true);

                        })
                        .ToArray()

                    :

                    type.GetMethods(bindingFlags)
                        .Where(method =>
                        {
                            return
                                !method.IsSpecialName &&
                                (!includeGenerics ? !method.IsGenericMethod : true) &&
                                (method.Name.Contains(filter)) &&
                                (valueTypeParametersOnly ? method.GetParameters().All(parameterInfo => parameterInfo.ParameterType.IsValueType) : true);

                        }).ToArray();

        public static bool TryGetAllMethodsWithAttribute<T> (
            out MethodInfo[] methodInfos, 
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            where T : Attribute
        {

            List<MethodInfo> list = new List<MethodInfo>();
            for (int ti = 0; ti < CachedTypes.Length; ti++)
            {
                var type = CachedTypes[ti];
                if (type == null)
                    continue;
                var methods = type.GetMethods(bindingFlags);
                for (int mi = 0; mi < methods.Length; mi++)
                {
                    if (methods[mi].CustomAttributes.Count() == 0)
                        continue;

                    if (methods[mi].GetCustomAttribute<T>() == null)
                        continue;

                    list.Add(methods[mi]);
                }
            }

            methodInfos = list.ToArray();
            return methodInfos.Length > 0;
        }

        public static bool TryFindMethodWithMatchingSignature (Type type, MethodInfo methodToMatch, out MethodInfo matchedMethod) =>
            (matchedMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).FirstOrDefault(method =>
            {
                return
                    method.ReturnType.Assembly == methodToMatch.ReturnType.Assembly &&
                    method.ReturnType.FullName == methodToMatch.ReturnType.FullName &&
                    method.Name == methodToMatch.Name &&
                    MethodSignaturesAreEqual(method, methodToMatch);
            })) != null;
    }
}
