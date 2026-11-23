// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// PJ 13/09/2017
// http://www.gedalia.net/portfolio/3d2ddeformation.html
// https://github.com/BradLarson/GPUImage <-- http://stackoverflow.duapp.com/questions/9886843/how-can-you-apply-distortions-to-a-uiimage-using-opengl-es/9896856#9896856

Shader "Hidden/VisSim/myDistortionMap"
{
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}

	SubShader {
		Pass {
			ZTest Always Cull Off ZWrite Off
						
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#include "UnityCG.cginc"

				uniform sampler2D _MainTex;
				uniform float4 _MainTex_TexelSize;
				half4 _MainTex_ST;
				sampler2D _WarpTextureX;
				half4 _WarpTextureX_ST;
				sampler2D _WarpTextureY;
				half4 _WarpTextureY_ST;
				half _MouseX, _MouseY;

				struct v2f {
					float4 pos : SV_POSITION;
					float2 uv : TEXCOORD0;
				};

				v2f vert( appdata_img v )
				{
					v2f o;
					o.pos = UnityObjectToClipPos (v.vertex);
					o.uv = half4(v.texcoord.xy, 1, 1);
					return o;
				}

				float4 frag (v2f i) : SV_Target
				{


					//float2 degcoords = i.uv; // use this to make non-mouse-contingent
					
					// make texture coordinate mouse-contingent
					// correct mouse y-coordinates if on a platform where '0' is the top (e.g., Direct3D-like)
					# if UNITY_UV_STARTS_AT_TOP
						_MouseY = 1.0 - _MouseY;
					# endif
					float2 mouseOffset = (half2(0.5, 0.5) - half2(_MouseX, _MouseY)); // map 0-1 (min/max of screen) to -.5 to +.5
					float2 degcoords = i.uv - mouseOffset; 
					degcoords = degcoords*.5 + .25; // map 0-1 to 0.25-0.75 (i.e., since deg tex is twice as big) [this has to be a hack! must be a better way of doing this...)

					// is this necessary???
					//degcoords = UnityStereoScreenSpaceUVAdjust(degcoords, _Overlay_ST);

					float4 warpX = tex2D(_WarpTextureX, degcoords);
					float4 warpY = tex2D(_WarpTextureY, degcoords);

					// For debugging
					//return warpX;

					// The exact inverse of the encoding in myDistortionMap.compute (v * 5 + 0.5).
					// This was 0.2 * w - 0.0425, a constant tuned to cancel the sRGB decode of a
					// texture that should never have been sRGB; the textures are linear now.
					float w = (warpX.x + warpX.y + warpX.z) / 3.0; // would be better to also use the alpha channel too, and get another 8-bits of precision...
					float xOffset = (0.2 * w) - 0.1; // rescale(0 to 1) -> (-.1 to .1)
					xOffset = -xOffset;

					w = (warpY.x + warpY.y + warpY.z) / 3.0;
					float yOffset = (0.2 * w) - 0.1; // rescale(0 to 1) -> (-.1 to .1)

					// Dead zone for 8-bit quantisation: 0.5 is stored as 128/255, which decodes to
					// an offset of 0.0004 -- about a pixel -- where there should be none.
					if (xOffset > -0.001 && xOffset < 0.001) {
						xOffset = 0;
					}
					if (yOffset > -0.001 && yOffset < 0.001) {
						yOffset = 0;
					}

					float2 offset = i.uv;
					offset.x += xOffset;
					offset.y += yOffset;
					
					return tex2D(_MainTex, UnityStereoScreenSpaceUVAdjust(offset, _MainTex_ST));
				}
			ENDCG
		}
	}

	Fallback off
}