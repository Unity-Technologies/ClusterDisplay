#pragma target 4.5
#pragma editor_sync_compilation
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

Texture2D _BlitTexture;
SamplerState sampler_LinearClamp;
uniform int _DisplayRed;

struct Attributes
{
    uint vertexID : SV_VertexID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 texcoord   : TEXCOORD0;
};

Varyings Vertex(Attributes input)
{
    Varyings output;
    output.positionCS = GetQuadVertexPosition(input.vertexID);
    output.positionCS.xy = output.positionCS.xy; //convert to -1..1
#if defined(FLIP_GEOMETRY_VERTICAL)
    output.positionCS.y *= -1;
#endif
    output.texcoord = GetQuadTexCoord(input.vertexID);
    return output;
}

float4 Fragment(Varyings input) : SV_Target
{
    if (_DisplayRed == 1)
        return float4(1, 0, 0, 1);
    return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord.xy);
}
