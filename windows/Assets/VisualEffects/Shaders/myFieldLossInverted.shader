Shader "Hidden/VisSim/myFieldLossInverted"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Overlay("Overlay Texture", 2D) = "white" {}
        _LODlevel("LOD level", Float) = 0.0
        _MaxLODlevel("Max LOD level", Float) = 0.0
        _MouseX("Mouse X Position (Normalized 0 to 1)", Float) = 0.0
        _MouseY("Mouse Y Position (Normalized 0 to 1)", Float) = 0.0
        _ViewDist_m("Viewing distance in meters", Float) = 2.4815
        _OverlayScale("Overlay Scale", Float) = 1.0
    }
    
    SubShader
    {
        AlphaTest Off
        Cull Back
        Lighting Off
        Tags { "Queue"="Overlay+1" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _MainTex_ST;
            sampler2D _Overlay;
            half4 _Overlay_ST;
            float _LODlevel;
            float _MaxLODlevel;
            half _MouseX, _MouseY;
            float _ViewDist_m;
            float _OverlayScale;

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv[2] : TEXCOORD0;
                float4 vertexPos : SV_POSITION;
            };


            v2f vert(appdata_t v)
            {
                v2f o;

                // basic info
                o.vertexPos = UnityObjectToClipPos(v.vertex);
                o.uv[0] = v.texcoord.xy;
                o.uv[1] = v.texcoord.xy;

                return o;
            };

            fixed4 frag(v2f i) : SV_Target
            {
                // correct mouse y-coordinates if on a platform where '0' is the top (e.g., Direct3D-like)
                #if UNITY_UV_STARTS_AT_TOP
                    _MouseY = 1.0 - _MouseY;
                #endif

                // values to convert rgb to grayscale from:
                // Foley et al, "Computer Graphics: Principles and Practice"
                const float4 rgb2grey = float4(0.299, 0.587, 0.114, 1.0);

                // Calculate mouse offset
                float2 mousePos = half2(_MouseX, _MouseY);
                float2 centerPos = half2(0.5, 0.5);

                // Calculate scaled UV coordinates centered around the mouse position
                float2 normalizedMousePos = centerPos + (mousePos - centerPos) / _OverlayScale;
                float2 scaledUV = (i.uv[0] - centerPos) / _OverlayScale + normalizedMousePos;

                // Clamp UV coordinates to prevent repetition
                scaledUV = clamp(scaledUV, 0.0, 1.0);

                // Fetch sample from degradation map
                float4 degradation = tex2D(_Overlay, scaledUV);
                float w = saturate(degradation.x * rgb2grey.x + degradation.y * rgb2grey.y + degradation.z * rgb2grey.z);

                // Copy over view texture uv coordinates and
                // map the bias term from [0-1] to [0-min_lod] where min_lod is
                // the coarsest mipmap level
                float lodBias = saturate(w) * _MaxLODlevel - 1;
                lodBias = max(0.0, lodBias); // Clamp to prevent negative LOD
                float4 bias_uv = float4(i.uv[0], 0.0, lodBias);
                return saturate(tex2Dlod(_MainTex, bias_uv)); // Clamp final color to [0,1]

            }




            ENDCG
        }
    }
}
