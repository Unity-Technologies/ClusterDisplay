using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;

namespace Unity.ClusterDisplay.Utils
{
    /// <summary>
    /// Contains utility methods for handling attributes.
    /// </summary>
    static class AttributeUtility
    {
        /// <summary>
        /// Gets an attribute instance decorating a member.
        /// </summary>
        /// <param name="member">The member on which to search for the attribute.</param>
        /// <param name="inherit">When true, inspects the ancestors of the <paramref name="member"/> for the attribute.</param>
        /// <typeparam name="T">The type of attribute to search for.</typeparam>
        /// <returns>The first matching attribute instance, or null if the attribute was not found on the type.</returns>
        public static T GetAttribute<T>(this MemberInfo member, bool inherit = false) where T : Attribute
        {
            return member.GetAttributes<T>().FirstOrDefault();
        }

        /// <summary>
        /// Gets all of the attribute instances decorating a member.
        /// </summary>
        /// <param name="member">The member on which to search for the attribute.</param>
        /// <param name="inherit">When true, inspects the ancestors of the <paramref name="member"/> for the attribute.</param>
        /// <typeparam name="T">The type of attribute to search for.</typeparam>
        /// <returns>A new array containing the attribute instances, or an empty array if the attribute was not found on the type.</returns>
        public static T[] GetAttributes<T>(this MemberInfo member, bool inherit = false) where T : Attribute
        {
            return member.GetCustomAttributes(typeof(T), inherit).Cast<T>().ToArray();
        }

        /// <summary>
        /// Searches all assemblies for all types decorated with a given attribute.
        /// </summary>
        /// <param name="inherit">When true, inspects the ancestors of each type for the attribute.</param>
        /// <typeparam name="T">The type of attribute to search for.</typeparam>
        /// <returns>A new array of tuples containing types using the attribute and the attribute instances.</returns>
        public static (Type type, T attribute)[] GetAllTypes<T>(bool inherit = false) where T : Attribute
        {
#if UNITY_EDITOR
            return TypeCache.GetTypesWithAttribute<T>()
#else
            return GetAllTypes()
#endif
                .Select(type => (type, type.GetAttribute<T>(inherit)))
                .Where(tuple => tuple.Item2 != null)
                .ToArray();
        }


        static Type[] GetAllTypes()
        {
            var allTypes = new ConcurrentBag<Type>();

            Parallel.ForEach(AppDomain.CurrentDomain.GetAssemblies(), assembly =>
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException typeLoadException)
                {
                    types = typeLoadException.Types;
                }

                foreach (var type in types)
                {
                    allTypes.Add(type);
                }
            });

            return allTypes.ToArray();
        }
    }
}
