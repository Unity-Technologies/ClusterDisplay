using System;
using System.Linq;
using System.Reflection;

namespace Unity.ClusterDisplay
{
    public static partial class ReflectionUtils
    {
        public static (PropertyInfo, MethodInfo)[] GetAllPropertySetMethods (
            System.Type type, 
            string filter, 
            bool valueTypesOnly = false,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static) => 
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

        public static bool TryFindPropertyWithMatchingSignature (Type type, PropertyInfo propertyToMatch, bool matchGetSetters, out PropertyInfo propertyMatched) =>
            (propertyMatched = type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).FirstOrDefault(property =>
            {
                return
                    property.PropertyType.Assembly == propertyToMatch.PropertyType.Assembly &&
                    property.PropertyType.FullName == propertyToMatch.PropertyType.FullName &&
                    property.Name == propertyToMatch.Name &&
                    PropertySignaturesAreEqual(property, propertyToMatch, matchGetSetters);
            })) != null;
    }
}
