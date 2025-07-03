Shader "Hidden/KriptoFX/KWS/KWS_DynamicWavesWetDecal"
{
	Properties
	{
		[HideInInspector]KWS_StencilMaskValue ("KWS_StencilMaskValue", Int) = 32
	}

	HLSLINCLUDE


	#include "../KWS_SharedAPI.cginc"
	
	struct vertexInput
	{
		float4 vertex : POSITION;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};

	struct vertexOutput
	{
		float4 vertex : SV_POSITION;
		float4 screenPos : TEXCOORD0;
		
		UNITY_VERTEX_OUTPUT_STEREO
	};


	vertexOutput vert(vertexInput v)
	{
		vertexOutput o;
		UNITY_SETUP_INSTANCE_ID(v);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
		o.vertex = ObjectToClipPos(v.vertex);
		o.screenPos = ComputeScreenPos(o.vertex);

		return o;
	}

	ENDHLSL

	Subshader
	{
		ZWrite Off
		Cull Front

		ZTest Always
		Blend SrcAlpha OneMinusSrcAlpha

		// pass 0 Albedo
		Pass
		{
			
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.6

			#pragma multi_compile _ KWS_DYNAMIC_WAVES_VISIBLE_ZONES_1 KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2 KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4 KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8
			#pragma multi_compile _ KWS_DYNAMIC_WAVES_USE_COLOR
			#pragma multi_compile _ KWS_USE_LOCAL_WATER_ZONES

			//#pragma multi_compile_fragment _ KWS_USE_DYNAMIC_WAVES KWS_USE_DYNAMIC_WAVES_ATLAS
			
			struct FragmentWetOutput
			{
				float4 GBuffer0 : SV_Target0;
				float4 GBuffer1 : SV_Target1;
				float4 GBuffer2 : SV_Target2;
			};
			
			FragmentWetOutput frag(vertexOutput i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				FragmentWetOutput o = (FragmentWetOutput)0;

				float2 screenUV = i.screenPos.xy / i.screenPos.w;
				float depth = GetSceneDepth(screenUV);

				float3 diffuseColor;
				float wetMap;
				float occlusion;
				float smoothness;
				float metallic;
				GetWetnessData(screenUV, depth, diffuseColor, wetMap, occlusion, smoothness, metallic);

				
				o.GBuffer0 = float4(diffuseColor, occlusion);
				o.GBuffer1 = float4(0, 0, 0, smoothness);
				o.GBuffer2 = float4(0, 0, 0, metallic);

				return o;
			}

			ENDHLSL
		}
	}
}