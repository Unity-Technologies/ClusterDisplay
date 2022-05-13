Shader "Hidden/Test/Custom Blit"
{
    SubShader
    {
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Fragment
                #include "CustomBlit.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
