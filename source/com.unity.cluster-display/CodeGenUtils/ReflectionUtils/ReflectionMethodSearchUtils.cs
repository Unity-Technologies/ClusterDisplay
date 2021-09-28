using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class DedicatedAttribute : Attribute {}

    public static partial class ReflectionUtils
    {
        private readonly static Dictionary<Type, MethodInfo> cachedMethodsWithDedicatedAttributes = new Dictionary<Type, MethodInfo>();

        public static bool DetermineIfMethodIsRPCCompatible (MethodInfo methodInfo)
        {
            if (methodInfo.IsAbstract)
            {
                LogWriter.LogError($"Instance method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType.Namespace}.{methodInfo.DeclaringType.Name}\" is unsupported because the type is abstract.");
                return false;
            }

            bool allCompatible = true;
            object lockObj = new object();

            Parallel.ForEach(methodInfo.GetParameters(), (parameterInfo) =>
            {
                ushort depth = 0;
                bool compatible = RecursivelyDetermineIfTypeIsCompatibleRPCParameter(methodInfo, parameterInfo, parameterInfo.ParameterType, ref depth);
                lock (lockObj)
                    allCompatible &= compatible;
            });

            return allCompatible;
        }

        public static MethodInfo[] GetCompatibleRPCMethods (
            System.Type type, 
            string filter)
        {
            List<MethodInfo> methods = new List<MethodInfo>(100);
            if (string.IsNullOrEmpty(filter))
                ParallelForeachMethod(type, (method) => 
                {
                    if (DetermineIfMethodIsRPCCompatible(method))
                        lock (methods)
                            methods.Add(method);

                    return true;
                });

            else ParallelForeachMethod(type, (method) => 
                {
                    if (method.Name.Contains(filter) && DetermineIfMethodIsRPCCompatible(method))
                        lock (methods)
                            methods.Add(method);

                    return true;
                });

            return methods.ToArray();
        }

        public static MethodInfo[] GetAllMethodsFromType (
            System.Type type, 
            string filter, 
            bool valueTypeParametersOnly = false,
            bool includeGenerics = true,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
        {
            List<MethodInfo> methods = new List<MethodInfo>(100);
            if (string.IsNullOrEmpty(filter))
                ParallelForeachMethod(type, (method) =>
                {
                    bool matchesCriteria =
                        !method.IsSpecialName && // Ignore properties.
                        (!includeGenerics ? !method.IsGenericMethod : true) && // Ignore generics.
                        (valueTypeParametersOnly ? method.GetParameters().All(parameterInfo => parameterInfo.ParameterType.IsValueType) : true);

                    if (matchesCriteria)
                        lock (methods)
                            methods.Add(method);

                    return true;
                }, bindingFlags);

            else ParallelForeachMethod(type, (method) =>
                {
                    bool matchesCriteria =
                        !method.IsSpecialName && // Ignore properties.
                        (!includeGenerics ? !method.IsGenericMethod : true) && // Ignore generics.
                        (method.Name.Contains(filter)) &&
                        (valueTypeParametersOnly ? method.GetParameters().All(parameterInfo => parameterInfo.ParameterType.IsValueType) : true);

                    if (matchesCriteria)
                        lock (methods)
                            methods.Add(method);

                    return true;
                }, bindingFlags);

            return methods.ToArray();
        }

        public static bool TryGetMethodWithDedicatedAttribute<AttributeType>(out MethodInfo methodInfo)
            where AttributeType : DedicatedAttribute =>
            TryGetMethodWithDedicatedAttribute(typeof(AttributeType), out methodInfo);

        public static bool TryGetMethodWithDedicatedAttribute (System.Type attributeType, out MethodInfo methodInfo)
        {
            if (attributeType == null)
            {
                LogWriter.LogError("Cannot get method with null attribute!");
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
                LogWriter.LogError($"There is more than one method with attribute: \"{attributeType.Name}\", this dedicated attribute should only be applied to one method.");
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

        public static void ParallelForeachMethod (
            Type type, 
            Func<MethodInfo, bool> callback, 
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        {
            Parallel.ForEach(type.GetMethods(bindingFlags), (methodInfo, loopState) =>
            {
                if (!callback(methodInfo))
                    loopState.Break();
            });
        }

        public static bool TryFindMethodWithMatchingSignature (Type type, MethodInfo methodToMatch, out MethodInfo matchedMethod)
        {
            MethodInfo foundMethod = null;
            ParallelForeachMethod(type, (method) =>
            {
                bool matching =
                    method.ReturnType.Assembly == methodToMatch.ReturnType.Assembly &&
                    method.ReturnType.FullName == methodToMatch.ReturnType.FullName &&
                    method.Name == methodToMatch.Name &&
                    MethodSignaturesAreEqual(method, methodToMatch);

                if(matching)
                {
                    foundMethod = method;
                    return false;
                }

                return true;
            });

            return (matchedMethod = foundMethod) != null;
        }
    }
}
