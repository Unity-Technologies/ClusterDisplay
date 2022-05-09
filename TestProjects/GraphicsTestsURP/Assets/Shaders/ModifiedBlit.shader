Shader "Hidden/Test/Modified Blit"
{
    SubShader
    {
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Fragment
                #include "ModifiedBlit.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
