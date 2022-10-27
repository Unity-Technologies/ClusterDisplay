using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    class EnsureShadersIncluded : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // Set the project's "Always included shaders" setting
            foreach (var type in TypeCache.GetTypesWithAttribute<RequiresUnreferencedShaderAttribute>())
            {
                const BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                var members = type.GetMembers(bindingFlags)
                    .Where(f => f.IsDefined(typeof(AlwaysIncludeShaderAttribute), false));

                foreach (var memberInfo in members)
                {
                    var shaderName = GetProperty<string>(memberInfo);
                    if (Util.AddAlwaysIncludedShaderIfNeeded(shaderName))
                    {
                        Debug.Log($"Added {shaderName} to the list of Always Included shader.");
                    }
                }
            }
        }

        static T GetProperty<T>(MemberInfo memberInfo, object obj = null) =>
            memberInfo switch
            {
                FieldInfo fieldInfo => (T)fieldInfo.GetValue(obj),
                PropertyInfo propertyInfo => (T)propertyInfo.GetValue(obj),
                _ => throw new ArgumentOutOfRangeException(nameof(memberInfo))
            };
    }
}
