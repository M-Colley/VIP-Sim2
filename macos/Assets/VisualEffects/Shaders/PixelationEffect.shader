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
                float2 uv = i.uv * _PixelSize;
                float2 pixelCoord = floor(uv);
                float2 fraction = uv - pixelCoord;

                // Get the colors of the surrounding pixels
                half4 col00 = tex2D(_MainTex, (pixelCoord + float2(0, 0)) / _PixelSize);
                half4 col10 = tex2D(_MainTex, (pixelCoord + float2(1, 0)) / _PixelSize);
                half4 col01 = tex2D(_MainTex, (pixelCoord + float2(0, 1)) / _PixelSize);
                half4 col11 = tex2D(_MainTex, (pixelCoord + float2(1, 1)) / _PixelSize);

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
