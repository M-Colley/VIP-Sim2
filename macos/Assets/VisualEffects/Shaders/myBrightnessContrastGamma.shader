// PJ 13/09/2017

Shader "Hidden/VisSim/myBrightnessContrastGamma"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_BCG ("Brightness (X) Contrast (Y) Gamma (Z)", Vector) = (0.0, 1.0, 1.0, 1.0)
		_Coeffs ("Contrast coeffs (RGB)", Vector) = (0.5, 0.5, 0.5, 1.0)
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
				half4 _BCG;
				half3 _Coeffs;

				half4 frag(v2f_img i) : SV_Target
				{
					half4 color = tex2D(_MainTex, i.uv);

					// Brightness, contrast and gamma apply to RGB only. Alpha is not a
					// colour channel here: VIP-Sim is composited onto the desktop by DWM
					// from the framebuffer's alpha, so anything that scales it changes
					// whether the overlay is visible at all. This used to run all four
					// channels through the same maths -- multiplying alpha by brightness
					// and then raising it to _BCG.z -- so turning the brightness down
					// faded the whole simulation out of existence rather than darkening
					// it, and gamma quietly did the same. _Coeffs is the contrast pivot
					// and is documented as RGB, which is the other half of the tell.
					half3 rgb = color.rgb * _BCG.x;
					rgb = (rgb - _Coeffs) * _BCG.y + _Coeffs;
					rgb = clamp(rgb, 0.0, 1.0);
					rgb = pow(rgb, _BCG.z);

					return half4(rgb, color.a);
				}

			ENDCG
		}
	}

	FallBack off
}
