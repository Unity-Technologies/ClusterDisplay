using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public static class ReflectionUtils
    {
        public const string DefaultUserAssemblyName = "Assembly-CSharp";
        public static System.Type GetTypeByString (string typeString)
        {
            string[] split = typeString.Split('.');
            if (split.Length == 0)
                return null;
            var className = split[split.Length - 1];
            System.Type type = null;
            try
            {
                type = Type.GetType(typeString, true);
            } catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }

            return type;
        }

        public static bool DetermineIfMethodIsRPCCompatible (MethodInfo methodInfo)
        {
            ushort depth = 0;
            return methodInfo
                .GetParameters()
                .All(parameterInfo => RecursivelyDetermineIfTypeIsCompatibleRPCParameter(methodInfo, parameterInfo, parameterInfo.ParameterType, ref depth));
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

            ushort localDepth = structureDepth;

            return
                type.IsValueType &&
                type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .All(fieldInfo => RecursivelyDetermineIfTypeIsCompatibleRPCParameter(methodInfo, parameterInfo, fieldInfo.FieldType, ref localDepth));

            dynamicallySizedMemberTypeFailure:
            Debug.LogError($"Method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType.FullName}\" cannot be used as an RPC, the parameter: \"{parameterInfo.Name}\" is type: \"{parameterInfo.ParameterType.FullName}\" which contains dynamically allocated type: \"{type.FullName}\" somehwere in it's member hierarchy.");
            return false;
        }

        public static MethodInfo[] GetMethodsWithRPCCompatibleParamters (
            System.Type type, 
            string filter)
        {
            if (!TryGetDefaultAssembly(out var defaultAssembly))
                return new MethodInfo[0];

            return
                string.IsNullOrEmpty(filter) ?

                    type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Where(method =>
                        {
                            ushort depth = 0;
                            return
                                !method.IsGenericMethod &&
                                method.DeclaringType.Assembly == defaultAssembly &&
                                method.GetParameters().All(parameterInfo => RecursivelyDetermineIfTypeIsCompatibleRPCParameter(method, parameterInfo, parameterInfo.ParameterType, ref depth));
                        })
                        .ToArray()

                    :

                    type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Where(method =>
                        {
                            ushort depth = 0;
                            return
                                !method.IsGenericMethod &&
                                method.DeclaringType.Assembly == defaultAssembly &&
                                (method.Name.Contains(filter)) &&
                                method.GetParameters().All(parameterInfo => RecursivelyDetermineIfTypeIsCompatibleRPCParameter(method, parameterInfo, parameterInfo.ParameterType, ref depth));
                        }).ToArray();
                        
        }

        public static MethodInfo[] GetAllMethodsFromType (
            System.Type type, 
            string filter, 
            bool valueTypeParametersOnly = false,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public, 
            bool includeGenerics = true)
        {
            return
                string.IsNullOrEmpty(filter) ?

                    type.GetMethods(bindingFlags)
                        .Where(method =>
                        {
                            return
                                (!includeGenerics ? !method.IsGenericMethod : true) &&
                                (valueTypeParametersOnly ? method.GetParameters().All(parameterInfo => parameterInfo.ParameterType.IsValueType) : true);

                        })
                        .ToArray()

                    :

                    type.GetMethods(bindingFlags)
                        .Where(method =>
                        {
                            return
                                (!includeGenerics ? !method.IsGenericMethod : true) &&
                                (method.Name.Contains(filter)) &&
                                (valueTypeParametersOnly ? method.GetParameters().All(parameterInfo => parameterInfo.ParameterType.IsValueType) : true);

                        }).ToArray();
                        
        }

        // From https://stackoverflow.com/a/1312321
        public static string GetMethodSignature(this MethodInfo mi)
        {
            String[] param = mi.GetParameters()
                               .Select(p => String.Format("{0} {1}",p.ParameterType.Name,p.Name))
                               .ToArray();

            string signature = String.Format("{0} {1}({2})", mi.ReturnType.Name, mi.Name, String.Join(",", param));

            return signature;
        }

        public static System.Type GetMemberType(MemberInfo memberInfo)
        {
            return
                memberInfo is FieldInfo ?
                (memberInfo as FieldInfo).FieldType :
                (memberInfo as PropertyInfo).PropertyType;
        }


        public static (FieldInfo[], PropertyInfo[]) GetAllValueTypeFieldsAndProperties(System.Type targetType)
        {
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            List<FieldInfo> fields = targetType.GetFields(bindingFlags).ToList();
            List<PropertyInfo> properties = targetType.GetProperties(bindingFlags).ToList();

            List<FieldInfo> serializedFields = new List<FieldInfo>();
            List<PropertyInfo> serializedProperties = new List<PropertyInfo>();

            System.Type baseType = targetType.BaseType;

            while (baseType != typeof(object))
            {
                fields.AddRange(baseType.GetFields(bindingFlags));
                properties.AddRange(baseType.GetProperties(bindingFlags).Where(propertyInfo => propertyInfo.GetGetMethod(true) != null));
                baseType = baseType.BaseType;
            }

            var distinctFields = fields.Distinct();
            var distinctProperties = properties.Distinct();

            foreach (var field in distinctFields)
            {
                if (!field.FieldType.IsValueType || (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null))
                    continue;

                serializedFields.Add(field);
            }

            foreach (var property in distinctProperties)
            {
                if (!property.PropertyType.IsValueType)
                    continue;
                serializedProperties.Add(property);
            }

            return (serializedFields.ToArray(), serializedProperties.ToArray());
        }

        private static Assembly cachedDefaultAssembly = null;
        public static bool TryGetDefaultAssembly(out Assembly defaultAssembly, bool logError = true)
        {
            if (cachedDefaultAssembly != null)
            {
                defaultAssembly = cachedDefaultAssembly;
                return true;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            defaultAssembly = cachedDefaultAssembly = assemblies.FirstOrDefault(assembly => assembly.GetName().Name == DefaultUserAssemblyName);
            if (defaultAssembly == null)
            {
                if (logError)
                    Debug.LogError($"Unable to find assembly with name: \"{DefaultUserAssemblyName}\".");
                return false;
            }

            return true;
        }

        private readonly static Dictionary<string, Assembly> cachedAssemblies = new Dictionary<string, Assembly>();
        public static bool TryGetAssemblyByName (string assemblyName, out Assembly outAssembly, bool logError = true)
        {
            bool somethingIsCached = cachedAssemblies.TryGetValue(assemblyName, out outAssembly);
            if (somethingIsCached && outAssembly != null)
                return true;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            outAssembly = assemblies.FirstOrDefault(assembly => assembly.GetName().Name == assemblyName);
            if (outAssembly == null)
            {
                if (logError)
                    Debug.LogError($"Unable to find assembly with name: \"{assemblyName}\".");
                return false;
            }

            if (!somethingIsCached)
                cachedAssemblies.Add(assemblyName, outAssembly);
            else cachedAssemblies[assemblyName] = outAssembly;
            return true;
        }

        public static Type[] GetAllTypes ()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    Type[] assemblyTypes = null;
                    try
                    {
                        assemblyTypes = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        assemblyTypes = e.Types;
                    }

                    return assemblyTypes;
                }).ToArray();
        }

        public static Type[] GetAllTypes (Assembly assembly)
        {
            Type[] assemblyTypes = null;
            try
            {
                assemblyTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                assemblyTypes = e.Types;
            }

            return assemblyTypes;
        }

        public static Type[] GetAllTypes (string filter, Assembly targetAssembly, bool includeGenerics = true)
        {
            Type[] types = new Type[1] {
                typeof(UnityEngine.Object)
            };

            string filterLower = !string.IsNullOrEmpty(filter) ? filter.ToLower() : null;

            Type[] targetAssemblyTypes = null;
            try
            {
                targetAssemblyTypes = targetAssembly.GetTypes();
            } catch (ReflectionTypeLoadException e)
            {
                targetAssemblyTypes = e.Types;
            }

            return filterLower == null ?

                targetAssemblyTypes
                    .Where(type =>
                    {
                        if (type == null)
                            return false;

                        bool found = false;
                        for (int i = 0; i < types.Length; i++)
                            found |= type.IsSubclassOf(types[i]);
                        return found;
                    })
                    .Where(type => includeGenerics ? true : !type.IsGenericType)
                    .ToArray() :

                targetAssemblyTypes
                    .Where(type =>
                    {
                        if (type == null)
                            return false;

                        bool found = false;
                        for (int i = 0; i < types.Length; i++)
                            found |= type.IsSubclassOf(types[i]);
                        return found;
                    })
                    .Where(type => type.FullName.ToLower().Contains(filterLower))
                    .Where(type => includeGenerics ? true : !type.IsGenericType)
                    .ToArray();
        }

        public static Type[] GetAllTypes (string filter, bool includeGenerics = true)
        {
            Type[] types = new Type[1] {
                typeof(UnityEngine.Object)
            };

            string filterLower = !string.IsNullOrEmpty(filter) ? filter.ToLower() : null;
            return filterLower == null ?

                AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(assembly =>
                    {
                        Type[] assemblyTypes = null;
                        try
                        {
                            assemblyTypes = assembly.GetTypes();
                        } catch (ReflectionTypeLoadException e)
                        {
                            assemblyTypes = e.Types;
                        }

                        return assemblyTypes;
                    })
                    .Where(type =>
                    {
                        if (type == null)
                            return false;

                        bool found = false;
                        for (int i = 0; i < types.Length; i++)
                            found |= type.IsSubclassOf(types[i]);
                        return found;
                    })
                    .Where(type => includeGenerics ? true : !type.IsGenericType)
                    .ToArray() :

                AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(assembly => 
                    {
                        Type[] assemblyTypes = null;
                        try
                        {
                            assemblyTypes = assembly.GetTypes();
                        } catch (ReflectionTypeLoadException e)
                        {
                            assemblyTypes = e.Types;
                        }

                        return assemblyTypes;
                    })
                    .Where(type =>
                    {
                        if (type == null)
                            return false;

                        bool found = false;
                        for (int i = 0; i < types.Length; i++)
                            found |= type.IsSubclassOf(types[i]);
                        return found;
                    })
                    .Where(type => type.FullName.ToLower().Contains(filterLower))
                    .Where(type => includeGenerics ? true : !type.IsGenericType)
                    .ToArray();
        }

        public static bool TryGetAllMethodsWithAttribute<T> (out MethodInfo[] methodInfos, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        {
            var targetAttribute = typeof(T);

            if (!TryGetDefaultAssembly(out var defaultAssembly))
            {
                methodInfos = null;
                return false;
            }

            Type[] types = null;
            try
            {
                types = defaultAssembly.GetTypes();
            } catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }

            return (methodInfos = types
                .Where(type => type != null)
                .SelectMany(type => type.GetMethods(bindingFlags)
                    .Where(method => method.CustomAttributes
                        .Any(customAttribute => customAttribute.AttributeType == targetAttribute))).ToArray()).Length > 0;
        }
    }
}
