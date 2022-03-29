using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public static partial class ReflectionUtils
    {
        public static System.Type GetMemberType(MemberInfo memberInfo) => 
            memberInfo is FieldInfo ?
            (memberInfo as FieldInfo).FieldType :
            (memberInfo as PropertyInfo).PropertyType;

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
    }
}
