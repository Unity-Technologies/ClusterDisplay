using System;
using System.Text;
using UnityEngine;

namespace Unity.ClusterDisplay.Utils
{
    static class TypeExtensions
    {
        class U<T> where T : unmanaged { }

        public static bool IsUnManaged(this Type t)
        {
            try
            {
                _ = typeof(U<>).MakeGenericType(t);
                return true;
            }
            catch (Exception) { return false; }
        }


        /// <summary>
        /// From http://stackoverflow.com/questions/401681/how-can-i-get-the-correct-text-definition-of-a-generic-type-using-reflection
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetFriendlyTypeName(this Type type)
        {
            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (!type.IsGenericType)
            {
                return type.FullName;
            }

            var builder = new StringBuilder();
            var name = type.Name;
            var index = name.IndexOf("`");
            builder.AppendFormat("{0}.{1}", type.Namespace, name.Substring(0, index));
            builder.Append('<');
            var first = true;
            foreach (var arg in type.GetGenericArguments())
            {
                if (!first)
                {
                    builder.Append(',');
                }
                builder.Append(GetFriendlyTypeName(arg));
                first = false;
            }
            builder.Append('>');
            return builder.ToString();
        }
    }
}
