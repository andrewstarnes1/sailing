#ifndef KWS_WATER_VERT_PASS
#define KWS_WATER_VERT_PASS

struct waterInput
{
	float4 vertex : POSITION;
	float surfaceMask : COLOR0;
	float3 normal : NORMAL;
	#if defined(KWS_USE_WATER_INSTANCING)
		float2 uvData : TEXCOORD0;
	#endif
	uint instanceID : SV_InstanceID;
};

struct v2fDepth
{
	float4 pos : SV_POSITION;
	float3 worldNormal : NORMAL;
	float3 worldPos : TEXCOORD0;
	float3 worldPosRefracted : TEXCOORD1;
	float surfaceMask : COLOR0;
	float windAttenuation :  COLOR1;
	float4 screenPos : TEXCOORD2;
	float2 localHeightAndTensionMask : TEXCOORD32;
	UNITY_VERTEX_OUTPUT_STEREO
};

struct v2fWater
{
	float4 pos : SV_POSITION;
	float3 worldNormal : NORMAL;
	float surfaceMask : COLOR0;
	float windAttenuation :  COLOR1;
	float3 worldPos : TEXCOORD0;
	float3 worldPosRefracted : TEXCOORD1;
	float4 screenPos : TEXCOORD2;

	UNITY_VERTEX_OUTPUT_STEREO
};

struct WaterOffsetData
{
	float3 offset;
	float3 oceanOffset;
	float foamMask;
	float2 flowDirection;
	float orthoDepth;
	float windAttenuation;
	float surfaceMask;
	bool isRequireDiscardTriangle;
};


WaterOffsetData ComputeWaterOffset(float3 worldPos)
{
	WaterOffsetData data = (WaterOffsetData)0;
	data.windAttenuation = 1;
	data.orthoDepth = -100000;
	data.surfaceMask = 1;
	
	float waterLevel = 0;
	float terrainLevel = 0;
	
	float shorelineMask = 1;
	

	float3 disp = 0;
	#if defined(KWS_USE_LOCAL_WATER_ZONES)
		disp = GetFftWavesDisplacementWithAttenuation(worldPos, data.windAttenuation);
	#else
		disp = GetFftWavesDisplacement(worldPos);
	#endif

	data.oceanOffset = disp;

	#if defined(KWS_USE_DYNAMIC_WAVES) || defined(KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE)
		bool isOutDistance = GetWorldToCameraDistance(worldPos) > KW_WaterFarDistance * 0.5 - Test4.x;
		if (!isOutDistance)
		{
			#endif
	
			#if defined(KWS_USE_DYNAMIC_WAVES)
		
			uint zoneIndexOffset = 0;
			uint zoneIndexCount = 0;
			if (GetTileRange(worldPos, zoneIndexOffset, zoneIndexCount))
			{
				for (uint zoneIndex = zoneIndexOffset; zoneIndex < zoneIndexCount; zoneIndex++)
				{
					ZoneData zone = (ZoneData)0;
					if (GetWaterZone(worldPos, zoneIndex, zone))
					{
						float4 dynamicWaves = GetDynamicWavesZoneBicubic(zone.id, zone.uv);
						float4 dynamicWavesAdditionalData = GetDynamicWavesZoneAdditionalData(zone.id, zone.uv);
						float zoneFade = GetDynamicWavesBorderFading(zone.uv);
						float zoneDepthMask = GetDynamicWavesZoneDepthMask(zone.id, zone.uv);
					
						waterLevel = max(waterLevel, dynamicWaves.z * zoneFade);
						terrainLevel = max(terrainLevel, dynamicWaves.w);
						data.flowDirection = data.flowDirection + NormalizeDynamicWavesVelocity(dynamicWaves.xy);
						shorelineMask = (shorelineMask * dynamicWavesAdditionalData.y);
						data.foamMask = max(data.foamMask, dynamicWavesAdditionalData.z * zoneFade);
						data.orthoDepth = max(data.orthoDepth, zoneDepthMask > 0.0 ?  zoneDepthMask : - 100000);
					//	if ((waterLevel + terrainLevel) <= 0.0001 && shorelineMask <= 0.0001) data.isRequireDiscardTriangle = true;
						//data.surfaceMask = terrainLevel > waterLevel ? 0 : 1;
					}	
				}
			}
		
			#endif

			#if defined(KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE)
			float2 movableZoneUV = 0;
			if (GetWaterZoneMovable(worldPos, movableZoneUV))
			{
				float4 dynamicWaves = GetDynamicWavesZoneMovable(movableZoneUV);
				float4 dynamicWavesAdditionalData = GetDynamicWavesZoneAdditionalDataMovable(movableZoneUV); //(wetmap, shoreline mask, foam mask, wetDepth)
				float zoneFade = GetDynamicWavesBorderFading(movableZoneUV);
				float zoneDepthMask = GetDynamicWavesZoneDepthMaskMovable(movableZoneUV);
			
				waterLevel = max(waterLevel, dynamicWaves.z * zoneFade);
				terrainLevel = max(terrainLevel, dynamicWaves.w);
				data.flowDirection = data.flowDirection + NormalizeDynamicWavesVelocity(dynamicWaves.xy);
				shorelineMask = (shorelineMask * dynamicWavesAdditionalData.y);
				data.foamMask = max(data.foamMask, dynamicWavesAdditionalData.z * zoneFade);
				data.orthoDepth = max(data.orthoDepth, zoneDepthMask > 0.0 ?  zoneDepthMask : - 100000);
			}
			#endif

			#if defined(KWS_USE_DYNAMIC_WAVES) || defined(KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE)
		
			//waterLevel = lerp(waterLevel, waterLevel * 0.1, saturate(pow(waterLevel * 0.3, 0.1) * (pow(1 - shorelineMask, 15))));
			disp *= shorelineMask;
			disp.y += waterLevel + terrainLevel;
			//disp.xz += data.flowDirection * max(0.5, waterLevel) * KWS_DYNAMIC_WAVES_MAX_XZ_OFFSET * lerp(7, 2, 1-KWS_Pow2(1-shorelineMask));
			disp.xz += data.flowDirection * lerp(1, 3, saturate(waterLevel * waterLevel)) * KWS_DYNAMIC_WAVES_MAX_XZ_OFFSET;

		
		}
	#endif

	
	#if defined(KWS_USE_LOCAL_WATER_ZONES)
	
	uint zoneIndexOffset_local = 0;
	uint zoneIndexCount_local = 0;
	bool isLocalWaterZone = GetTileRange_LocalZone(worldPos, zoneIndexOffset_local, zoneIndexCount_local);

	if (isLocalWaterZone)
	{
		float offsetBlending = 0;
		float maxHeightOffset = -100000;
		for (uint zoneIndex = zoneIndexOffset_local; zoneIndex < zoneIndexCount_local; zoneIndex++)
		{
			LocalZoneData zone = (LocalZoneData)0;
			if (GetWaterZone_LocalZone(worldPos, zoneIndex, zone))
			{
				if (zone.overrideWindSettings > 0.5)
				{
					float zoneFade = GetLocalWaterZoneSphereBlendFactor(zone.uv, saturate(zone.windEdgeBlending + 0.25));
					data.windAttenuation = lerp(lerp(data.windAttenuation, zone.windStrengthMultiplier, zoneFade), zone.windStrengthMultiplier, KWS_Pow10(zone.windEdgeBlending));
						
				}

				if (zone.overrideHeight > 0.5)
				{
					float currentHeightOffset = zone.center.y + zone.halfSize.y - KWS_WaterPosition.y;
					float heightFade = GetLocalWaterZoneSphereBlendFactor(zone.uv, zone.heightEdgeBlending);
					maxHeightOffset = max(maxHeightOffset, currentHeightOffset);
					offsetBlending = lerp(heightFade, 1, KWS_Pow20(zone.heightEdgeBlending));
					data.surfaceMask = 0;
				}
					

			}
		}
		disp.y = lerp(disp.y, maxHeightOffset, offsetBlending);
			
	}
	#endif

	data.offset += disp;

	data.offset.y += KWS_OceanLevelHeightOffset;

	return data;
}


v2fDepth vertDepth(waterInput v)
{
	v2fDepth o = (v2fDepth)0;
	
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	KWS_INITIALIZE_DEFAULT_MATRIXES;

	#if defined(KWS_USE_WATER_INSTANCING) && !defined(USE_WATER_TESSELATION)
		UpdateInstanceData(v.instanceID, v.uvData, v.vertex, KWS_MATRIX_M, KWS_MATRIX_I_M);
	#endif

	o.worldPos.xyz = LocalToWorldPos(v.vertex.xyz, KWS_MATRIX_M);

	WaterOffsetData offsetData = ComputeWaterOffset(o.worldPos.xyz);
	float3 offset = WorldToLocalPosWithoutTranslation(offsetData.offset, KWS_MATRIX_I_M);

	// if (offsetData.isRequireDiscardTriangle)
	// {
	// 	o.pos.w = NAN_VALUE;
	// 	return o;
	// }
	v.vertex.xyz += offset;

	o.surfaceMask = v.surfaceMask* offsetData.surfaceMask;
	o.windAttenuation = offsetData.windAttenuation;

	o.worldPosRefracted.xyz = LocalToWorldPos(v.vertex.xyz, KWS_MATRIX_M);
	o.localHeightAndTensionMask.x = offset.y;
	o.localHeightAndTensionMask.y = v.vertex.y - offset.y;

	o.pos = ObjectToClipPos(v.vertex, KWS_MATRIX_M, KWS_MATRIX_I_M);
	o.screenPos = ComputeScreenPos(o.pos);
	o.worldNormal = GetWorldSpaceNormal(v.normal, KWS_MATRIX_M);
	
	return o;
}

v2fWater vertWater(waterInput v)
{
	v2fWater o = (v2fWater)0;

	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	KWS_INITIALIZE_DEFAULT_MATRIXES;

	#if defined(KWS_USE_WATER_INSTANCING) && !defined(USE_WATER_TESSELATION)
		UpdateInstanceData(v.instanceID, v.uvData, v.vertex, KWS_MATRIX_M, KWS_MATRIX_I_M);
	#endif
	o.worldPos = LocalToWorldPos(v.vertex.xyz, KWS_MATRIX_M);
	
	WaterOffsetData offsetData = ComputeWaterOffset(o.worldPos.xyz);
	float3 offset = WorldToLocalPosWithoutTranslation(offsetData.offset, KWS_MATRIX_I_M);
	//
	// if (offsetData.isRequireDiscardTriangle)
	// {
	// 	o.pos.w = NAN_VALUE;
	// 	return o;
	// }
	v.vertex.xyz += offset;

	o.surfaceMask = v.surfaceMask * offsetData.surfaceMask;
	o.windAttenuation = offsetData.windAttenuation;

	o.worldPosRefracted = LocalToWorldPos(v.vertex.xyz, KWS_MATRIX_M);
	o.pos = ObjectToClipPos(v.vertex, KWS_MATRIX_M, KWS_MATRIX_I_M);
	o.screenPos = ComputeScreenPos(o.pos);
	o.worldNormal = GetWorldSpaceNormal(v.normal, KWS_MATRIX_M);
	return o;
}
#endif