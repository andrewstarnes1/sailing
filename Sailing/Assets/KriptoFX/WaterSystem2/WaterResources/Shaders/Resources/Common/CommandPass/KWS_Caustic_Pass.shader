﻿Shader "Hidden/KriptoFX/KWS/Caustic_Pass"
{
	HLSLINCLUDE

	#include "../../Common/KWS_WaterHelpers.cginc"
	
	float KWS_CausticDepthScale;
	uint KWS_CausticCascadeIndex;
	//float KWS_CausticCascadeDepthScale;

	static const float DepthScaleRelativeToCascade[3] =
	{
		0.2,
		0.04,
		0.01
	};

	static const float DepthScaleRelativeToWind[3] =
	{
		1,
		0.5,
		0.25
	};

	static const float MaxDepthScaleRelativeToWind[3] =
	{
		1.5,
		0.25,
		0.05
	};

	struct appdata_caustic
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct v2f_caustic
	{
		float4 vertex : SV_POSITION;
		float3 oldPos : TEXCOORD0;
		float3 newPos : TEXCOORD1;
	};

	float GetDepthScale()
	{
		float normalizedWind = saturate(max(0, KWS_WindSpeed-1) / 8.0) ;
		float depthScale = lerp(KWS_CausticDepthScale, KWS_CausticDepthScale * DepthScaleRelativeToWind[KWS_CausticCascadeIndex], normalizedWind) * DepthScaleRelativeToCascade[KWS_CausticCascadeIndex];
		depthScale = min(depthScale, MaxDepthScaleRelativeToWind[KWS_CausticCascadeIndex]);
		return depthScale;
	}

	float2 ComputeDisplacementXZ(float2 uv)
	{
		#ifdef KWS_USE_CAUSTIC_FILTERING
			float2 displacement = GetFftWavesDisplacementSliceBicubic(float3(uv.x, 0, -uv.y) * GetDomainSize(KWS_CausticCascadeIndex), KWS_CausticCascadeIndex).xz;
		#else 
			float2 displacement = GetFftWavesDisplacementSlice(float3(uv.x, 0, -uv.y) * GetDomainSize(KWS_CausticCascadeIndex), KWS_CausticCascadeIndex).xz;
		#endif
		
		

		displacement *= float2(1, -1);
		displacement *= GetDepthScale();
		return displacement;
	}
	
	v2f_caustic vert_caustic(appdata_caustic v)
	{
		v2f_caustic o;
		
		o.oldPos = v.vertex.xyz;
		v.vertex.xy += ComputeDisplacementXZ(v.vertex.xy);
		o.newPos = v.vertex.xyz;

		o.vertex = float4(v.vertex.xy, 0, 0.5);
		return o;
	}

	half4 frag_caustic(v2f_caustic i) : SV_Target
	{
		float oldArea = length(ddx(i.oldPos.xyz)) * length(ddy(i.oldPos.xyz));
		float newArea = length(ddx(i.newPos.xyz)) * length(ddy(i.newPos.xyz));

		float color = oldArea / newArea * KWS_CAUSTIC_MULTIPLIER;
		return  float4(color.xxx, 1);
	}

		ENDHLSL

	SubShader
	{
		//Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
		
		Blend One One
		ZWrite Off
		ZTest Always
		Cull Off

		Pass
		{
			HLSLPROGRAM
			
			#pragma vertex vert_caustic
			#pragma fragment frag_caustic

			#pragma multi_compile _ KWS_USE_CAUSTIC_FILTERING

			ENDHLSL
		}
	}
}
