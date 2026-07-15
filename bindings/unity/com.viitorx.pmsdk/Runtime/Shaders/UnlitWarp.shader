// Unlit texture shader modulated by vertex color.
// The PMSDK edge-blend stage writes its falloff ramps into mesh vertex colors
// (see PMSDKMeshWarp.Update / pmsdk_blendconfig_apply_to_mesh); stock
// Unlit/Texture ignores vertex colors, so blending is invisible without this.
// Cull Off: the warped mesh lives in normalized projector-raster space and may
// be viewed from either side depending on rig orientation.
Shader "PMSDK/UnlitWarp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                // The native edge-blend stage writes its falloff ramp into the
                // vertex color ALPHA channel (pmsdk_blendconfig_apply_to_mesh sets
                // color.w only). Fold it into RGB: a projector "blends to black".
                // Output alpha stays 1 so captures/compositing treat us as opaque.
                c.rgb *= i.color.rgb * i.color.a;
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
}
