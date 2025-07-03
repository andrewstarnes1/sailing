void RayMarchDirLight(RaymarchData raymarchData, inout RaymarchResult result)
{
	result.DirLightScattering = 0;
	result.DirLightSurfaceShadow = 1;
	result.DirLightSceneShadow = 1;

	
	float3 finalScattering = 0;
	//* GetVolumeLightInDepthTransmitance(raymarchData.waterHeight, GetCameraAbsolutePosition().y, raymarchData.transparent);
	float transmittance = 1;
	
	#if defined(KWS_USE_DIR_LIGHT)
		float3 currentPos = raymarchData.rayStart;
		float3 turbidityColor = raymarchData.turbidityColor.xyz;
		float transparent = raymarchData.transparent;

		// #if defined(KWS_USE_LOCAL_WATER_ZONES)
		// 	
		// 	float noise = InterleavedGradientNoise(raymarchData.uv * _ScreenParams.xy, KWS_Time) * 2 - 1;
		//
		// 	LocalZoneData blendedZone = (LocalZoneData)0;
		// 	blendedZone.transparent = transparent;
		// 	blendedZone.turbidityColor.xyz = turbidityColor;
		//
		// 	EvaluateBlendedZoneData(blendedZone, raymarchData.rayStart, raymarchData.rayDir, raymarchData.rayLengthToWaterZ, raymarchData.waterHeight + 20, noise);
		// 	transparent = blendedZone.transparent;
		// 	turbidityColor.xyz = blendedZone.turbidityColor.xyz;
		//
		// #endif
		//
		float rayLength = GetMaxRayDistanceRelativeToTransparent(transparent);
		float3 step = raymarchData.rayDir * rayLength / (float)KWS_RayMarchSteps;
		currentPos += step * raymarchData.offset;


		ShadowLightData light = KWS_DirLightsBuffer[0];
		
		float sunAngleAttenuation = GetVolumeLightSunAngleAttenuation(light.forward.xyz);
		finalScattering = GetAmbientColor(GetExposure()) * 0.5;
		finalScattering *= GetVolumeLightInDepthTransmitance(raymarchData.waterHeight, currentPos.y, transparent);
		finalScattering *= sunAngleAttenuation;

		
		float3 reflectedStep = reflect(raymarchData.rayDir, float3(0, -1, 0)) * (raymarchData.rayLength / (float)KWS_RayMarchSteps);
		

		UNITY_LOOP
		for (uint i = 0; i < KWS_RayMarchSteps; ++i)
		{
			if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToWaterZ) break;
			//if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToWaterZ) step = reflectedStep; //todo check how to clip correctly internal reflection

			
			half atten = 1;
			UNITY_BRANCH
			if (KWS_UseDirLightShadow == 1)	atten *= DirLightRealtimeShadow(0, currentPos);
			
			#if defined(KWS_USE_CAUSTIC) || defined(KWS_USE_ADDITIONAL_CAUSTIC)
				atten += atten * RaymarchCaustic(raymarchData, currentPos, light.forward);
			#endif
			atten *= sunAngleAttenuation;
			atten *= GetVolumeLightInDepthTransmitance(raymarchData.waterHeight, currentPos.y, transparent);
			
			IntegrateLightSlice(finalScattering, transmittance, atten, rayLength);
			
			currentPos += step;
		}

		finalScattering *= light.color.xyz * turbidityColor.xyz;
		
		result.DirLightSurfaceShadow = DirLightRealtimeShadow(0, raymarchData.rayStart);
		#if defined(KWS_USE_CAUSTIC) || defined(KWS_USE_ADDITIONAL_CAUSTIC)
			result.DirLightSceneShadow = DirLightRealtimeShadow(0, raymarchData.rayEnd);
		#endif
	#endif
	
	result.DirLightScattering.rgb = finalScattering;
	result.DirLightScattering.a = saturate(1 - transmittance * (1 + KWS_VOLUME_LIGHT_TRANSMITANCE_NEAR_OFFSET_FACTOR));
}


void RayMarchAdditionalLights(RaymarchData raymarchData, inout RaymarchResult result)
{
	result.AdditionalLightsScattering = 0;
	result.AdditionalLightsSceneAttenuation = 0;

	
	float3 reflectedStep = reflect(raymarchData.rayDir, float3(0, -1, 0)) * (raymarchData.rayLength / KWS_RayMarchSteps);

	#if KWS_USE_POINT_LIGHTS
		UNITY_LOOP
		for (uint pointIdx = 0; pointIdx < KWS_PointLightsCount; pointIdx++)
		{

			float3 scattering = 0;
			float transmittance = 1;
			KWS_LightData light = KWS_PointLightsBuffer[pointIdx];
			result.AdditionalLightsSceneAttenuation = max(result.AdditionalLightsSceneAttenuation, PointLightAttenuation(pointIdx, raymarchData.rayEnd));
			
			float3 step = raymarchData.step;
			float3 currentPos = raymarchData.currentPos;
			UNITY_LOOP
			for (uint i = 0; i < KWS_RayMarchSteps; ++i)
			{
				if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToSceneZ) break;
				if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToWaterZ) step = reflectedStep;

				half atten = PointLightAttenuation(pointIdx, currentPos);
				IntegrateAdditionalLight(raymarchData, scattering, transmittance, atten, light.position, step, currentPos);
			}
			result.AdditionalLightsScattering.rgb += scattering * light.color * raymarchData.turbidityColor;
			result.AdditionalLightsScattering.a *= saturate(1 - transmittance);
		}

	#endif

	#if KWS_USE_SHADOW_POINT_LIGHTS
		
		UNITY_LOOP
		for (uint shadowPointIdx = 0; shadowPointIdx < KWS_ShadowPointLightsCount; shadowPointIdx++)
		{
			float3 scattering = 0;
			float transmittance = 1;
			ShadowLightData light = KWS_ShadowPointLightsBuffer[shadowPointIdx];
			result.AdditionalLightsSceneAttenuation = max(result.AdditionalLightsSceneAttenuation, PointLightAttenuationShadow(shadowPointIdx, raymarchData.rayEnd));
			
			float3 step = raymarchData.step;
			float3 currentPos = raymarchData.currentPos;
			UNITY_LOOP
			for (uint i = 0; i < KWS_RayMarchSteps; ++i)
			{
				if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToSceneZ) break;
				if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToWaterZ) step = reflectedStep;

				float atten = PointLightAttenuationShadow(shadowPointIdx, currentPos);
				IntegrateAdditionalLight(raymarchData, scattering, transmittance, atten, light.position, step, currentPos);
			}
			result.AdditionalLightsScattering.rgb += scattering * light.color * raymarchData.turbidityColor;
			result.AdditionalLightsScattering.a *= saturate(1 - transmittance);
		}
	#endif



	#if KWS_USE_SPOT_LIGHTS
		UNITY_LOOP
		for (uint spotIdx = 0; spotIdx < KWS_SpotLightsCount; spotIdx++)
		{
			float3 scattering = 0;
			float transmittance = 1;
			KWS_LightData light = KWS_SpotLightsBuffer[spotIdx];
			result.AdditionalLightsSceneAttenuation = max(result.AdditionalLightsSceneAttenuation, SpotLightAttenuation(spotIdx, raymarchData.rayEnd));
			
			float3 step = raymarchData.step;
			float3 currentPos = raymarchData.currentPos;
			UNITY_LOOP
			for (uint i = 0; i < KWS_RayMarchSteps; ++i)
			{
				if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToSceneZ) break;
				if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToWaterZ) step = reflectedStep;

				float atten = SpotLightAttenuation(spotIdx, currentPos);
				IntegrateAdditionalLight(raymarchData, scattering, transmittance, atten, light.position, step, currentPos);
			}
			result.AdditionalLightsScattering.rgb += scattering * light.color * raymarchData.turbidityColor;
			result.AdditionalLightsScattering.a *= saturate(1 - transmittance);
		}
	#endif

	#if KWS_USE_SHADOW_SPOT_LIGHTS

		UNITY_LOOP
		for (uint shadowSpotIdx = 0; shadowSpotIdx < KWS_ShadowSpotLightsCount; shadowSpotIdx++)
		{
			float3 scattering = 0;
			float transmittance = 1;
			ShadowLightData light = KWS_ShadowSpotLightsBuffer[shadowSpotIdx];
			result.AdditionalLightsSceneAttenuation = max(result.AdditionalLightsSceneAttenuation, SpotLightAttenuationShadow(shadowSpotIdx, raymarchData.rayEnd));
			
			float3 step = raymarchData.step;
			float3 currentPos = raymarchData.currentPos;
			UNITY_LOOP
			for (uint i = 0; i < KWS_RayMarchSteps; ++i)
			{
				if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToSceneZ) break;
				if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToWaterZ) step = reflectedStep;

				float atten = SpotLightAttenuationShadow(shadowSpotIdx, currentPos);
				IntegrateAdditionalLight(raymarchData, scattering, transmittance, atten, light.position, step, currentPos);
			}
			result.AdditionalLightsScattering.rgb += scattering * light.color * raymarchData.turbidityColor;
			result.AdditionalLightsScattering.a *= saturate(1 - transmittance);
		}
	#endif
}