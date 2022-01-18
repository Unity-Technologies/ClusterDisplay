Shader "Hidden/Custom/Vignette"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        PackageRequirements 
        {
            "com.unity.render-pipelines.universal" : "14.0.0"
        }

        ZTest Always 
        ZWrite Off 
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ScreenCoordOverride.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            #pragma multi_compile _ SCREEN_COORD_OVERRIDE

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_VignetteTex);
            SAMPLER(sampler_VignetteTex);
            float4 _VignetteColor;
            float _Intensity;

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.vertex = vertexInput.positionCS;
                output.uv = input.uv;
                
                return output;
            }

            half4 Frag (Varyings input) : SV_Target 
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                if (_Intensity < 1e-8)
                {
                    return baseColor;
                }

                // Non optimal, _OverlayTex could be single channel.
                float overlayFactor = SAMPLE_TEXTURE2D(_VignetteTex, sampler_VignetteTex, SCREEN_COORD_APPLY_SCALEBIAS(input.uv)).a;
                return lerp(baseColor, _VignetteColor, overlayFactor  * _Intensity);
            }
            
            ENDHLSL
        }
    }
}
