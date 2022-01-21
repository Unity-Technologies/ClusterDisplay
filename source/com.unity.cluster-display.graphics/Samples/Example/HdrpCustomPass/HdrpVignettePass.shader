Shader "ClusterDisplay/Samples/HDRP/CustomPass/Vignette"
{
    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
    
    // Note the multi-compile statement necessary to toggle Cluster Display related shader features.
    #pragma multi_compile_fragment _ SCREEN_COORD_OVERRIDE

    // These files provide Cluster Display related shader features.
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ScreenCoordOverride.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ScreenCoordOverride.hlsl"
    
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        float4 color = float4(0.0, 0.0, 0.0, 0.0);

        // Load the camera color buffer at the mip 0 if we're not at the before rendering injection point
        if (_CustomPassInjectionPoint != CUSTOMPASSINJECTIONPOINT_BEFORE_RENDERING)
        {
            color = float4(CustomPassLoadCameraColor(varyings.positionCS.xy, 0), 1);

            // Transform UVs to Cluster Space to evaluate the vignette.
            float2 transformedUV = SCREEN_COORD_APPLY_SCALEBIAS(posInput.positionNDC.xy);

            float2 dist = abs(transformedUV - float2(0.5, 0.5)) * 2;

            // We use the Cluster aspect ratio to ensure the vignette is rounded.
            dist.x *= SCREEN_SIZE_OVERRIDE.x / SCREEN_SIZE_OVERRIDE.y;

            float t = pow(saturate(1.0 - dot(dist, dist)), 2);
            return lerp(float4(0, 0, 0, 1), color, t);
        }

        return color;
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
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}
