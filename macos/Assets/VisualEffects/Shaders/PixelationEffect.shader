Shader "Hidden/PixelationEffect"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _PixelSize ("Pixel Size", Range(1, 100)) = 10
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
            float _PixelSize;

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

            fixed4 frag(v2f i) : SV_Target
            { 
                /* Hard pixalation without blendig edges
                float2 uv = i.uv;
                uv = floor(uv * _PixelSize) / _PixelSize;
                return tex2D(_MainTex, uv);
                */
                // Blend the four nearest cell centres. The samples used to sit on cell corners,
                // which include the frame's edges: with one cell (the slider's maximum) they were
                // the four screen corners, usually transparent desktop, so the whole overlay
                // went transparent and the effect vanished. Clamped to the outermost centres,
                // no sample falls outside the frame.
                float2 uv = i.uv * _PixelSize - 0.5;
                float2 pixelCoord = floor(uv);
                float2 fraction = uv - pixelCoord;
                float2 lo = 0.5 / _PixelSize;
                float2 hi = 1.0 - lo;

                // Get the colors of the surrounding pixels
                half4 col00 = tex2D(_MainTex, clamp((pixelCoord + float2(0.5, 0.5)) / _PixelSize, lo, hi));
                half4 col10 = tex2D(_MainTex, clamp((pixelCoord + float2(1.5, 0.5)) / _PixelSize, lo, hi));
                half4 col01 = tex2D(_MainTex, clamp((pixelCoord + float2(0.5, 1.5)) / _PixelSize, lo, hi));
                half4 col11 = tex2D(_MainTex, clamp((pixelCoord + float2(1.5, 1.5)) / _PixelSize, lo, hi));

                // Interpolate between the colors
                half4 col0 = lerp(col00, col10, fraction.x);
                half4 col1 = lerp(col01, col11, fraction.x);
                half4 color = lerp(col0, col1, fraction.y);

                return color;
            }

            ENDCG
        }
    }
}
