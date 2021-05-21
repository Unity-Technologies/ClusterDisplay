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

        public static bool TryGetDefaultAssembly(out Assembly defaultAssembly) => (defaultAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
        {
            return assembly.GetName().Name == DefaultUserAssemblyName;
        }).FirstOrDefault()) != null;

        public static bool TryGetAssemblyByName (string assemblyName, out Assembly outAssembly) => (outAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name == assemblyName).FirstOrDefault()) != null;

        public static Type[] GetAllTypes (string filter, Assembly targetAssembly, bool includeGenerics = true)
        {
            Type[] types = new Type[1] {
                typeof(UnityEngine.Object)
            };

            string filterLower = !string.IsNullOrEmpty(filter) ? filter.ToLower() : null;
            return filterLower == null ?

                targetAssembly.GetTypes()
                .Where(type =>
                {
                    bool found = false;
                    for (int i = 0; i < types.Length; i++)
                        found |= type.IsSubclassOf(types[i]);
                    return found;
                })
                .Where(type => includeGenerics ? true : !type.IsGenericType)
                .ToArray() :

                targetAssembly.GetTypes()
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

                AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type =>
                {
                    bool found = false;
                    for (int i = 0; i < types.Length; i++)
                        found |= type.IsSubclassOf(types[i]);
                    return found;
                })
                .Where(type => includeGenerics ? true : !type.IsGenericType)
                .ToArray() :

                AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
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

        public static bool TryGetAllMethodsWithAttribute<T> (out MethodInfo[] methodInfos, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        {
            var targetAttribute = typeof(T);

            if (!TryGetDefaultAssembly(out var defaultAssembly))
            {
                methodInfos = null;
                return false;
            }

            return (methodInfos = defaultAssembly.GetTypes()
                .SelectMany(type => type.GetMethods(bindingFlags)
                    .Where(method => method.CustomAttributes
                        .Any(customAttribute => customAttribute.AttributeType == targetAttribute))).ToArray()).Length > 0;
        }
    }
}
