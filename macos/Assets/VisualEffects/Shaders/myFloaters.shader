Shader "Hidden/VisSim/myFloaters"
{
	Properties{
		_MainTex("Screen Blended", 2D) = "" {}
		_Overlay("Color", 2D) = "grey" {}
		_MouseX ("Mouse X", Float) = 0.5
        _MouseY ("Mouse Y", Float) = 0.5
		_WarpParams("Frequency (X) Amplitude (Y) Timer (Z)", Vector) = (0, 0, 0, 0)
		_ScintillateParams("Seed (X) Strength (Y) Lum Contribution (Z)", Vector) = (0, 0, 0, 0)
	}

	CGINCLUDE

		#include "UnityCG.cginc"
		#include "./OVShelperFuncs.cginc"

		struct v2f {
			float4 pos : SV_POSITION;
			float2 uv[2] : TEXCOORD0;
		};

		sampler2D _Overlay;
		half4 _Overlay_ST;

		sampler2D _MainTex;
		half4 _MainTex_ST;

		half _Intensity;
		half4 _MainTex_TexelSize;
		half4 _UV_Transform = half4(1, 0, 0, 1);

		half3 _WarpParams;

		half3 _ScintillateParams;

		float _MouseX; // Declare _MouseX
        float _MouseY; // Declare _MouseY

		v2f vert(appdata_img v) {
			v2f o;
			o.pos = UnityObjectToClipPos(v.vertex);

			o.uv[0] = UnityStereoScreenSpaceUVAdjust(float2(
				dot(v.texcoord.xy, _UV_Transform.xy),
				dot(v.texcoord.xy, _UV_Transform.zw)
				), _Overlay_ST);

			#if UNITY_UV_STARTS_AT_TOP
			if (_MainTex_TexelSize.y < 0.0)
				o.uv[0].y = 1.0 - o.uv[0].y;
			#endif

			o.uv[1] = UnityStereoScreenSpaceUVAdjust(v.texcoord.xy, _MainTex_ST);
			return o;
		}

		half4 fragScreen_dark(v2f i) : SV_Target{
			half2 t = i.uv[0];

			t.x += (_MouseX - 0.5);
            t.y += (_MouseY - 0.5);
			t.x += sin(_WarpParams.z + t.x * _WarpParams.x) * _WarpParams.y;
			t.y += cos(_WarpParams.z + t.y * _WarpParams.x) * _WarpParams.y - _WarpParams.y;

			// Clamp UV coordinates to prevent repetition
            t = clamp(t, 0.0, 1.0);

			//half color = tex2D(_MainTex, i.uv[1]);
			//half4 overlay = tex2D(_Overlay, t);
			//half4 toBlend = (overlay * _Intensity);
			//return  (1 - toBlend)*color; // black
			// Floaters darken what you are looking at; they do not punch holes in the
			// overlay. Modulate RGB, carry the source alpha through untouched.
			half4 color = tex2D(_MainTex, i.uv[1]);
			half3 rgb = (1 - (tex2D(_Overlay, t).rgb * _Intensity)) * color.rgb; // black
			return half4(rgb, color.a);
		}

		half4 fragScreen_light(v2f i) : SV_Target{
			half2 t = i.uv[0];
			
			t.x += (_MouseX - 0.5);
            t.y += (_MouseY - 0.5);
			t.x += sin(_WarpParams.z + t.x * _WarpParams.x) * _WarpParams.y;
			t.y += cos(_WarpParams.z + t.y * _WarpParams.x) * _WarpParams.y - _WarpParams.y;

			// Clamp UV coordinates to prevent repetition
            t = clamp(t, 0.0, 1.0);

			//half color = tex2D(_MainTex, i.uv[1]);
			//half4 overlay = tex2D(_Overlay, t);
			//half4 toBlend = (overlay * _Intensity);
			//return 1 - (1 - toBlend)*(1 - color);
			// white -- screen blend on RGB only; alpha is the compositing channel and
			// this used to drive it towards 1, making the whole overlay opaque.
			half4 color = tex2D(_MainTex, i.uv[1]);
			half3 rgb = 1 - (1 - (tex2D(_Overlay, t).rgb * _Intensity)) * (1 - color.rgb);
			return half4(rgb, color.a);
		}

		half4 fragScreen_scintillate(v2f i) : SV_Target{
			half2 t = i.uv[0];

			t.x += (_MouseX - 0.5);
            t.y += (_MouseY - 0.5);
			t.x += sin(_WarpParams.z + t.x * _WarpParams.x) * _WarpParams.y;
			t.y += cos(_WarpParams.z + t.y * _WarpParams.x) * _WarpParams.y - _WarpParams.y;

			half4 color = tex2D(_MainTex, i.uv[1]);
			half4 overlay = tex2D(_Overlay, t);

			//scintillating
			if ((overlay.x + overlay.y + overlay.z) > 0.05)
			{
				// generate and apply noise
				float n = simpleNoise_fracLess(i.uv[1] + _ScintillateParams.x);
				float nr = frac(n) * 2.0;
				float ng = frac(n * 1.2154) * 2.0;
				float nb = frac(n * 1.3453) * 2.0;
				float na = frac(n * 1.3647) * 2.0;
				half lum = luminance(color.rgb);
				half4 scintillated = lerp(color, color * half4(nr, ng, nb, na), _ScintillateParams.y * (1.0 - lerp(0.0, lum, _ScintillateParams.z)));
				// Scintillation is a colour phenomenon: noise on alpha would flicker the
				// overlay in and out of the desktop rather than shimmer it.
				return half4(scintillated.rgb, color.a);
			}
			else
			{
				return color;
			}
		}

	ENDCG

	Subshader {
		ZTest Always Cull Off ZWrite Off
			// Must write alpha as well as RGB. With ColorMask RGB this shader left the
			// destination's alpha untouched, which is fine when it blits into an
			// intermediate RenderTexture that already has alpha, but not when it is the
			// last effect in the chain and blits into the fresh, fully transparent
			// backbuffer -- there was then nothing to inherit and the floaters, along
			// with the rest of the simulation, never appeared. Each pass now carries the
			// source alpha through explicitly, so the effect behaves the same wherever
			// it lands in the chain.
			ColorMask RGBA

		// (0) Dark
		Pass{
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment fragScreen_dark
				#pragma fragmentoption ARB_precision_hint_fastest // versus ARB_precision_hint_nicest
			ENDCG
		}

		// (1) Light
		Pass{
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment fragScreen_light
				#pragma fragmentoption ARB_precision_hint_fastest // versus ARB_precision_hint_nicest
			ENDCG
		}

		// (2) Scintillating
		Pass{
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment fragScreen_scintillate
				#pragma fragmentoption ARB_precision_hint_fastest // versus ARB_precision_hint_nicest
			ENDCG
		}
	}

	Fallback off

} // shader