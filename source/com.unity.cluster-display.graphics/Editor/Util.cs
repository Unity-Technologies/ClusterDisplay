using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    static class Util
    {
        /// <summary>
        /// Ensure the proper runtime availability of the parametrized shader name.
        /// </summary>
        /// <remarks>
        /// Based on logic exposed here:
        /// https://forum.unity.com/threads/modify-always-included-shaders-with-pre-processor.509479/
        /// </remarks>
        /// <param name="shaderName">The name of the shader to validate.</param>
        public static bool AddAlwaysIncludedShaderIfNeeded(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"Could not find shader \"{shaderName}\"");
            }

            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var serializedObject = new SerializedObject(graphicsSettingsObj);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

            // Check if shader is already included.
            for (int i = 0; i < arrayProp.arraySize; ++i)
            {
                if (shader == arrayProp.GetArrayElementAtIndex(i).objectReferenceValue)
                {
                    return false;
                }
            }

            var arrayIndex = arrayProp.arraySize;
            arrayProp.InsertArrayElementAtIndex(arrayIndex);
            var arrayElem = arrayProp.GetArrayElementAtIndex(arrayIndex);
            arrayElem.objectReferenceValue = shader;

            serializedObject.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
