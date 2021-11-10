#if CLUSTER_DISPLAY_URP
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    static class UrpUtility
    {
        static SerializedProperty GetRenderPasses()
        {
            // We assume that an asset must be set, and the array of ScriptableRenderData should not be empty.
            var urpAsset = GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;
            Assert.IsNotNull(urpAsset, $"Expected GraphicsSettings.renderPipelineAsset to be an instance of {nameof(UniversalRenderPipelineAsset)}");

            var rendererDataField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            var renderData = rendererDataField.GetValue(urpAsset) as ScriptableRendererData[];
            Assert.IsNotNull(renderData, $"{nameof(UniversalRenderPipelineAsset)} collection of {nameof(ScriptableRendererData)} is empty.");
            Assert.IsTrue(renderData.Length >= 0);

            var renderDataSerializedObject = new SerializedObject(renderData[0]);
            var rendererFeaturesProp = renderDataSerializedObject.FindProperty("m_RendererFeatures");
            Assert.IsTrue(rendererFeaturesProp.isArray);

            return rendererFeaturesProp;
        }

        /// <summary>
        /// Whether or not a render feature is currently active on the renderer.
        /// </summary>
        /// <remarks>
        /// Render features are URP's equivalent of post effects.
        /// </remarks>
        public static bool HasRenderFeature<T>(out T value) where T : ScriptableRendererFeature
        {
            var rendererFeaturesProp = GetRenderPasses();
            var arraySize = rendererFeaturesProp.arraySize;
            for (var i = 0; i != arraySize; ++i)
            {
                var renderFeatureElt = rendererFeaturesProp.GetArrayElementAtIndex(i);
                var objRef = renderFeatureElt.objectReferenceValue;
                Assert.IsTrue(objRef is ScriptableRendererFeature);

                if (objRef is T)
                {
                    value = objRef as T;
                    return true;
                }
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Adds a render feature to the renderer.
        /// </summary>
        /// <remarks>
        /// Render features are URP's equivalent of post effects.
        /// Use <see cref="HasRenderFeature{T}"/> to check whether a feature was already added or not.
        /// </remarks>
        public static T AddRenderFeature<T>() where T : ScriptableRendererFeature
        {
            var renderFeature = ScriptableObject.CreateInstance<T>();

            var renderPassesProp = GetRenderPasses();
            var renderPassMapProp = renderPassesProp.serializedObject.FindProperty("m_RendererFeatureMap");
            var target = renderPassesProp.serializedObject.targetObject;

            if (EditorUtility.IsPersistent(target))
                AssetDatabase.AddObjectToAsset(renderFeature, target);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderFeature, out _, out long localId);

            // Add render feature.
            renderPassesProp.arraySize++;
            var renderFeatureElt = renderPassesProp.GetArrayElementAtIndex(renderPassesProp.arraySize - 1);
            renderFeatureElt.objectReferenceValue = renderFeature;

            // Update GUID map.
            renderPassMapProp.arraySize++;
            var guidProp = renderPassMapProp.GetArrayElementAtIndex(renderPassMapProp.arraySize - 1);
            guidProp.longValue = localId;

            renderPassesProp.serializedObject.ApplyModifiedProperties();

            if (EditorUtility.IsPersistent(target))
            {
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
            }

            return renderFeature;
        }

        public static bool CurrentPipelineIsUrp() => GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset;
    }
}
#endif