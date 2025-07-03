Shader "Hidden/KriptoFX/KWS/KWS_JumpFloodSDF"
{

	HLSLINCLUDE

	
	#include "../../Common/KWS_WaterHelpers.cginc"

	Texture2D _SourceRT;

	float4 _SourceRT_TexelSize;


	float2 KWS_StepSize;


	struct v2f
	{
		float2 uv : TEXCOORD0;
		float4 vertex : SV_POSITION;
	};

	v2f vert(uint vertexID : SV_VertexID)
	{
		v2f o;
		o.vertex = GetTriangleVertexPosition(vertexID);
		o.uv = GetTriangleUVScaled(vertexID);
		return o;
	}
	

	float4 fragPrePass(v2f i) : SV_Target
	{
		//float near = KWS_OrthoDepthNearFarSize.x;
		//float far = KWS_OrthoDepthNearFarSize.y;
		//float terrainDepth = _SourceRT.SampleLevel(sampler_linear_clamp, i.uv, 0).r * (far - near) - far;
		//return saturate(-terrainDepth);
		float terrainDepth = GetWaterOrthoDepth(i.uv) - KWS_WaterPosition.y;

		if (abs(terrainDepth) < 2) return float4(i.uv, 0, 1);
		else return 1;
	}

	struct FragmentOutput
	{
		half2 dest0 : SV_Target0;
		half dest1 : SV_Target1;
	};

	bool IsBorder(float2 pos)
	{
		return pos.x <= 0.0 || pos.x >= 1.0 || pos.y <= 0.0 || pos.y >= 1.0;
	}

	FragmentOutput fragJumpFlood(v2f i) : SV_Target
	{
		FragmentOutput o = (FragmentOutput)0;

		float closest_dist = 9999999.9;
		float2 closest_pos = 0;

		for (float x = -1.0; x <= 1.0; x += 1.0)
		{
			for (float y = -1.0; y <= 1.0; y += 1.0)
			{
				float2 voffset = i.uv + float2(x, y) * _SourceRT_TexelSize.xy * KWS_StepSize;

				//if (IsOutsideUvBorders(voffset.xy))                            continue;

				float2 pos = _SourceRT.SampleLevel(sampler_point_clamp, voffset, 0).xy;
				float dist = distance(pos * _SourceRT_TexelSize.zw, i.uv * _SourceRT_TexelSize.zw);

				if (pos.x != 0.0 && pos.y != 0.0 && dist < closest_dist && !IsBorder(pos))
				{
					closest_dist = dist;
					closest_pos = pos;
				}
			}
		}
		o.dest0 = closest_pos;
		o.dest1 = closest_dist;
		return o;
	}

	ENDHLSL

	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always

		//copy source color to depth
		Pass
		{
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment fragPrePass

			ENDHLSL
		}

		//jump flood pass
		Pass
		{
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment fragJumpFlood

			ENDHLSL
		}
	}
}