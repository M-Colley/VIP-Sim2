// Monocular double vision.
//
// The name is a leftover and deliberately kept: myDoubleVision.cs asks for this
// shader by the string "Hidden/VisSim/BasicShader", and the scene stores it by asset
// GUID on both eye instances, so renaming it buys nothing and breaks both references.
//
// What was here before was a Lambert SURFACE shader -- lit 3D geometry maths, tagged
// RenderType Opaque, falling back to Diffuse -- being used as a full-screen image
// effect. Two consequences. It forced alpha to 1 on every pixel, and since VIP-Sim is
// composited onto the desktop from framebuffer alpha, switching on monocular double
// vision turned the overlay into a solid rectangle. And it declared neither of the
// properties myDoubleVision.cs actually sets, _Displace and _Amount, so the
// displacement it was being handed every frame went nowhere: the effect was a no-op
// that merely made things opaque.
//
// This is now a plain unlit image effect implementing what the script has always
// asked for -- the image blended with a displaced copy of itself -- with alpha taken
// from the undisplaced sample so the overlay keeps its own footprint.
Shader "Hidden/VisSim/BasicShader"
{
    Properties
    {
        _MainTex("Base (RGB)", 2D) = "white" {}
        _Displace("Displacement (normalised xy)", Vector) = (0, 0, 0, 0)
        _Amount("Ghost blend amount", Float) = 0.5
    }

    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            Fog { Mode off }

            CGPROGRAM

                #pragma vertex vert_img
                #pragma fragment frag
                #pragma fragmentoption ARB_precision_hint_fastest
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                float4 _Displace;
                float _Amount;

                half4 frag(v2f_img i) : SV_Target
                {
                    half4 base = tex2D(_MainTex, i.uv);
                    half4 ghost = tex2D(_MainTex, i.uv + _Displace.xy);

                    // An even blend at _Amount = 0.5 gives the two equally-weighted
                    // superimposed images that diplopia actually looks like, and matches
                    // what the older DoubleVision.shader did with 0.5 * (left + right).
                    half3 rgb = lerp(base.rgb, ghost.rgb, saturate(_Amount));

                    // Alpha from the undisplaced sample: the ghost is a copy of the same
                    // content, so letting it widen the alpha would grow the overlay past
                    // the region it is supposed to cover.
                    return half4(rgb, base.a);
                }

            ENDCG
        }
    }

    FallBack off
}
