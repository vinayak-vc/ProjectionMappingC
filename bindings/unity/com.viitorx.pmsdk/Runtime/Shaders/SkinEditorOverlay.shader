// Trivial per-vertex-color line/quad shader for the Skin Editor's GL immediate-mode
// overlay. Draws handles + outlines OVER the skin content (ZTest Always), with alpha
// blending so semi-transparent unselected handles work.
Shader "PMSDK/SkinEditorOverlay"
{
    Properties { _MainTex ("Unused", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZTest Always
        ZWrite Off
        Cull Off
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f    { float4 pos : SV_POSITION; float4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
