Shader "Hidden/FlickeringStars"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Radius ("Radius", float) = 5.0
        _StarRadius ("Star Radius", float) = 1.0
        _Fade ("Fade", float) = 1.0
        _Coordinate0 ("Coordinate 0", Vector) = (0,0,0,0)
        _Coordinate1 ("Coordinate 1", Vector) = (0,0,0,0)
        _Coordinate2 ("Coordinate 2", Vector) = (0,0,0,0)
        _Coordinate3 ("Coordinate 3", Vector) = (0,0,0,0)
        _Coordinate4 ("Coordinate 4", Vector) = (0,0,0,0)
        _Coordinate5 ("Coordinate 5", Vector) = (0,0,0,0)
        _Coordinate6 ("Coordinate 6", Vector) = (0,0,0,0)
        _Coordinate7 ("Coordinate 7", Vector) = (0,0,0,0)
        _Coordinate8 ("Coordinate 8", Vector) = (0,0,0,0)
        _Coordinate9 ("Coordinate 9", Vector) = (0,0,0,0)
        _Coordinate10 ("Coordinate 10", Vector) = (0,0,0,0)
        _Coordinate11 ("Coordinate 11", Vector) = (0,0,0,0)
        _Coordinate12 ("Coordinate 12", Vector) = (0,0,0,0)
        _Coordinate13 ("Coordinate 13", Vector) = (0,0,0,0)
        _Coordinate14 ("Coordinate 14", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _Radius;
            float _StarRadius;
            float _Fade;
            float4 _Coordinate0;
            float4 _Coordinate1;
            float4 _Coordinate2;
            float4 _Coordinate3;
            float4 _Coordinate4;
            float4 _Coordinate5;
            float4 _Coordinate6;
            float4 _Coordinate7;
            float4 _Coordinate8;
            float4 _Coordinate9;
            float4 _Coordinate10;
            float4 _Coordinate11;
            float4 _Coordinate12;
            float4 _Coordinate13;
            float4 _Coordinate14;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {

                float2 uv = i.uv;
                float4 color = tex2D(_MainTex, uv);

                // Check distances to all coordinates
                float2 coords[15] = {
                    _Coordinate0.xy, _Coordinate1.xy, _Coordinate2.xy, _Coordinate3.xy,
                    _Coordinate4.xy, _Coordinate5.xy, _Coordinate6.xy, _Coordinate7.xy,
                    _Coordinate8.xy, _Coordinate9.xy, _Coordinate10.xy, _Coordinate11.xy,
                    _Coordinate12.xy, _Coordinate13.xy, _Coordinate14.xy
                };
                float starRadii[15] = {
                    _Coordinate0.z, _Coordinate1.z, _Coordinate2.z, _Coordinate3.z,
                    _Coordinate4.z, _Coordinate5.z, _Coordinate6.z, _Coordinate7.z,
                    _Coordinate8.z, _Coordinate9.z, _Coordinate10.z, _Coordinate11.z,
                    _Coordinate12.z, _Coordinate13.z, _Coordinate14.z
                };
                float fades[15] = {
                    _Coordinate0.w, _Coordinate1.w, _Coordinate2.w, _Coordinate3.w,
                    _Coordinate4.w, _Coordinate5.w, _Coordinate6.w, _Coordinate7.w,
                    _Coordinate8.w, _Coordinate9.w, _Coordinate10.w, _Coordinate11.w,
                    _Coordinate12.w, _Coordinate13.w, _Coordinate14.w
                };

                for (int j = 0; j < 15; j++)
                {
                    float distance = length(uv - coords[j]);
                    if (distance < starRadii[j])
                    {
                        float4 blurredColor = float4(0, 0, 0, 0);
                        // 5x5 taps: dividing by 16 brightened every star by 1.56x and clipped.
                        float blurSamples = 25;
                        float blurRadius = 0.005;
                        for (int x = -2; x <= 2; x++)
                        {
                            for (int y = -2; y <= 2; y++)
                            {
                                blurredColor += tex2D(_MainTex, uv + float2(x, y) * blurRadius);
                            }
                        }
                        blurredColor /= blurSamples;
                        color = lerp(color, blurredColor, fades[j]);
                    }
                }

                return color;
            }
            ENDCG
        }
    }
}
