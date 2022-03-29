#if UNITY_EDITOR
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay
{
    public static class RPCEditorGUICommon
    {
        public static void HorizontalLine () => EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        public static void ListMethods(
            string headerLabel,
            MethodInfo[] cachedMethods, 
            string methodSearchStr, 
            ref Vector2 scrollPosition, 
            System.Action<string> onChangeSearch,
            System.Action<MethodInfo> onSelectMethod)
        {
            EditorGUILayout.LabelField(headerLabel, EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(55));
            string newMethodSearchStr = EditorGUILayout.TextField(methodSearchStr, GUILayout.Width(250));
            EditorGUILayout.EndHorizontal();
            HorizontalLine();

            if (methodSearchStr != newMethodSearchStr)
                onChangeSearch(newMethodSearchStr);

            if (cachedMethods != null && cachedMethods.Length > 0)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, alwaysShowHorizontal: false, alwaysShowVertical: true, GUILayout.Height(250));

                for (int i = 0; i < cachedMethods.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Add", GUILayout.Width(45)))
                        onSelectMethod(cachedMethods[i]);

                    EditorGUILayout.LabelField(ReflectionUtils.GetMethodSignature(cachedMethods[i]));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }

            else EditorGUILayout.LabelField($"No methods found from search: \"{methodSearchStr}\".");
            HorizontalLine();
        }
    }
}
#endif
