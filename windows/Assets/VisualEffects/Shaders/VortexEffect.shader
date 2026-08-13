Shader "Hidden/VortexEffect"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _MouseX("Mouse X Position (Normalized 0 to 1)", Float) = 0.5
        _MouseY("Mouse Y Position (Normalized 0 to 1)", Float) = 0.5
        _VortexRadius("Vortex Radius", Float) = 0.5
        _SuctionStrength("Suction Strength", Float) = 0.5
        _InnerCircleRadius("Inner Circle Radius", Float) = 0.25
        _FadeWidth("Fade Width", Float) = 0.05
        _NoiseScale("Noise Scale", Float) = 10.0
        _NoiseAmount("Noise Amount", Float) = 0.1
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
            float _VortexRadius;
            float _SuctionStrength;
            float _InnerCircleRadius;
            float _FadeWidth;
            float _NoiseScale;
            float _NoiseAmount;
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

            float noise(float2 uv)
            {
                float2 p = floor(uv * _NoiseScale);
                float2 f = frac(uv * _NoiseScale);

                float a = rand(p);
                float b = rand(p + float2(1.0, 0.0));
                float c = rand(p + float2(0.0, 1.0));
                float d = rand(p + float2(1.0, 1.0));

                float u = f.x * f.x * (3.0 - 2.0 * f.x);
                float v = f.y * f.y * (3.0 - 2.0 * f.y);

                return lerp(a, b, u) +
                    (c - a) * v * (1.0 - u) +
                    (d - b) * u * v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 mousePos = float2(_MouseX, _MouseY);

                // Calculate the aspect ratio
                float aspectRatio = _ScreenWidth / _ScreenHeight;

                // Adjust the UV coordinates to maintain a circular vortex
                float2 adjustedUV = uv;
                adjustedUV.x *= aspectRatio;
                float2 adjustedMousePos = mousePos;
                adjustedMousePos.x *= aspectRatio;

                // Calculate distance from mouse position
                float2 delta = adjustedUV - adjustedMousePos;
                float dist = length(delta);

                // Apply noise to the distance to create deformed edges
                float noiseValue = noise(uv);
                dist += noiseValue * _NoiseAmount * _VortexRadius;

                // Initialize the color with the original pixel color
                fixed4 color = tex2D(_MainTex, uv);

                // Apply vortex effect only within the vortex radius
                if (dist < _VortexRadius)
                {
                    float angle = atan2(delta.y, delta.x);
                    float radius = dist / _VortexRadius;

                    // Increase suction strength as we approach the center
                    float adjustedSuctionStrength = _SuctionStrength * (1.0 - dist / _VortexRadius);
                    float vortexRadius = dist * (1.0 + adjustedSuctionStrength * (1.0 - dist / _VortexRadius));

                    // Introduce noise/distortion in the angle
                    float angleNoise = noise(float2(uv.x * _NoiseScale, uv.y * _NoiseScale));
                    angle += angleNoise * _NoiseAmount;

                    // Calculate the new vortexUV with distorted angle
                    float2 vortexUV = adjustedMousePos + vortexRadius * float2(cos(angle), sin(angle));

                    // Ensure vortexUV is within the texture bounds
                    vortexUV.x /= aspectRatio;
                    vortexUV = clamp(vortexUV, 0.0, 1.0);

                    // Fetch the color from the vortexed coordinates
                    color = tex2D(_MainTex, vortexUV);

                    


                    // Apply fade effect at the borders
                    float fade = smoothstep(_VortexRadius - _FadeWidth, _VortexRadius, dist);
                    fixed4 outerColor = lerp(color, tex2D(_MainTex, uv), fade);

                    
                    if (dist < _InnerCircleRadius)
                    {
                        // Set color to grey
                        fixed4 greyColor = fixed4(0.5, 0.5, 0.5, 1.0);
    
                        // Calculate fade factor
                        float fade = smoothstep(_InnerCircleRadius - _FadeWidth, _InnerCircleRadius, dist);
    
                        // Interpolate between grey and original color based on fade factor
                        //fixed4 outerColor = tex2D(_MainTex, uv);
                        color = lerp(greyColor, outerColor, fade);
                    }
                    else
                    {
                        // Calculate fade factor for outer fade effect
                        float fade = smoothstep(_VortexRadius - _FadeWidth, _VortexRadius, dist);
    
                        // Interpolate between vortex effect color and original color
                        fixed4 vortexColor = tex2D(_MainTex, vortexUV); // Ensure vortexUV is correctly calculated
                        color = lerp(vortexColor, tex2D(_MainTex, uv), fade);
                    }
                }

                return saturate(color); // Clamp RGB to avoid overbright artifacting

            }

            ENDCG
        }
    }
}
