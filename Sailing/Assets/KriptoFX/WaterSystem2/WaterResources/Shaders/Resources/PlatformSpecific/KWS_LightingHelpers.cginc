#ifndef KWS_LIGHTING_HELPERS
#define KWS_LIGHTING_HELPERS



#ifdef KWS_BUILTIN

	
	#ifndef UNITY_LIGHTING_COMMON_INCLUDED
		#include "UnityLightingCommon.cginc"
	#endif

	#ifndef KWS_LIGHTING_COMMON_INCLUDED
		#include "KWS_Lighting.cginc"
	#endif


	inline half3 KWS_ComputeDirLightShadow(float3 worldPos, float shadowStrengthAdditive, bool useNoiseShadows, float2 screenUV)
	{
		ShadowLightData light = KWS_DirLightsBuffer[0];
		float3 shadowPos = worldPos;
		if (useNoiseShadows)
		{
			float3 blueNoise = KWS_BlueNoise3D.SampleLevel(sampler_linear_repeat, (screenUV * _ScreenParams.xy) / 128.0, 0).xyz;
			shadowPos += (blueNoise * 2 - 1) * 0.25;
		}
		float atten = DirLightRealtimeShadow(0, shadowPos);
		atten = lerp(1, atten, light.shadowStrength);
		return saturate(atten + shadowStrengthAdditive);
	}

	inline half KWS_ComputeUnderwaterLightingAttenuation(float3 worldPos)
	{
		#define KWS_DISABLE_POINT_SPOT_SHADOWS
		
		float attenuation = 0;
		#if defined(KWS_USE_POINT_LIGHTS)
		UNITY_LOOP
		for (uint pointIdx = 0; pointIdx < KWS_PointLightsCount; pointIdx++)
		{
			KWS_LightData light = KWS_PointLightsBuffer[pointIdx];
			attenuation += PointLightAttenuation(pointIdx, worldPos);
		}

		UNITY_LOOP
		for (uint pointShadowIdx = 0; pointShadowIdx < KWS_ShadowPointLightsCount; pointShadowIdx++)
		{
			ShadowLightData light = KWS_ShadowPointLightsBuffer[pointShadowIdx];
			attenuation += PointLightAttenuationShadow(pointShadowIdx, worldPos);

		}
		
		#endif

		#if defined(KWS_USE_SPOT_LIGHTS)
		UNITY_LOOP
		for (uint SpotIdx = 0; SpotIdx < KWS_SpotLightsCount; SpotIdx++)
		{
			KWS_LightData light = KWS_SpotLightsBuffer[SpotIdx];
			attenuation += SpotLightAttenuation(SpotIdx, worldPos);
		}

		UNITY_LOOP
		for (uint shadowSpotIdx = 0; shadowSpotIdx < KWS_ShadowSpotLightsCount; shadowSpotIdx++)
		{
			ShadowLightData light = KWS_ShadowSpotLightsBuffer[shadowSpotIdx];
			attenuation += SpotLightAttenuationShadow(shadowSpotIdx, worldPos);
		}
		#endif
		return saturate(attenuation);
	}

	inline half4 KWS_ComputeLighting(float3 worldPos, float shadowStrengthAdditive, bool useNoiseShadows, float2 screenUV)
	{
		half3 result = 0;
		float dirLightAtten = 1;

		#if defined(KWS_USE_DIR_LIGHT)
			ShadowLightData light = KWS_DirLightsBuffer[0];

			#ifdef KWS_USE_DIR_SHADOW
				dirLightAtten = KWS_ComputeDirLightShadow(worldPos, shadowStrengthAdditive, useNoiseShadows, screenUV);
				light.color *= dirLightAtten;
			#endif
		
			light.color *= saturate(dot(light.forward, float3(0, 1, 0)));
			result += light.color;
		#endif

		#if defined(KWS_USE_POINT_LIGHTS)
			UNITY_LOOP
			for (uint pointIdx = 0; pointIdx < KWS_PointLightsCount; pointIdx++)
			{
				KWS_LightData light = KWS_PointLightsBuffer[pointIdx];
				light.color *= PointLightAttenuation(pointIdx, worldPos);
				result += light.color;
			}

			UNITY_LOOP
			for (uint pointShadowIdx = 0; pointShadowIdx < KWS_ShadowPointLightsCount; pointShadowIdx++)
			{
				ShadowLightData light = KWS_ShadowPointLightsBuffer[pointShadowIdx];
				light.color *= PointLightAttenuationShadow(pointShadowIdx, worldPos);
				result += light.color;
			}
		
		#endif

		#if defined(KWS_USE_SPOT_LIGHTS)
			UNITY_LOOP
			for (uint SpotIdx = 0; SpotIdx < KWS_SpotLightsCount; SpotIdx++)
			{
				KWS_LightData light = KWS_SpotLightsBuffer[SpotIdx];
				light.color *= SpotLightAttenuation(SpotIdx, worldPos);
				result += light.color;
			}

			UNITY_LOOP
			for (uint shadowSpotIdx = 0; shadowSpotIdx < KWS_ShadowSpotLightsCount; shadowSpotIdx++)
			{
				ShadowLightData light = KWS_ShadowSpotLightsBuffer[shadowSpotIdx];
				light.color *= SpotLightAttenuationShadow(shadowSpotIdx, worldPos);
				result += light.color;
			}
		#endif
		
				
		float3 ambient = GetAmbientColor(GetExposure());
		return float4(ambient + result, dirLightAtten);
	}



#endif
#endif


















/////////////////////////////////////////////////////////////// URP ////////////////////////////////////////////////////////////

#ifdef KWS_URP


	#ifndef UNIVERSAL_LIGHTING_INCLUDED
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
	#endif


	int KWS_AdditionalLightsCount;

	int KWS_GetAdditionalLightsCount()
	{
		#if USE_FORWARD_PLUS
			return 0;
		#else
			return KWS_AdditionalLightsCount;
		#endif
	}

	
	inline half KWS_ComputeDirLightShadow(float3 worldPos, float shadowStrengthAdditive, bool useNoiseShadows, float2 screenUV)
	{
		float3 shadowPos = worldPos;
		if (useNoiseShadows)
		{
			float3 blueNoise = KWS_BlueNoise3D.SampleLevel(sampler_linear_repeat, (screenUV * _ScreenParams.xy) / 128.0, 0).xyz;
			shadowPos += (blueNoise * 2 - 1) * 0.25;
		}
		float atten = MainLightRealtimeShadow(TransformWorldToShadowCoord(shadowPos));
		return saturate(atten + shadowStrengthAdditive);
	}

	inline half KWS_ComputeUnderwaterLightingAttenuation(float3 worldPos)
	{
		float attenuation = 0;
		#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
			
		uint pixelLightCount = KWS_GetAdditionalLightsCount();
		InputData inputData = (InputData)0;
		inputData.positionWS = worldPos;
		
		LIGHT_LOOP_BEGIN(pixelLightCount)
			Light light = GetAdditionalPerObjectLight(lightIndex, worldPos);
			float atten = saturate(light.distanceAttenuation);
			attenuation += atten;
		LIGHT_LOOP_END

		#endif
		return saturate(attenuation);
	}


	inline half4 KWS_ComputeLighting(float3 worldPos, float shadowStrengthAdditive, bool useNoiseShadows, float2 screenUV)
	{
		half3 result = 0;
		
		Light light = GetMainLight();
		float dirLightAtten = 1;
		#ifdef KWS_USE_DIR_SHADOW
			dirLightAtten *= KWS_ComputeDirLightShadow(worldPos, shadowStrengthAdditive, useNoiseShadows, screenUV);
			light.color *= dirLightAtten;
		#endif
		light.color *= saturate(dot(light.direction, float3(0, 1, 0)));
		
		result += light.color;

		InputData inputData = (InputData)0;
		inputData.normalizedScreenSpaceUV = screenUV;
		inputData.positionWS = worldPos;

		#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
			
			uint pixelLightCount = KWS_GetAdditionalLightsCount();

			LIGHT_LOOP_BEGIN(pixelLightCount)
			
			Light light = GetAdditionalPerObjectLight(lightIndex, worldPos);
			float atten = saturate(light.distanceAttenuation);
		#ifdef KWS_USE_ADDITIONAL_SHADOW
			atten *= AdditionalLightRealtimeShadow(lightIndex, worldPos, light.direction);
		#endif
			light.color *= atten;
			result += light.color;

			LIGHT_LOOP_END


		#endif
		//float3 ambient = SampleSH(float3(0, 1, 0));
		float3 ambient = GetAmbientColor(GetExposure());
		return float4(ambient + result, dirLightAtten);
	}


#endif



























/////////////////////////////////////////////////////////////// KWS_HDRP ////////////////////////////////////////////////////////////

#ifdef KWS_HDRP

	#define DIR_LIGHTING_MULTIPLIER_HDRP 0.3

	#define SHADOW_LOW
	#define AREA_SHADOW_LOW
	
	#define LIGHT_EVALUATION_NO_CONTACT_SHADOWS // To define before LightEvaluation.hlsl
	#define LIGHT_EVALUATION_NO_HEIGHT_FOG

	#define SHADOW_LOW          // Different options are too expensive.
	#define AREA_SHADOW_LOW
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Builtin/BuiltinData.hlsl"

	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"

	// #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/VolumetricLighting/VolumetricLighting.cs.hlsl"
	// #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/VolumetricLighting/VBuffer.hlsl"

	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/PhysicallyBasedSky/PhysicallyBasedSkyCommon.hlsl"

	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoopDef.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightEvaluation.hlsl"

	inline half3 KWS_ComputeDirLightShadow(float3 worldPos, float shadowStrengthAdditive, bool useNoiseShadows, float2 screenUV)
	{
		
		float dirLightAtten = 1;
		float3 cameraRelativeWorldPos = GetCameraRelativePositionWS(worldPos);
		uint lightIdx = 0;

		for (lightIdx = 0; lightIdx < _DirectionalLightCount; ++lightIdx)
		{
			DirectionalLightData light = _DirectionalLightDatas[lightIdx];
			
			LightLoopContext context;
			context.shadowContext = InitShadowContext();
			PositionInputs posInput;
			posInput.positionWS = cameraRelativeWorldPos;
			
			if (light.volumetricLightDimmer > 0)
			{
				float3 atten = 1;
				if (_DirectionalShadowIndex >= 0 && (uint)_DirectionalShadowIndex == lightIdx && (light.volumetricShadowDimmer > 0))
				{
					dirLightAtten *= GetDirectionalShadowAttenuation(context.shadowContext, screenUV, posInput.positionWS, 0, light.shadowIndex, -light.forward);
				}
			}
		}
		return dirLightAtten;
	}


	inline half KWS_ComputeUnderwaterLightingAttenuation(float3 worldPos)
	{
		float attenuation = 0;
		float3 cameraRelativeWorldPos = GetCameraRelativePositionWS(worldPos);
		uint lightIdx = 0;
		while(lightIdx < _PunctualLightCount)
		{
			LightData light = FetchLight(lightIdx);
			
			float3 L;
			float4 distances; // {d, d^2, 1/d, d_proj}
			
			GetPunctualLightVectors(cameraRelativeWorldPos, light, L, distances);
			float atten = PunctualLightAttenuation(distances, light.rangeAttenuationScale, light.rangeAttenuationBias, light.angleScale, light.angleOffset);
			attenuation += atten;

			lightIdx++;
		}
		
		return saturate(attenuation);
	}


	inline half4 KWS_ComputeLighting(float3 worldPos, float shadowStrengthAdditive = 0, bool useNoiseShadows = false, float2 screenUV = 0)
	{
		float3 result = 0;

		float dirLightAtten = 1;
		float exposure = GetExposure();
		float3 cameraRelativeWorldPos = GetCameraRelativePositionWS(worldPos);
		uint lightIdx = 0;

		for (lightIdx = 0; lightIdx < _DirectionalLightCount; ++lightIdx)
		{
			DirectionalLightData light = _DirectionalLightDatas[lightIdx];
			float3 L = -light.forward;
			float sunAngleAttenuation = GetVolumeLightSunAngleAttenuation(L);

			LightLoopContext context;
			context.shadowContext = InitShadowContext();
			PositionInputs posInput;
			posInput.positionWS = cameraRelativeWorldPos;
			
			float4 lightColor = EvaluateLight_Directional(context, posInput, light);
			lightColor.a *= light.volumetricLightDimmer;
			
			#ifdef KWS_USE_DIR_SHADOW
				if (light.volumetricLightDimmer > 0)
				{
					float3 atten = 1;
					if (_DirectionalShadowIndex >= 0 && (uint)_DirectionalShadowIndex == lightIdx && (light.volumetricShadowDimmer > 0))
					{
						float shadow = GetDirectionalShadowAttenuation(context.shadowContext, screenUV, posInput.positionWS, 0, light.shadowIndex, L);
						dirLightAtten *= shadow;
						atten = shadow;
						atten *= GetDirectionalShadowAttenuation(context.shadowContext, screenUV, posInput.positionWS, 0, light.shadowIndex, L);
						atten = saturate(atten + shadowStrengthAdditive);
						atten = lerp(float3(1, 1, 1), atten, light.volumetricShadowDimmer);
						//lightColor.rgb *= ComputeShadowColor(atten, light.shadowTint, light.penumbraTint); //why always black?
					}
					lightColor.rgb *= atten;
				}
			#endif
			lightColor.rgb *= lightColor.a;
			lightColor.rgb *= saturate(dot(float3(0, 1, 0), L));

			result += lightColor.rgb * DIR_LIGHTING_MULTIPLIER_HDRP;
		}

		LightLoopContext context;
		context.shadowContext = InitShadowContext();
		
		lightIdx = 0;

		while(lightIdx < _PunctualLightCount)
		{
			LightData light = FetchLight(lightIdx);
			
			float3 L;
			float4 distances; // {d, d^2, 1/d, d_proj}
			
			float4 lightColor = float4(light.color, 1.0);
			lightColor.a *= light.volumetricLightDimmer;
			lightColor.rgb *= lightColor.a;
			
			GetPunctualLightVectors(cameraRelativeWorldPos, light, L, distances);
			float atten = PunctualLightAttenuation(distances, light.rangeAttenuationScale, light.rangeAttenuationBias, light.angleScale, light.angleOffset);

			#ifdef KWS_USE_ADDITIONAL_SHADOW
				if (distances.x < light.range && atten > 0.0 && L.y > 0.0 && light.shadowIndex >= 0 && light.shadowDimmer > 0)
				{
					atten *= GetPunctualShadowAttenuation(context.shadowContext, screenUV, cameraRelativeWorldPos, 0, light.shadowIndex, L, distances.x, light.lightType == GPULIGHTTYPE_POINT, light.lightType != GPULIGHTTYPE_PROJECTOR_BOX);
				}
			#endif

			lightColor.rgb *= atten;

			result += lightColor.rgb;
			
			lightIdx++;
		}
		
		float3 ambient = GetAmbientColor(1);
		return float4((ambient + result) * exposure, dirLightAtten);
	}


	

#endif


