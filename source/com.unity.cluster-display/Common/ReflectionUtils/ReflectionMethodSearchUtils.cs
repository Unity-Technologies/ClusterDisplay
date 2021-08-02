using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class DedicatedAttribute : Attribute {}

    public static partial class ReflectionUtils
    {
        private readonly static Dictionary<Type, MethodInfo> cachedMethodsWithDedicatedAttributes = new Dictionary<Type, MethodInfo>();

        public static MethodInfo[] GetCompatibleRPCMethods (
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

        public static FieldInfo[] GetAllFieldsFromType(
            System.Type type,
            string filter,
            bool valueTypesOnly = false,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static) =>

                string.IsNullOrEmpty(filter) ?

                    type.GetFields(bindingFlags)
                        .Where(field => (valueTypesOnly ? field.FieldType.IsValueType : true))
                        .ToArray()

                    :

                    type.GetFields(bindingFlags)
                        .Where(field =>
                                (valueTypesOnly ? field.FieldType.IsValueType : true) &&
                                field.Name.Contains(filter))
                        .ToArray();

        public static MethodInfo[] GetAllMethodsFromType (
            System.Type type, 
            string filter, 
            bool valueTypeParametersOnly = false,
            bool includeGenerics = true,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static) => 

                string.IsNullOrEmpty(filter) ?

                    type.GetMethods(bindingFlags)
                        .Where(method =>
                                !method.IsSpecialName &&
                                (!includeGenerics ? !method.IsGenericMethod : true) &&
                                (valueTypeParametersOnly ? method.GetParameters().All(parameterInfo => parameterInfo.ParameterType.IsValueType) : true))
                        .ToArray()

                    :

                    type.GetMethods(bindingFlags)
                        .Where(method =>
                                !method.IsSpecialName &&
                                (!includeGenerics ? !method.IsGenericMethod : true) &&
                                (method.Name.Contains(filter)) &&
                                (valueTypeParametersOnly ? method.GetParameters().All(parameterInfo => parameterInfo.ParameterType.IsValueType) : true))
                        .ToArray();

        public static bool TryGetMethodWithDedicatedAttribute<AttributeType>(out MethodInfo methodInfo)
            where AttributeType : DedicatedAttribute =>
            TryGetMethodWithDedicatedAttribute(typeof(AttributeType), out methodInfo);

        public static bool TryGetMethodWithDedicatedAttribute (System.Type attributeType, out MethodInfo methodInfo)
        {
            if (attributeType == null)
            {
                Debug.LogError("Cannot get method with null attribute!");
                methodInfo = null;
                return false;
            }

            if (cachedMethodsWithDedicatedAttributes.TryGetValue(attributeType, out methodInfo))
                return true;

            #if UNITY_EDITOR

            var methods = UnityEditor.TypeCache.GetMethodsWithAttribute(attributeType).ToArray();

            #else

            if (!TryGetAllMethodsWithAttribute(attributeType, out var methods))
                return false;

            #endif

            if (methods.Length == 0)
                return false;

            if (methods.Length > 1)
            {
                Debug.LogError($"There is more than one method with attribute: \"{attributeType.Name}\", this dedicated attribute should only be applied to one method.");
                return false;
            }

            methodInfo = methods.FirstOrDefault();
            if (methodInfo == null)
                return false;

            cachedMethodsWithDedicatedAttributes.Add(attributeType, methodInfo);
            return true;
        }

        public static bool TryGetAllMethodsWithAttribute<AttributeType>(
            out MethodInfo[] methodInfos,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            where AttributeType : Attribute =>
            TryGetAllMethodsWithAttribute(typeof(AttributeType), out methodInfos);

        public static bool TryGetAllMethodsWithAttribute (
            System.Type attributeType,
            out MethodInfo[] methodInfos, 
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        {
            #if UNITY_EDITOR

            methodInfos = UnityEditor.TypeCache.GetMethodsWithAttribute(attributeType).ToArray();
            return methodInfos.Length > 0;

            #else

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

                    if (methods[mi].GetCustomAttribute(attributeType) == null)
                        continue;

                    list.Add(methods[mi]);
                }
            }

            methodInfos = list.ToArray();
            return methodInfos.Length > 0;

            #endif
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
