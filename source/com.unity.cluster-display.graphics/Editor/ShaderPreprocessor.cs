#if CLUSTER_DISPLAY_URP
using System;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    public class ShaderPreprocessor : IPreprocessShaders
    {
        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (UniversalRenderPipeline.asset is not { useScreenCoordOverride: true })
            {
                throw new InvalidOperationException(
                    "Universal Render Pipeline asset does not use Screen Coordinates Override. " +
                    "You can fix this by selecting the \"Screen Coordinates Override\" checkbox " +
                    "in the \"Post-processing\" section of the asset.");
            }
        }
    }
}
#endif
