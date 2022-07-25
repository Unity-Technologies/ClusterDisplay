Shader "Hidden/Test/CustomRenderPass"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        Pass
        {
            Name "CustomRenderPass"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma multi_compile _ SCREEN_COORD_OVERRIDE

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ScreenCoordOverride.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_SourceTex);
            SAMPLER(sampler_SourceTex);

            half4 Fragment (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 baseColor = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_SourceTex, input.texcoord);
                float2 transformedUV = SCREEN_COORD_APPLY_SCALEBIAS(input.texcoord);
                return half4(lerp(baseColor.rgb, float3(transformedUV, 0), 0.5), 1);
            }

            ENDHLSL
        }
    }
}
