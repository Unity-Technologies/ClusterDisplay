Shader "Hidden/ExampleScreenCoordOverride"
{
    SubShader
    {
        //PackageRequirements 
        //{
        //    "com.unity.render-pipelines.universal" : "14.0.0"
        //}
        
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "DefaultPass"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            //#pragma multi_compile _ SCREEN_COORD_OVERRIDE

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            v2f Vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 Frag(v2f i) : SV_Target
            {
                fixed4 col = float4(0, 0.5, 0, 0);//tex2D(_InputTexture, i.texcoord);
                return col;
            }
            
            ENDCG
        }
    }
}
