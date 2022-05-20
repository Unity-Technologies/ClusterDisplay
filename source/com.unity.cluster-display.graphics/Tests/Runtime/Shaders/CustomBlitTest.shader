Shader "Hidden/Test/Custom Blit Test"
{
    SubShader
    {
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Fragment
                #include "CustomBlitTest.hlsl"
            ENDHLSL
        }

        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Fragment
                #define FLIP_GEOMETRY_VERTICAL
                #include "CustomBlitTest.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
