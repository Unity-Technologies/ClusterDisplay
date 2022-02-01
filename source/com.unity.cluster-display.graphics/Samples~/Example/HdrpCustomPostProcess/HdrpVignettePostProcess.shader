Shader "ClusterDisplay/Samples/HDRP/CustomPostProcess/Vignette"
{
    Properties
    {
        // This property is necessary to make the CommandBuffer.Blit bind the source texture to _MainTex
        _MainTex("Main Texture", 2DArray) = "grey" {}
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    // Note the multi-compile statement necessary to toggle Cluster Display related shader features.
    #pragma multi_compile_fragment _ SCREEN_COORD_OVERRIDE

    // These files provide Cluster Display related shader features.
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ScreenCoordOverride.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ScreenCoordOverride.hlsl"
    
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/FXAA.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    // List of properties to control your post process effect
    float _Intensity;
    float4 _Color;
    TEXTURE2D_X(_MainTex);

    float4 CustomPostProcess(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float4 sourceColor = SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, input.texcoord);

        // Transform UVs to Cluster Space to evaluate the vignette.
        float2 transformedUV = SCREEN_COORD_APPLY_SCALEBIAS(input.texcoord);

        float2 dist = abs(transformedUV - float2(0.5, 0.5)) * 2 * _Intensity;

        // We use the Cluster aspect ratio to ensure the vignette is rounded.
        dist.x *= SCREEN_SIZE_OVERRIDE.x / SCREEN_SIZE_OVERRIDE.y;

        float t = pow(saturate(1.0 - dot(dist, dist)), 2);
        return lerp(_Color, sourceColor, t);
    }

    ENDHLSL

    SubShader
    {

        // See https://forum.unity.com/threads/package-requirements-in-shaderlab.1108832/
        PackageRequirements { "com.unity.render-pipelines.high-definition" : "14.0.0" }
        
        Pass
        {
            Name "Vignette"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment CustomPostProcess
                #pragma vertex Vert
            ENDHLSL
        }
    }
    Fallback Off
}
