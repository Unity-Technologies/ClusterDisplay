#ifndef GLOBAL_SCREEN_SPACE_INCLUDED
#define GLOBAL_SCREEN_SPACE_INCLUDED

//#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/GlobalScreenSpace.hlsl"

void GlobalScreenSpace_float(float2 in_, out float2 out_)
{
    out_ = in_;
}

#endif // GLOBAL_SCREEN_SPACE_INCLUDED