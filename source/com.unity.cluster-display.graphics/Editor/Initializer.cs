using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [InitializeOnLoad]
    static class Initializer
    {
        static Initializer()
        {
            // Set the project's "Always included shaders" setting
            foreach (var type in TypeCache.GetTypesWithAttribute<RequiresUnreferencedShaderAttribute>())
            {
                const BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                var members = type.GetMembers(bindingFlags)
                    .Where(f => f.IsDefined(typeof(AlwaysIncludeShaderAttribute), false));

                foreach (var memberInfo in members)
                {
                    var shaderName = memberInfo.GetValue<string>();
                    if (Util.AddAlwaysIncludedShaderIfNeeded(shaderName))
                    {
                        Debug.Log($"Added {shaderName} to the list of Always Included shader.");
                    }
                }
            }

            // Sanity check.
            if (XRSettings.enabled)
            {
                Debug.LogWarning("XR is currently enabled which is not expected when using Cluster Display.");
            }
        }

        static T GetValue<T>(this MemberInfo memberInfo, object obj = null) =>
            memberInfo switch
            {
                FieldInfo fieldInfo => (T)fieldInfo.GetValue(obj),
                PropertyInfo propertyInfo => (T)propertyInfo.GetValue(obj),
                _ => throw new ArgumentOutOfRangeException(nameof(memberInfo))
            };
    }
}
