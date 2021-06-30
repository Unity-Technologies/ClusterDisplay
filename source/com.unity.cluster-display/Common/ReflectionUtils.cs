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
        public static string GetAssemblyLocation (string name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name).Location;

        private readonly static Dictionary<Assembly, Type[]> cachedAssemblyTypes = new Dictionary<Assembly, Type[]>();
        private static Type[] cachedAllTypes;

        private static Type[] CachedTypes
        {
            get
            {
                if (cachedAllTypes == null)
                    cachedAllTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()).ToArray();
                return cachedAllTypes;
            }
        }

        public static System.Type GetTypeByString (string typeString)
        {
            string[] split = typeString.Split('.');
            if (split.Length == 0)
                return null;

            try
            {
                return Type.GetType(typeString, true);
            } catch (System.Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }

        private static string scriptAssembliesPath;
        public static bool IsAssemblyPostProcessable (Assembly assembly)
        {
            if (string.IsNullOrEmpty(scriptAssembliesPath))
                scriptAssembliesPath = System.IO.Path.Combine(Application.dataPath, "../Library/ScriptAssemblies").Replace('\\', '/');
            return System.IO.File.Exists($"{scriptAssembliesPath}/{assembly.GetName().Name}.dll");
        }

        public static bool TypeIsInPostProcessableAssembly (Type type)
        {
            if (string.IsNullOrEmpty(scriptAssembliesPath))
                scriptAssembliesPath = System.IO.Path.Combine(Application.dataPath, "../Library/ScriptAssemblies").Replace('\\', '/');
            return System.IO.File.Exists($"{scriptAssembliesPath}/{type.Assembly.GetName().Name}.dll");
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
            return
                string.IsNullOrEmpty(filter) ?
                    type
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Where(method => DetermineIfMethodIsRPCCompatible(method)).ToArray()
                    :
                    type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Where(method =>
                                (method.Name.Contains(filter)) &&
                                DetermineIfMethodIsRPCCompatible(method)).ToArray();
        }

        public static FieldInfo[] GetAllFieldsFromType (
            System.Type type, 
            string filter, 
            bool valueTypesOnly = false,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
        {
            return
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
        }

        public static MethodInfo[] GetAllMethodsFromType (
            System.Type type, 
            string filter, 
            bool valueTypeParametersOnly = false,
            bool includeGenerics = true,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
        {
            return
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
        }

        public static (PropertyInfo, MethodInfo)[] GetAllPropertySetMethods (
            System.Type type, 
            string filter, 
            bool valueTypesOnly = false,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
        {
            return
                string.IsNullOrEmpty(filter) ?

                    type.GetProperties(bindingFlags)
                        .Where(property =>
                        {
                            return
                                property.SetMethod != null &&
                                (valueTypesOnly ? property.PropertyType.IsValueType : true);
                        })
                        .Select(property => (property, property.SetMethod))
                        .ToArray()

                    :

                    type.GetProperties(bindingFlags)
                        .Where(property =>
                        {
                            return
                                property.SetMethod != null &&
                                (valueTypesOnly ? property.PropertyType.IsValueType : true) &&
                                property.Name.Contains(filter);

                        })
                        .Select(property => (property, property.SetMethod))
                        .ToArray();
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

        public static Type[] GetAllTypes() => CachedTypes;

        public static Type[] GetAllTypes (Assembly assembly)
        {
            if (cachedAssemblyTypes.TryGetValue(assembly, out var types))
                return types;
            types = assembly.GetTypes();
            cachedAssemblyTypes.Add(assembly, types);
            return types;
        }

        public static Type[] GetAllTypes (string filter, Assembly targetAssembly, bool includeGenerics = true)
        {
            Type[] types = new Type[1] {
                typeof(UnityEngine.Object)
            };

            string filterLower = !string.IsNullOrEmpty(filter) ? filter.ToLower() : null;

            Type[] assemblyTypes = null;
            if (!cachedAssemblyTypes.TryGetValue(targetAssembly, out assemblyTypes))
            {
                assemblyTypes = targetAssembly.GetTypes();
                cachedAssemblyTypes.Add(targetAssembly, assemblyTypes);
            }

            return filterLower == null ?

                assemblyTypes
                    .Where(type =>
                    {
                        bool found = false;
                        for (int i = 0; i < types.Length; i++)
                            found |= type.IsSubclassOf(types[i]);
                        return found;
                    })
                    .Where(type => includeGenerics ? true : !type.IsGenericType)
                    .ToArray() :

                assemblyTypes
                    .Where(type =>
                    {
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

                CachedTypes
                    .Where(type =>
                    {
                        bool found = false;
                        for (int i = 0; i < types.Length; i++)
                            found |= type.IsSubclassOf(types[i]);
                        return found;
                    })
                    .Where(type => includeGenerics ? true : !type.IsGenericType)
                    .ToArray() :

                CachedTypes
                    .Where(type =>
                    {
                        bool found = false;
                        for (int i = 0; i < types.Length; i++)
                            found |= type.IsSubclassOf(types[i]);
                        return found;
                    })
                    .Where(type => type.FullName.ToLower().Contains(filterLower))
                    .Where(type => includeGenerics ? true : !type.IsGenericType)
                    .ToArray();
        }

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
    }
}
