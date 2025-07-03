void RayMarchDirLight(RaymarchData raymarchData, inout RaymarchResult result)
{
	result.DirLightScattering = 0;
	result.DirLightSurfaceShadow = 1;
	result.DirLightSceneShadow = 1;

	float exposure = GetExposure();
	float3 finalScattering = 0;
	float3 currentPos = raymarchData.rayStart;
	float3 turbidityColor = raymarchData.turbidityColor.xyz;
	float transparent = raymarchData.transparent;



	float rayLength = GetMaxRayDistanceRelativeToTransparent(transparent);
	float3 step = raymarchData.rayDir * rayLength / (float)KWS_RayMarchSteps;
	float3 initialStartPos = currentPos + step * raymarchData.offset;

	finalScattering = GetAmbientColor(exposure) * 0.5;
	finalScattering *= GetVolumeLightInDepthTransmitance(raymarchData.waterHeight, currentPos.y, raymarchData.transparent);

	for (uint lightIdx = 0; lightIdx < _DirectionalLightCount; ++lightIdx)
	{
		DirectionalLightData light = _DirectionalLightDatas[lightIdx];
		float3 L = -light.forward;
		float sunAngleAttenuation = GetVolumeLightSunAngleAttenuation(L);

		float transmittance = 1;
		float3 currentPos = initialStartPos;

		LightLoopContext context;
		context.shadowContext = InitShadowContext();
		PositionInputs posInput;
		posInput.positionWS = GetCameraRelativePositionWS(currentPos);
		
		float4 lightColor = EvaluateLight_Directional(context, posInput, light);
		lightColor.a *= light.volumetricLightDimmer;
		lightColor.rgb = saturate(lightColor.rgb * exposure * lightColor.a); // Composite
		
	
		finalScattering *= sunAngleAttenuation;

		//float3 step = raymarchData.step;
		
		float3 color; float attenuation;
		if (light.volumetricLightDimmer > 0)
		{
			for (uint j = 0; j < MAX_VOLUMETRIC_LIGHT_ITERATIONS; ++j)
			{
				if (j >= KWS_RayMarchSteps) break;
				if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToWaterZ) break;

				posInput.positionWS = GetCameraRelativePositionWS(currentPos);
				
				float3 atten = 1;
				if (_DirectionalShadowIndex >= 0 && (uint)_DirectionalShadowIndex == lightIdx && (light.volumetricLightDimmer > 0) && (light.volumetricShadowDimmer > 0))
				{
					atten *= GetDirectionalShadowAttenuation(context.shadowContext, raymarchData.uv, GetCameraRelativePositionWS(currentPos), 0, light.shadowIndex, L);
					atten = lerp(float3(1, 1, 1), atten, light.volumetricShadowDimmer);
				}
				//lightColor.rgb *= ComputeShadowColor(atten, light.shadowTint, light.penumbraTint); //why always black?
				
				#if defined(KWS_USE_CAUSTIC) || defined(KWS_USE_ADDITIONAL_CAUSTIC)
					atten += atten * RaymarchCaustic(raymarchData, currentPos, light.forward);
				#endif
				atten *= sunAngleAttenuation;
				atten *= GetVolumeLightInDepthTransmitance(raymarchData.waterHeight, currentPos.y, raymarchData.transparent);
				
				IntegrateLightSlice(finalScattering, transmittance, atten, rayLength);
				currentPos += step;
			}

			
			result.DirLightScattering.rgb += finalScattering * lightColor.rgb * turbidityColor;
			result.DirLightScattering.a = saturate(1 - transmittance * (1 + KWS_VOLUME_LIGHT_TRANSMITANCE_NEAR_OFFSET_FACTOR));
		}
		
		if (_DirectionalShadowIndex >= 0 && (uint)_DirectionalShadowIndex == lightIdx)
		{
			result.DirLightSurfaceShadow = GetDirectionalShadowAttenuation(context.shadowContext, raymarchData.uv, GetCameraRelativePositionWS(raymarchData.rayStart), 0, light.shadowIndex, L);
			#if defined(KWS_USE_CAUSTIC) || defined(KWS_USE_ADDITIONAL_CAUSTIC)
				result.DirLightSceneShadow = GetDirectionalShadowAttenuation(context.shadowContext, raymarchData.uv, GetCameraRelativePositionWS(raymarchData.rayEnd), 0, light.shadowIndex, L);
			#endif
		}
	}
}

bool lightFoundInCluster(uint lightIdx, uint tileOffset)
{
	uint count = g_vLightListTile[LIGHT_DWORD_PER_FPTL_TILE * tileOffset +0] & 0xffff;

	for (uint i = 0; i < count; ++i)
	{
		uint word = g_vLightListTile[LIGHT_DWORD_PER_FPTL_TILE * tileOffset +1 + i / 2];
		uint idx = (i % 2 == 0) ? (word & 0xffff) : (word >> 16);

		if (idx == lightIdx) return true;
	}

	return false;
}

void RayMarchAdditionalLights(RaymarchData raymarchData, inout RaymarchResult result)
{
	result.AdditionalLightsScattering = 0;
	result.AdditionalLightsSceneAttenuation = 0;

	
	LightLoopContext context;
	context.shadowContext = InitShadowContext();
	
	float exposure = GetExposure();
	float3 reflectedStep = reflect(raymarchData.rayDir, float3(0, -1, 0)) * (raymarchData.rayLength / KWS_RayMarchSteps);

	float iterrations = 0;
	uint lightIdx = 0;

	while(lightIdx < _PunctualLightCount)
	{
		//looks like tile rendering is slower than just the simple loop through all lights 

		//float3 currentPos = raymarchData.currentPos;
		//bool isUsed = true;
		//for (uint i = 0; i < KWS_RayMarchSteps; ++i)
		//{
		//	uint lightCount, lightStart;
		//	float distToEndPoint = length(currentPos - raymarchData.rayStart);
		//	//if (distToEndPoint > raymarchData.rayLengthToSceneZ || distToEndPoint > raymarchData.rayLengthToWaterZ) break;

		//	PositionInputs posInput;
		//	posInput.positionWS = currentPos;
		//	posInput.tileCoord = raymarchData.uv * _ScreenSize.xy / GetTileSize();
		//	posInput.linearDepth = distToEndPoint * i;
		//	GetCountAndStart(posInput, LIGHTCATEGORY_PUNCTUAL, lightStart, lightCount);

		//	uint v_lightIdx = lightStart;
		//	uint v_lightListOffset = 0;
		//	while(v_lightListOffset < lightCount)
		//	{
		//		v_lightIdx = FetchIndex(lightStart, v_lightListOffset);
		//		uint s_lightIdx = ScalarizeElementIndex(v_lightIdx, false);
		//		if (s_lightIdx == -1)
		//			break;

		
		//		if (v_lightIdx == lightIdx)
		//		{
		//			isUsed = true;
		//			break;
		//		}
		//		v_lightListOffset++;

		//	}
		//	currentPos += raymarchData.step;
		//}

		
		LightData light = FetchLight(lightIdx);
		
		//if (IsMatchingLightLayer(light.lightLayers, KWS_WaterLightLayerMask)  //check why it doesnt work
		
		float3 currentPos = raymarchData.currentPos;
		float3 L;
		float4 distances; // {d, d^2, 1/d, d_proj}
		
		float3 scattering = 0;
		float transmittance = 1;
		
		float4 lightColor = float4(light.color, 1.0);
		lightColor.a *= light.volumetricLightDimmer;
		lightColor.rgb *= lightColor.a * exposure;

		GetPunctualLightVectors(GetCameraRelativePositionWS(raymarchData.rayEnd), light, L, distances);
		float surfaceAtten = PunctualLightAttenuation(distances, light.rangeAttenuationScale, light.rangeAttenuationBias, light.angleScale, light.angleOffset);
		surfaceAtten *= light.volumetricLightDimmer * exposure;
		result.AdditionalLightsSceneAttenuation = max(result.AdditionalLightsSceneAttenuation, saturate(surfaceAtten * 100));
		
		
		for (uint i = 0; i < KWS_RayMarchSteps; ++i)
		{
			float distToEndPoint = length(currentPos - raymarchData.rayStart);
			if (distToEndPoint > raymarchData.rayLengthToSceneZ || distToEndPoint > raymarchData.rayLengthToWaterZ) break;
			//if (length(currentPos - raymarchData.rayStart) > raymarchData.rayLengthToWaterZ) step = reflectedStep;
			
			GetPunctualLightVectors(GetCameraRelativePositionWS(currentPos), light, L, distances);
			float atten = PunctualLightAttenuation(distances, light.rangeAttenuationScale, light.rangeAttenuationBias, light.angleScale, light.angleOffset);
			if (distances.x < light.range && atten > 0.0 && L.y > 0.0 && light.shadowIndex >= 0 && light.shadowDimmer > 0)
			{
				atten *= GetPunctualShadowAttenuation(context.shadowContext, raymarchData.uv, GetCameraRelativePositionWS(currentPos), 0, light.shadowIndex, L, distances.x, light.lightType == GPULIGHTTYPE_POINT, light.lightType != GPULIGHTTYPE_PROJECTOR_BOX);
			}

			IntegrateAdditionalLight(raymarchData, scattering, transmittance, atten, light.positionRWS, raymarchData.step, currentPos);
		}
		result.AdditionalLightsScattering.rgb += scattering * lightColor.rgb * raymarchData.turbidityColor;
		result.AdditionalLightsScattering.a *= saturate(1 - transmittance);

		
		lightIdx++;
		
	}
	

}