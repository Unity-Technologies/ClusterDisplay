Shader "Unlit/DebugGlobalScreenSpace"
{
    Properties
    {
    }
    SubShader
    {
        Pass
        {
			HLSLPROGRAM

			#pragma target 5.0
			#pragma vertex vert
			#pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            #pragma multi_compile __ USING_GLOBAL_SCREEN_SPACE

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformWorldToHClip(v.vertex.xyz);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return float4(i.vertex.xy, 1, 1);
            }
            ENDHLSL
        }
    }
}
