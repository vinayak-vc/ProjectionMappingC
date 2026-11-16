// PMSDK projector-output shader.
//
// Fragment pipeline (each stage is a no-op at its default values):
//   1. UV transform   — rotate 0/90/180/270 + mirror X/Y   (PMSDKOutputTransform, #8)
//   2. sample content
//   3. edge blend      — multiply by the vertex-color falloff ramp the native blend
//                        stage bakes into COLOR (PMSDKMeshWarp/pmsdk_blendconfig_apply_to_mesh)
//   3a. shape mask     — for Ellipse skins, cut a feathered circular hole in the
//                        quad's UV space (PMSDKSkin, M-Sk1). Discards outside the
//                        mask so overlapping skins can safely coexist.
//   3b. opacity        — per-skin opacity multiplier (PMSDKSkin, M-Sk1).
//   4. luminance gain  — multiply by a camera-measured per-projector vignette gain map
//                        sampled in raster space (UV1)              (PMSDKLuminanceGain)
//   5. black level     — uniform floor lift + per-region uplift map that raises
//                        single-projector black to the doubled overlap black
//                        (PMSDKEdgeBlend #6 / PMSDKBlackLevelLift)
//   6. color correct   — per-channel gain/offset + output gamma   (PMSDKColorCorrection, #6/#7)
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
        // Luminance gain (camera-measured vignette compensation, raster-space)
        _GainTex ("Gain Map (R)", 2D) = "white" {}
        _UseGainTex ("Use Gain Map", Float) = 0
        // Black level (#6)
        _BlackLevel ("Black Level", Range(0,1)) = 0
        // Per-region black-level uplift (camera-measured, raster-space)
        _BlackLiftTex ("Black Lift Map (R)", 2D) = "black" {}
        _UseBlackLift ("Use Black Lift Map", Float) = 0
        // Color correction (#6 per-channel gamma, #7)
        _ColorGain ("Color Gain", Color) = (1,1,1,1)
        _ColorOffset ("Color Offset", Color) = (0,0,0,0)
        _OutGamma ("Output Gamma (per channel)", Vector) = (1,1,1,0)
        // Shape mask + opacity (PMSDKSkin, M-Sk1). Defaults are no-op.
        _ShapeMask ("Shape Mask (0=None, 1=Ellipse)", Float) = 0
        _MaskFeather ("Mask Feather (UV units) — scalar fallback", Range(0, 0.5)) = 0
        // Per-edge feather (Top, Right, Bottom, Left). M-Sk4 v1.2 polish: PMSDKSkin
        // pushes (SoftMask, SoftMask, SoftMask, SoftMask) by default; if
        // PerEdgeFeatherEnabled, pushes the user-authored EdgeFeather values instead.
        _MaskFeather4 ("Mask Feather per-edge (T,R,B,L)", Vector) = (0,0,0,0)
        _Opacity ("Skin Opacity", Range(0, 1)) = 1
        // Photo-derived alpha mask (quad skins): the skin's UV quad is cut to the mask's
        // silhouette, holes included. Cropped to the part's bounding box, so the mask UV
        // is the quad UV. Default is a no-op.
        _AlphaMask ("Alpha Mask (R)", 2D) = "white" {}
        _UseAlphaMask ("Use Alpha Mask", Float) = 0
        // Auto Adjust warp map (D-046): raster position -> content UV for curved surfaces.
        // RG = uv * valid, A = valid (premultiplied so bilinear filtering never mixes in
        // invalid texels); sampled at the raster position (UV1) inside _WarpMapRect.
        _WarpMap ("Warp Map (RG=uv*a, A=valid)", 2D) = "black" {}
        _UseWarpMap ("Use Warp Map", Float) = 0
        _WarpMapRect ("Warp Map Raster Rect (x, y, w, h)", Vector) = (0,0,1,1)
        // Rectangle seam feather (D-045): fade each quad edge over _MaskFeather4 (T,R,B,L).
        _RectFeather ("Rectangle Edge Feather", Float) = 0
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
            sampler2D _GainTex;
            float _UseGainTex;
            sampler2D _BlackLiftTex;
            float _UseBlackLift;
            float _UVRotation;
            float _MirrorX;
            float _MirrorY;
            float _BlackLevel;
            fixed4 _ColorGain;
            fixed4 _ColorOffset;
            float3 _OutGamma;
            float _ShapeMask;
            float _MaskFeather;
            float4 _MaskFeather4;
            float _Opacity;
            sampler2D _AlphaMask;
            float _UseAlphaMask;
            sampler2D _WarpMap;
            float _UseWarpMap;
            float4 _WarpMapRect;
            float _RectFeather;

            // Smooth 0..1 ramp over [0, f]; a zero feather is a hard edge.
            float edgeRamp(float distance, float f)
            {
                return f > 0.0001 ? smoothstep(0.0, f, distance) : 1.0;
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 rasterUV : TEXCOORD1; // warped raster position (PMSDKMeshWarp UV1)
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 rasterUV : TEXCOORD1;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.rasterUV = v.rasterUV;
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
                // Warp map (Auto Adjust, curved surfaces): content UV comes from the measured
                // raster -> UV map instead of the mesh UV. Masks below keep using the mesh UV.
                float2 contentUV = i.uv;
                if (_UseWarpMap > 0.5)
                {
                    float2 m = (i.rasterUV - _WarpMapRect.xy) / max(_WarpMapRect.zw, 1e-6);
                    if (m.x < 0.0 || m.y < 0.0 || m.x > 1.0 || m.y > 1.0) discard;
                    float4 w = tex2D(_WarpMap, m);
                    if (w.a < 0.5) discard;
                    contentUV = w.rg / w.a;
                    // Raster area the straightened content does not reach stays dark.
                    if (contentUV.x < -0.001 || contentUV.y < -0.001 || contentUV.x > 1.001 || contentUV.y > 1.001) discard;
                }
                float2 uv = transformUV(contentUV);
                fixed4 c = tex2D(_MainTex, uv);

                // Edge blend: fold the vertex-color ramp (alpha) into RGB — a
                // projector blends to black at its overlap edges.
                c.rgb *= i.color.rgb * i.color.a;

                // Shape mask (PMSDKSkin): discard pixels outside an ellipse inscribed
                // in the [0,1]^2 UV quad, with a feathered edge. Non-quad skins
                // (Polygon/Freeform) already have their shape as raw geometry and set
                // _ShapeMask=0. Discard so overlapping skins render correctly.
                //
                // Per-edge feather (M-Sk4 v1.2 polish): _MaskFeather4 = (Top, Right,
                // Bottom, Left). Pixels closer to a given side use that side's feather;
                // in between, weighted by |nx| / (|nx|+|ny|). Falls back to scalar
                // _MaskFeather when all four channels are zero (PMSDKSkin's default
                // push for skins without PerEdgeFeatherEnabled sends the scalar tiled
                // across all four, so this branch is rare).
                //
                // Uses i.uv (raw mesh UV), NOT the transformed content-sampling UV — the
                // mask defines the PHYSICAL projected region, so a content rotation via
                // PMSDKOutputTransform must not rotate the mask along with it.
                if (_ShapeMask > 0.5)
                {
                    float2 d = (i.uv - 0.5) * 2.0;
                    float r = length(d);

                    float4 f4 = _MaskFeather4;
                    if ((f4.x + f4.y + f4.z + f4.w) < 0.0001) f4 = _MaskFeather.xxxx;
                    float fY = (d.y >= 0.0) ? f4.x : f4.z; // top / bottom
                    float fX = (d.x >= 0.0) ? f4.y : f4.w; // right / left
                    float2 an = abs(d);
                    float denom = max(an.x + an.y, 0.0001);
                    float feather = max((fX * an.x + fY * an.y) / denom, 0.0001);

                    float mask = smoothstep(1.0, 1.0 - feather, r);
                    if (mask <= 0.001) discard;
                    c.rgb *= mask;
                }

                // Alpha mask (photo skins): bilinear-filtered silhouette, so edges are
                // anti-aliased and holes are cut out. Uses the raw mesh UV for the same
                // reason as the shape mask above.
                if (_UseAlphaMask > 0.5)
                {
                    float am = tex2D(_AlphaMask, i.uv).r;
                    if (am <= 0.004) discard;
                    c.rgb *= am;
                }

                // Rectangle seam feather: fade toward each quad edge so neighbouring facets
                // overlap softly instead of showing a hard seam. _MaskFeather4 = (T,R,B,L).
                if (_RectFeather > 0.5)
                {
                    float fm = edgeRamp(1.0 - i.uv.y, _MaskFeather4.x) * edgeRamp(1.0 - i.uv.x, _MaskFeather4.y)
                             * edgeRamp(i.uv.y, _MaskFeather4.z) * edgeRamp(i.uv.x, _MaskFeather4.w);
                    c.rgb *= fm;
                }

                // Per-skin opacity (multiplied on RGB — projector outputs light, so
                // reducing opacity fades toward black, which is "no projected light").
                c.rgb *= _Opacity;

                // Luminance gain: dim bright (centre) regions toward the shared target
                // measured by the camera, so vignetting leaves no brighter/darker band.
                // Sampled in raster space (UV1) so it tracks the physical projector.
                if (_UseGainTex > 0.5)
                {
                    c.rgb *= tex2D(_GainTex, i.rasterUV).r;
                }

                // Black level: lift the floor uniformly so single-projector regions
                // can be raised toward the doubled black of overlap regions.
                c.rgb = c.rgb * (1.0 - _BlackLevel) + _BlackLevel;

                // Per-region black-level uplift: a camera-measured map raises each
                // projector's non-overlap black up to the overlap's doubled black, so a
                // blended wall reads as one uniform floor instead of a bright centre band.
                // Sampled in raster space (UV1), same as the gain map.
                if (_UseBlackLift > 0.5)
                {
                    float bl = tex2D(_BlackLiftTex, i.rasterUV).r;
                    c.rgb = c.rgb * (1.0 - bl) + bl;
                }

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
