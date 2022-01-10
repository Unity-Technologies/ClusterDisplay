Shader "Hidden/ClusterDisplay/Blit"
{
    SubShader
    {
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Fragment
                #include "Blit.hlsl"
            ENDHLSL
        }

        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            // We need to define the keyword before including shader code.
            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Fragment
                #define FLIP_GEOMETRY_VERTICAL
                #include "Blit.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
