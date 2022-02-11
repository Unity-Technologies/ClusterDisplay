#if CLUSTER_DISPLAY_URP
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    class ShaderPreprocessor : IPreprocessShaders
    {
        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (TryGetGlobalSettingsStripScreenCoordOverrideVariants(out var stripped) && stripped)
            {
                throw new InvalidOperationException(
                    "Screen Coordinates Override shader variants are stripped from Player builds. " +
                    "You can fix this by unselecting the \"Strip Screen Coord Override Variants\" checkbox " +
                    "in the Universal Render Pipeline Global Settings.");
            }
        }

        static bool TryGetGlobalSettingsStripScreenCoordOverrideVariants(out bool value)
        {
            try
            {
                var type = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings, Unity.RenderPipelines.Universal.Runtime");
                Assert.IsNotNull(type);

                var instanceProp = type.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(instanceProp);

                var instance = instanceProp.GetValue(null);
                Assert.IsNotNull(instance);

                var settingProp = type.GetProperty("stripScreenCoordOverrideVariants", BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(settingProp);

                value = (bool)settingProp.GetValue(instance);
                return true;
            }
            catch
            {
                Debug.LogError("Could not read Universal Render Pipeline Global Settings.");
            }

            value = true;
            return false;
        }
    }
}
#endif
