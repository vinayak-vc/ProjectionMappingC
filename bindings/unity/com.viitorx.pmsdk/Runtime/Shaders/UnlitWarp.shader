// PMSDK projector-output shader.
//
// Fragment pipeline (each stage is a no-op at its default values):
//   1. UV transform   — rotate 0/90/180/270 + mirror X/Y   (PMSDKOutputTransform, #8)
//   2. sample content
//   3. edge blend      — multiply by the vertex-color falloff ramp the native blend
//                        stage bakes into COLOR (PMSDKMeshWarp/pmsdk_blendconfig_apply_to_mesh)
//   4. black level     — uniform black-floor lift                 (PMSDKEdgeBlend, #6)
//   5. color correct   — per-channel gain/offset + output gamma   (PMSDKColorCorrection, #6/#7)
//
// Cull Off: the warped mesh lives in normalized projector-raster space and may be
// viewed from either side depending on rig orientation.
Shader "PMSDK/UnlitWarp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        // Output transform (#8)
        _UVRotation ("UV Rotation (0..3 = 0/90/180/270)", Float) = 0
        _MirrorX ("Mirror X", Float) = 0
        _MirrorY ("Mirror Y", Float) = 0
        // Black level (#6)
        _BlackLevel ("Black Level", Range(0,1)) = 0
        // Color correction (#6 per-channel gamma, #7)
        _ColorGain ("Color Gain", Color) = (1,1,1,1)
        _ColorOffset ("Color Offset", Color) = (0,0,0,0)
        _OutGamma ("Output Gamma (per channel)", Vector) = (1,1,1,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite On
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _UVRotation;
            float _MirrorX;
            float _MirrorY;
            float _BlackLevel;
            fixed4 _ColorGain;
            fixed4 _ColorOffset;
            float3 _OutGamma;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float2 transformUV(float2 uv)
            {
                // Mirror about centre.
                if (_MirrorX > 0.5) uv.x = 1.0 - uv.x;
                if (_MirrorY > 0.5) uv.y = 1.0 - uv.y;
                // Rotate about centre in 90-degree steps.
                int r = ((int)round(_UVRotation)) & 3;
                float2 c = uv - 0.5;
                if (r == 1) c = float2(-c.y, c.x);       // 90
                else if (r == 2) c = float2(-c.x, -c.y); // 180
                else if (r == 3) c = float2(c.y, -c.x);  // 270
                return c + 0.5;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = transformUV(i.uv);
                fixed4 c = tex2D(_MainTex, uv);

                // Edge blend: fold the vertex-color ramp (alpha) into RGB — a
                // projector blends to black at its overlap edges.
                c.rgb *= i.color.rgb * i.color.a;

                // Black level: lift the floor uniformly so single-projector regions
                // can be raised toward the doubled black of overlap regions.
                c.rgb = c.rgb * (1.0 - _BlackLevel) + _BlackLevel;

                // Per-projector color match: gain/offset then per-channel output gamma.
                c.rgb = saturate(c.rgb * _ColorGain.rgb + _ColorOffset.rgb);
                float3 g = max(_OutGamma, 0.0001);
                c.rgb = pow(c.rgb, 1.0 / g);

                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
}
