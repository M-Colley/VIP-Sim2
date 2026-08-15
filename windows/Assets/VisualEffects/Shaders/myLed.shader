// PJ 13/09/2017

Shader "Hidden/VisSim/myLed"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Params("Scale (X) Ratio (Y) Brightness (Z) Shape (W)", Vector) = (80, 1, 1, 1.5)
		_Margin("Blank Margin", Float) = 0.0
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
				#include "./OVShelperFuncs.cginc"

				sampler2D _MainTex;
				half4 _Params;
				float _Margin;

				half4 frag(v2f_img i) : SV_Target
				{
					// Alpha decides whether a pixel reaches the desktop at all, so it is
					// read once from the source and carried through every branch below
					// untouched. This shader used to lose it in three separate places.
					half srcAlpha = tex2D(_MainTex, i.uv).a;

					if (i.uv.x < _Margin || i.uv.y < _Margin || (1.0 - i.uv.x) < _Margin || (1.0 - i.uv.y) < _Margin)
					{
						// Blank the margin to black, but only where the source was solid.
						// Returning alpha 1 here painted an opaque black border across the
						// desktop outside the simulated region.
						return half4(0.0, 0.0, 0.0, srcAlpha);
					}

					// Brightness and the LED grid modulation both used to be applied to all
					// four channels, so a dimmer display and the dark gaps between the LEDs
					// each ate into alpha and made the overlay patchily see-through.
					half3 rgb = pixelate(_MainTex, i.uv, _Params.x, _Params.y).rgb * _Params.z;
					half2 coord = i.uv * half2(_Params.x, _Params.x / _Params.y);
					half2 mv = abs(sin(coord * PI)) * _Params.w;
					half s = mv.x * mv.y;
					half c = step(s, 1.0);
					rgb = ((1 - c) * rgb) + ((rgb * s) * c);
					return half4(rgb, srcAlpha);
				}

			ENDCG
		}
	}

	FallBack off
}
