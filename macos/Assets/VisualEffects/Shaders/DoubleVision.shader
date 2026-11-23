Shader "Hidden/DoubleVision"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Displacement("Displacement Amount", Float) = 0.05
    }
    
    SubShader
    {
        Tags { "Queue" = "Transparent" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Displacement;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
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
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Displace to the left and right
                float2 uvLeft = uv + float2(-_Displacement, 0);
                float2 uvRight = uv + float2(_Displacement, 0);

                // Sample the texture at the displaced coordinates
                fixed4 colLeft = tex2D(_MainTex, uvLeft);
                fixed4 colRight = tex2D(_MainTex, uvRight);

                // Combine the two displaced images
                fixed4 combinedColor = 0.5 * (colLeft + colRight);

                return combinedColor;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
