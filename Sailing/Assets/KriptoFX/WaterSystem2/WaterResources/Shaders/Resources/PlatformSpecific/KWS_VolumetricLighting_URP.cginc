
inline void RayMarchDirLight(RaymarchData raymarchData, inout RaymarchResult result)
{
	result.DirLightScattering = 0;
	result.DirLightSurfaceShadow = 1;
	result.DirLightSceneShadow = 1;
	result.SurfaceLight = 1;
	
	float3 finalScattering = 0;
	float transmittance = 1;

	Light light = GetMainLight();

	#ifdef _LIGHT_LAYERS
				//if (IsMatchingLightLayer(light.layerMask, KWS_WaterLightLayerMask)) //doesnt work on unity 6+ 
	#endif
	{
		float3 currentPos = raymarchData.rayStart;
		float3 turbidityColor = raymarchData.turbidityColor.xyz;
		float transparent = raymarchData.transparent;



		float rayLength = GetMaxRayDistanceRelativeToTransparent(transparent);
		float3 step = raymarchData.rayDir * rayLength / (float)KWS_RayMarchSteps;
		currentPos += step * raymarchData.offset;

		float sunAngleAttenuation = GetVolumeLightSunAngleAttenuation(light.direction.xyz);
		finalScattering = GetAmbientColor(GetExposure()) * 0.5;
		finalScattering *= GetVolumeLightInDepthTransmitance(raymarchData.waterHeight, currentPos.y, raymarchData.transparent);
		finalScattering *= sunAngleAttenuation;
		
		float3 reflectedStep = reflect(raymarchData.rayDir, float3(0, -1, 0)) * (raymarchData.rayLength / KWS_RayMarchSteps);
		
		UNITY_LOOP
		for (uint i = 0; i < KWS_RayMarchSteps; ++i)
		{
			if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToWaterZ) break;
			
			float atten = MainLightRealtimeShadow(TransformWorldToShadowCoord(currentPos));
			
			#if defined(KWS_USE_CAUSTIC) || defined(KWS_USE_ADDITIONAL_CAUSTIC)
				atten += atten * RaymarchCaustic(raymarchData, currentPos, light.direction);
			#endif
			atten *= sunAngleAttenuation;
			atten *= GetVolumeLightInDepthTransmitance(raymarchData.waterHeight, currentPos.y, raymarchData.transparent);
			
			IntegrateLightSlice(finalScattering, transmittance, atten, rayLength);
			currentPos += step ;
		}
		
		finalScattering *= light.color.xyz * turbidityColor;
		
		
		result.DirLightSurfaceShadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(raymarchData.rayStart));
		#if defined(KWS_USE_CAUSTIC) || defined(KWS_USE_ADDITIONAL_CAUSTIC)
			result.DirLightSceneShadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(raymarchData.rayEnd));
		#endif

		result.SurfaceLight = GetAmbientColor(GetExposure()) + light.color.xyz * result.DirLightSurfaceShadow;
	}
	
	result.DirLightScattering.rgb = finalScattering;
	result.DirLightScattering.a = saturate(1 - transmittance * (1 + KWS_VOLUME_LIGHT_TRANSMITANCE_NEAR_OFFSET_FACTOR));
}


inline void RayMarchAdditionalLights(RaymarchData raymarchData, inout RaymarchResult result)
{
	result.AdditionalLightsScattering = 0;
	result.AdditionalLightsSceneAttenuation = 0;

	InputData inputData = (InputData)0;
	inputData.normalizedScreenSpaceUV = raymarchData.uv;
	inputData.positionWS = raymarchData.currentPos;

	#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
		
		//#if USE_FORWARD_PLUS
		//	for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
		//	{
		//		float3 scattering = 0;
		//		float transmittance = 1;
		//		float3 currentPos = raymarchData.currentPos;
		//		result.AdditionalLightsSceneAttenuation = max(result.AdditionalLightsSceneAttenuation, GetAdditionalLight(lightIndex, raymarchData.rayEnd).distanceAttenuation);
		//		Light light;
		//		UNITY_LOOP
		//		for (uint i = 0; i < KWS_RayMarchSteps; ++i)
		//		{
		//			if(length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToSceneZ) break;
		//			light = GetAdditionalLight(lightIndex, currentPos, 1.0);
		//			IntegrateAdditionalLight(raymarchData, scattering, transmittance, light.distanceAttenuation * light.shadowAttenuation, light.direction, currentPos);
		//		    currentPos += step;
		//}
		//		result.AdditionalLightsScattering += scattering * light.color * raymarchData.turbidityColor;
		
		//	}
		//#endif
		
		//uint pixelLightCount = GetAdditionalLightsCount(); AdditionalLightsCount is set on per-object basis by unity's rendering code.
		//Its just gonna return you the number of lights that the last object rendered was affected by.
		uint pixelLightCount = KWS_GetAdditionalLightsCount();
		float3 reflectedStep = reflect(raymarchData.rayDir, float3(0, -1, 0)) * (raymarchData.rayLength / KWS_RayMarchSteps);

		LIGHT_LOOP_BEGIN(pixelLightCount)
		
		float3 scattering = 0;
		float transmittance = 1;
		float3 currentPos = raymarchData.currentPos;
		result.AdditionalLightsSceneAttenuation = max(result.AdditionalLightsSceneAttenuation, saturate(GetAdditionalPerObjectLight(lightIndex, raymarchData.rayEnd).distanceAttenuation));
		Light light;
		float3 step = raymarchData.step;

		UNITY_LOOP
		for (uint i = 0; i < KWS_RayMarchSteps; ++i)
		{
			if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToSceneZ) break;
			if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToWaterZ) step = reflectedStep;

			light = GetAdditionalPerObjectLight(lightIndex, currentPos);
			float atten = AdditionalLightRealtimeShadow(lightIndex, currentPos, light.direction) * saturate(light.distanceAttenuation);
			IntegrateAdditionalLight(raymarchData, scattering, transmittance, atten, light.direction, step, currentPos);
		}
		result.AdditionalLightsScattering.rgb += scattering * light.color * raymarchData.turbidityColor;
		result.AdditionalLightsScattering.a *= saturate(1 - transmittance);

		result.SurfaceLight += light.color.xyz * AdditionalLightRealtimeShadow(lightIndex, raymarchData.rayStart, light.direction) * saturate(light.distanceAttenuation);

		LIGHT_LOOP_END


	#endif
}