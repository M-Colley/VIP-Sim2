Shader "Hidden/FovealDarkness"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _MouseX("Mouse X Position (Normalized 0 to 1)", Float) = 0.5
        _MouseY("Mouse Y Position (Normalized 0 to 1)", Float) = 0.5
        _InnerCircleRadius("Inner Circle Radius", Float) = 0.25
        _FadeWidth("Fade Width", Float) = 0.05
        _Opacity("Opacity of Overlay", Float) = 0.5
        _ScreenHeight("Screen Height in px", int) = 1440
        _ScreenWidth("Screen Width in px", int) = 2560
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _MouseX;
            float _MouseY;
            float _InnerCircleRadius;
            float _FadeWidth;
            float _Opacity;
            int _ScreenHeight;
            int _ScreenWidth;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 mousePos = float2(_MouseX, _MouseY);

                // Calculate the aspect ratio. As floats: both uniforms are ints, and int/int
                // truncated to 1 on 16:9 and 16:10 screens (an ellipse, not a circle) and to 2
                // on 21:9.
                float aspectRatio = (float)_ScreenWidth / max((float)_ScreenHeight, 1.0);

                // Adjust the UV coordinates to maintain a circular vortex
                float2 adjustedUV = uv;
                adjustedUV.x *= aspectRatio;
                float2 adjustedMousePos = mousePos;
                adjustedMousePos.x *= aspectRatio;

                // Calculate distance from mouse position
                float2 delta = adjustedUV - adjustedMousePos;
                float dist = length(delta);

                // The pixel itself. adjustedUV is for the distance only: sampled there, the
                // image was stretched by the aspect ratio -- invisible while the ratio wrongly
                // came out as 1, and the whole frame on a 21:9 screen where it came out as 2.
                fixed4 color = tex2D(_MainTex, uv);
   
                    
                // Full strength inside _InnerCircleRadius, fading out over the next _FadeWidth.
                // The fade used to run inwards from radius - width; with the defaults (0.01 and
                // 0.05) it started below zero, so even the centre was darkened by only 5%.
                // _Opacity is the strength of the dark spot: 0 leaves the image, 1 is black. It
                // was the reverse, against its label and the profiles' severity.
                float fade = smoothstep(_InnerCircleRadius, _InnerCircleRadius + max(_FadeWidth, 1e-5), dist);
                fixed4 dark = lerp(color, fixed4(0, 0, 0, 1.0), _Opacity);
                color = lerp(dark, color, fade);

                return color;
            }

            ENDCG
        }
    }
}
