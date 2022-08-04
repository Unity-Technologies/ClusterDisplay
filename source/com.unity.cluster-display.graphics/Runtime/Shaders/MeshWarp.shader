Shader "Hidden/ClusterDisplay/MeshWarp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Off

        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float3 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4x4 _CameraTransform;
            float4x4 _CameraProjection;

            // returns 1 if v inside the box, returns 0 otherwise
            float InsideRect(float2 v, float2 bottomLeft, float2 topRight)
            {
                // BottomLeft represents the smallest {x, y} while TopRight the highest, bottomLeft < topRight.
                // step(bottomLeft, v) returns {1,1} if v >= bottomLeft, that is, the point is within the rect {bottomLeft, +infinity},
                // step(topRight, v) returns {0,0} if v < topRight, that is, the point is within the rect {-infinity, topRight},
                // so if the point is within {bottomLeft, topRight}, the intersection of {bottomLeft, +infinity} and {-infinity, topRight},
                // step(bottomLeft, v) returns {1, 1} and step(topRight, v), {0, 0}, so s = {1, 1}.
                // In that case s.x * s.y = 1 * 1 = 1.
                float2 s = step(bottomLeft, v) - step(topRight, v);
                return s.x * s.y;
            }

            v2f vert(appdata v)
            {
                v2f o;
                // Flatten the mesh into a "fullscreen quad" by
                // using its UVs as screen coordinates.
                float2 fullScreenClip = v.uv.xy * 2 - float2(1, 1);

                // (0, 0) is top-left for device coordinates but
                // (0, 0) is bottom-left for UVs
                fullScreenClip.y = -fullScreenClip.y;

                // Set Z and W to 1 so perspective division doesn't do anything.
                o.vertex = float4(fullScreenClip, 1, 1);

                // Debug: original vertices and UVs
                // o.vertex = UnityObjectToClipPos(v.vertex);
                // o.uv = float3(v.uv, 1);

                // Calculate the UV mapping by projecting the vertices into normalized screen space
                // using the specified camera matrices.
                const float4 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
                float4 posInCameraSpace = mul(_CameraTransform, worldPos);
                float depth = posInCameraSpace.z;
                float4 clipCoord = mul(_CameraProjection, posInCameraSpace);
                float4 normalized = clipCoord / clipCoord.w;
                normalized.xy = (normalized.xy + float2(1, 1)) * 0.5;

                // Use a depth-correct interpolation so that we don't get weird triangular artifacts
                // https://forum.unity.com/threads/help-giving-a-productionaly-generated-symmetric-mesh-stretched-the-mesh-in-a-weird-way.545413/#post-3605017
                const float3 uv_warped = float3(normalized.xy * depth, depth);

                // Let the rasterizer interpolate the UVs + depth
                o.uv = uv_warped;
                o.uv.xy = TRANSFORM_TEX(o.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Remove the depth multiplier
                float2 uv = i.uv.xy / i.uv.z;

                // sample the texture
                return tex2D(_MainTex, uv) * InsideRect(uv.xy, float2(0, 0), float2(1, 1));
            }
            ENDCG
        }
    }
}
