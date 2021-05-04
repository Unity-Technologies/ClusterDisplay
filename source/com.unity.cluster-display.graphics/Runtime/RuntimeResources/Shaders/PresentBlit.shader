Shader "ClusterDisplay/PresentBlit"
{
    HLSLINCLUDE

		#pragma target 4.5
		#pragma editor_sync_compilation
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

        Texture2D _BlitTexture;
        SamplerState sampler_PointClamp;
        SamplerState sampler_LinearClamp;
        SamplerState sampler_PointRepeat;
        SamplerState sampler_LinearRepeat;
        uniform float4 _BlitScaleBias;
        uniform float4 _BlitScaleBiasRt;
        uniform float _BlitMipLevel;
        uniform float2 _BlitTextureSize;
        uniform uint _BlitPaddingSize;
        uniform int _BlitTexArraySlice;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord   : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
            return output;
        }

        Varyings VertQuad(Attributes input)
        {
            Varyings output;
            output.positionCS = GetQuadVertexPosition(input.vertexID) * float4(_BlitScaleBiasRt.x, _BlitScaleBiasRt.y, 1, 1) + float4(_BlitScaleBiasRt.z, _BlitScaleBiasRt.w, 0, 0);
            output.positionCS.xy = output.positionCS.xy * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f); //convert to -1..1
            output.texcoord = GetQuadTexCoord(input.vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
            return output;
        }

        Varyings VertQuadPadding(Attributes input)
        {
            Varyings output;
            float2 scalePadding = ((_BlitTextureSize + float(_BlitPaddingSize)) / _BlitTextureSize);
            float2 offsetPaddding = (float(_BlitPaddingSize) / 2.0) / (_BlitTextureSize + _BlitPaddingSize);

            output.positionCS = GetQuadVertexPosition(input.vertexID) * float4(_BlitScaleBiasRt.x, _BlitScaleBiasRt.y, 1, 1) + float4(_BlitScaleBiasRt.z, _BlitScaleBiasRt.w, 0, 0);
            output.positionCS.xy = output.positionCS.xy * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f); //convert to -1..1
            output.texcoord = GetQuadTexCoord(input.vertexID);
            output.texcoord = (output.texcoord - offsetPaddding) * scalePadding;
            output.texcoord = output.texcoord * _BlitScaleBias.xy + _BlitScaleBias.zw;
            return output;
        }

        float4 Frag(Varyings input, SamplerState s)
        {
        #if defined(USE_TEXTURE2D_X_AS_ARRAY) && defined(BLIT_SINGLE_SLICE)
            return SAMPLE_TEXTURE2D_ARRAY_LOD(_BlitTexture, s, input.texcoord.xy, _BlitTexArraySlice, _BlitMipLevel);
        #endif

            return SAMPLE_TEXTURE2D_LOD(_BlitTexture, s, input.texcoord.xy, _BlitMipLevel);
        }

        float4 FragNearest(Varyings input) : SV_Target
        {
            return Frag(input, sampler_PointClamp);
        }

        float4 FragBilinear(Varyings input) : SV_Target
        {
            return Frag(input, sampler_LinearClamp);
        }
        
        float4 FragBilinearRepeat(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord.xy;
            return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
        }

        float4 FragNearestRepeat(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord.xy;
            return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointRepeat, uv, _BlitMipLevel);
        }

    ENDHLSL

    SubShader
    {
        // 3: Bilinear quad
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex VertQuad
                #pragma fragment FragBilinear
            ENDHLSL
        }
    }

    Fallback Off
}
