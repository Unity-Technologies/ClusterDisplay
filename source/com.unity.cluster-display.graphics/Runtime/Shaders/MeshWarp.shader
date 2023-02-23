Shader "Hidden/ClusterDisplay/MeshWarp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BackgroundTex ("Background Texture", CUBE) = "white" {}
        _BackgroundColor ("Background Color", COLOR) = (1, 1, 1, 1)
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
                float4 vertex : SV_POSITION;
                float3 uv : TEXCOORD0;
                float3 worldPosition: TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _UVRotation;
            float2 _UVShift;

            float4x4 _CameraTransform;
            float4x4 _CameraProjection;

            UNITY_DECLARE_TEXCUBE(_BackgroundTex);
            float3 _OuterFrustumCenter;
            float4 _BackgroundColor;

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

            // returns 1 if value is >= 0, returns 0 otherwise
            float IsPositive(float value)
            {
                return step(0.0f, value);
            }

            v2f vert(appdata v)
            {
                const float2x2 uvRotation = float2x2(_UVRotation.x, _UVRotation.y, _UVRotation.z, _UVRotation.w);
                v2f o;
                // Flatten the mesh into a "fullscreen quad" by
                // using its UVs as screen coordinates.
                float2 fullScreenClip = (mul(uvRotation, v.uv.xy) + _UVShift) * 2 - float2(1, 1);

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
                o.worldPosition = worldPos.xyz;
                float4 posInCameraSpace = mul(_CameraTransform, worldPos);
                float depth = -posInCameraSpace.z;
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
                const float2 uv = i.uv.xy / i.uv.z;

                // Compute blend of inner / outer frustum
                const float4 innerFrustumSample = tex2D(_MainTex, uv);
                const float3 cubeMapSampleCoord = i.worldPosition - _OuterFrustumCenter;
                const float4 outerFrustumSample = UNITY_SAMPLE_TEXCUBE(_BackgroundTex, cubeMapSampleCoord) * _BackgroundColor;

                // Blend them together
                const float innerFrustumAlpha = InsideRect(uv.xy, float2(0, 0), float2(1, 1)) * IsPositive(i.uv.z);
                return innerFrustumSample * innerFrustumAlpha + outerFrustumSample * (1 - innerFrustumAlpha);
            }
            ENDCG
        }
    }
}
