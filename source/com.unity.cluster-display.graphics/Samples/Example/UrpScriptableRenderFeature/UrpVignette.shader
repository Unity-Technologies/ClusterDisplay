Shader "Hidden/ClusterDisplay/Samples/URP/Vignette"
{
    SubShader
    {
        // See https://forum.unity.com/threads/package-requirements-in-shaderlab.1108832/
        PackageRequirements { "com.unity.render-pipelines.universal" : "14.0.0" }

        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment Fragment
            
            // Note the multi-compile statement necessary to toggle Cluster related shader features.
            #pragma multi_compile_fragment _ SCREEN_COORD_OVERRIDE

            // This file provides Cluster related shader features.
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ScreenCoordOverride.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Fullscreen.hlsl"

            TEXTURE2D_X(_SourceTex);
            SAMPLER(sampler_SourceTex);

            float4 _Color;
            float _Intensity;

            half4 Fragment (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                half4 baseColor = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_SourceTex, input.uv);

                // Transform UVs to Cluster Space to evaluate the vignette.
                float2 transformedUV = SCREEN_COORD_APPLY_SCALEBIAS(input.uv);

                float2 dist = abs(transformedUV - float2(0.5, 0.5)) * 2 * _Intensity;

                // We use the Cluster aspect ratio to ensure the vignette is rounded.
                dist.x *= SCREEN_SIZE_OVERRIDE.x / SCREEN_SIZE_OVERRIDE.y;

                float t = pow(saturate(1.0 - dot(dist, dist)), 2);
                return lerp(_Color, baseColor, t);
            }

            ENDHLSL
        }
    }
}
