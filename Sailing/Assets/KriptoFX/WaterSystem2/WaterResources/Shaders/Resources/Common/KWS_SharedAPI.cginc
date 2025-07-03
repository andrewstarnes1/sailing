#ifndef KWS_SHARED_API_INCLUDED

#define KWS_SHARED_API_INCLUDED


#ifndef SHADERGRAPH_PREVIEW

	#ifndef KWS_WATER_PASS_HELPERS
		#include "../Common/KWS_WaterHelpers.cginc"

	#endif

#endif

float3 KWS_ParticlesPos; //particle system transform world position
float3 KWS_ParticlesScale; //particle system transform localScale


inline float3 TileWarpParticlesOffsetXZ(float3 vertex, float3 center)
{
	float3 halfScale = KWS_ParticlesScale * 0.5;
	float3 quadOffset = vertex.xyz - center.xyz;
	vertex.xz = frac((center.xz + halfScale.xz - KWS_ParticlesPos.xz) / KWS_ParticlesScale.xz) * KWS_ParticlesScale.xz; //aabb warp
	vertex.xz += KWS_ParticlesPos.xz + quadOffset.xz - halfScale.xz; //ofset relative to pivot and size
	return vertex;
}


inline float3 GetWaterSurfaceCollisionForQuadParticlesAquarium(float3 vertex, float3 center, float levelOffset)
{
	#ifdef SHADERGRAPH_PREVIEW
		return vertex;
	#else
		
		float waterLevel = KWS_ParticlesPos.y + levelOffset;
		float3 waterDisplacement = GetFftWavesDisplacement(vertex);
		//vertex.xyz += ComputeExtrudeMask(vertex);


		float3 quadOffset = vertex.xyz - center.xyz;
		float quadOffsetLength = length(quadOffset);

		float currentOffset = 0;
		float currentScale = 1;

		if (center.y > waterLevel - quadOffsetLength)
		{
			center.y = waterLevel + waterDisplacement.y - quadOffsetLength;
			vertex = center.xyz + quadOffset;
		}
		
		return vertex;
	#endif
}

inline float3 GetWaterSurfaceCollisionForQuadParticles(float3 vertex, float3 center)
{
	#ifdef SHADERGRAPH_PREVIEW
		return vertex;
	#else
		
		float4 screenPos = ComputeScreenPos(ObjectToClipPos(float4(vertex, 1)));
		float2 screenUV = screenPos.xy / screenPos.w;
		
		bool underwaterMask = GetUnderwaterMask(GetWaterMask(screenUV));
		
		if (!underwaterMask)
		{
			vertex = NAN_VALUE;
			return vertex;
		}
		//vertex.y += ComputeExtrudeMask(vertex).y * 0.5;

		
		float3 waterPos = KWS_WaterPosition;
		float3 waterDisplacement = GetFftWavesDisplacement(vertex);
		
		

		float3 quadOffset = vertex.xyz - center.xyz;
		float quadOffsetLength = length(quadOffset);
		
		if (center.y > waterPos.y - quadOffsetLength)
		{
			center.y = waterPos.y + waterDisplacement.y - quadOffsetLength;
			vertex = center.xyz + quadOffset;
		}
		
		return vertex;
	#endif
}

inline float4 GetUnderwaterColor(float2 uv, float3 albedoColor, float3 vertexWorldPos)
{
	#ifdef SHADERGRAPH_PREVIEW
		return 1;
	#else

		float2 underwaterUV = clamp(uv, 0.01, 0.99);
		
		//bool isUnderwater = GetUnderwaterMask(GetWaterMask(uv));
		//if(!isUnderwater) return half4(albedoColor, 1);
		//float3 waterPos = KWS_WaterPosition;
		//float3 waterDisplacement = GetFftWavesDisplacement(vertexWorldPos);
		//if(vertexWorldPos.y > waterPos.y + waterDisplacement.y) return float4(albedoColor, 1);
		

		float transparent = KWS_Transparent;
		float3 turbidityColor = KWS_TurbidityColor;
		float3 dyeColor = KWS_DyeColor;

		float distanceToVertex = GetWorldToCameraDistance(vertexWorldPos);
		float4 volLight = GetVolumetricLightWithAbsorbtionByDistance(uv, uv, transparent, turbidityColor, dyeColor, albedoColor, distanceToVertex, GetExposure(), 0);
		volLight.a = saturate(volLight.a * 1.5);
		return volLight;

	#endif
}

inline float4 GetUnderwaterColorAlbedo(float2 uv, float3 albedoColor, float3 vertexWorldPos)
{
	#ifdef SHADERGRAPH_PREVIEW
		return 1;
	#else

		float2 underwaterUV = clamp(uv, 0.01, 0.99);
		

		float transparent = KWS_Transparent;
		float3 turbidityColor = KWS_TurbidityColor;
		float3 dyeColor = KWS_DyeColor;

		float distanceToVertex = GetWorldToCameraDistance(vertexWorldPos);
		float3 volLight = GetVolumetricLightWithAbsorbtionByDistance(uv, uv, transparent, turbidityColor, dyeColor, 0, distanceToVertex, GetExposure(), albedoColor * 5).xyz;

		return half4(volLight, 1);

	#endif
}

inline float4 GetUnderwaterColorRefraction(float2 uv, float3 albedoColor, float2 refractionNormal, float3 vertexWorldPos)
{
	#ifdef SHADERGRAPH_PREVIEW
		return 1;
	#else
		float2 underwaterUV = clamp(uv + refractionNormal, 0.01, 0.99);
		
		half3 refraction = GetSceneColor(underwaterUV);
		
		float transparent = KWS_Transparent;
		float3 turbidityColor = KWS_TurbidityColor;
		float3 dyeColor = KWS_DyeColor;

		float distanceToVertex = GetWorldToCameraDistance(vertexWorldPos) + 2;
		
		float3 volLight = GetVolumetricLightWithAbsorbtionByDistance(uv, uv, transparent, turbidityColor, dyeColor, refraction, distanceToVertex, GetExposure(), albedoColor * 5).xyz;
		return half4(volLight, 1);
		
	#endif
}

inline float4 GetUnderwaterColorRefractionAquarium(float2 uv, float3 albedoColor, float2 refractionNormal)
{
	#ifdef SHADERGRAPH_PREVIEW
		return 1;
	#else
		float2 underwaterUV = clamp(uv + refractionNormal, 0.01, 0.99);
		half3 refraction = GetSceneColor(underwaterUV);
		
		refraction += albedoColor;
		return half4(refraction, 1);
		
	#endif
}


inline void GetWetnessData(float2 screenUV, float sceneDepth, out float3 diffuseColor, out float wetMap, out float occlusion, out float smoothness, out float metallic)
{
	diffuseColor = float3(0, 0, 0);
	wetMap = 0;
	occlusion = 0;
	smoothness = 0;
	metallic = 0;

	#ifdef SHADERGRAPH_PREVIEW
		
	#else

		float depth = sceneDepth;
		float3 worldPos = GetWorldSpacePositionFromDepth(screenUV, depth);
		float waterLevel =  KWS_WaterPosition.y;
		float waterLevelMaxLevelFade = KWS_Pow2(KWS_WindSpeed / KWS_MAX_WIND_SPEED) * 20 + 1;
	
		float zoneFadeAdditive = 0;
		float colorOverrideStrength = 0;
		float reflectionStrength = 1.0;
	    float underwaterFade = 1.0;


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
						if (zone.overrideHeight > 0.5 && zone.clipWaterBelowZone)
						{
							float2 distanceToBox = abs(mul((worldPos.xz - zone.center.xz), zone.rotationMatrix)) / zone.halfSize.xz;
							float distanceToBorder = max(distanceToBox.x, distanceToBox.y);
							float zoneMinHeight = zone.center.y - zone.halfSize.y;
							if (distanceToBorder < 1.1 && worldPos.y < zoneMinHeight && worldPos.y > KWS_WaterPosition.y) discard;
						}
						
						if (zone.overrideHeight > 0.5)
						{
							float currentHeightOffset = zone.center.y + zone.halfSize.y - KWS_WaterPosition.y;
							float heightFade = GetLocalWaterZoneSphereBlendFactor(zone.uv, zone.heightEdgeBlending);
							maxHeightOffset = max(maxHeightOffset, currentHeightOffset);
							offsetBlending = lerp(heightFade, 1, KWS_Pow20(zone.heightEdgeBlending));
								
						}
							

					}
				}
				waterLevel += lerp(0, maxHeightOffset, offsetBlending);
					
			}
		#endif

		
		if (worldPos.y < waterLevel + waterLevelMaxLevelFade) wetMap = clamp(waterLevel - worldPos.y + waterLevelMaxLevelFade, 0, 1) * 1;
		
	
	
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
						float4 dynamicWaves = GetDynamicWavesZone(zone.id, zone.uv);
						float4 dynamicWavesAdditionalData = GetDynamicWavesZoneAdditionalDataBicubic(zone.id, zone.uv); //(wetmap, shoreline mask, foam mask, wetDepth)
						float zoneFade = GetDynamicWavesBorderFading(zone.uv);
						
						float worldHeight = DecodeDynamicWavesHeight(zone.id, dynamicWavesAdditionalData.w);
						float maxWetLevel = 2;
						float currentHeight = dynamicWaves.z + worldHeight;
						float wetFadeByHeight = 1 - saturate(worldPos.y - currentHeight - maxWetLevel);

						underwaterFade *= saturate(worldPos.y - currentHeight + 1);
						
						zoneFadeAdditive = saturate(zoneFadeAdditive + zoneFade);
						wetMap = max(wetMap, dynamicWavesAdditionalData.x * wetFadeByHeight);
						
						#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
							float4 zoneColorData = GetDynamicWavesZoneColorData(zone.id, zone.uv);
							zoneColorData.rgb = lerp(zoneColorData.rgb, zoneColorData.rgb * 0.35, saturate(zoneColorData.a * zoneColorData.a + zoneColorData.a * 2));
							zoneColorData.a = saturate(zoneColorData.a * 2);
							zoneColorData.a *= 1 - saturate((KWS_WaterPosition.y - 1 - worldPos.y) / (DYNAMIC_WAVE_COLOR_MAX_TRANSPARENT * 2));

							colorOverrideStrength = max(colorOverrideStrength, zoneColorData.a * 2);

							diffuseColor.rgb = lerp(diffuseColor.rgb, zoneColorData.rgb, zoneColorData.a);
						#endif
					}
				}
			}

	
		#endif

		

		#if defined(KWS_URP) || defined(KWS_HDRP)
			reflectionStrength *= lerp(0.4, 0.85, underwaterFade);
		
		#endif
		
		if (wetMap < 0.001) discard;
	
		occlusion = saturate(wetMap * 0.7 * KWS_WetStrength);
		smoothness = saturate(wetMap * reflectionStrength * (1 - KWS_Pow2(1 - KWS_WetStrength)));
		metallic = saturate(wetMap * 0.65) + colorOverrideStrength;

	#endif
}



////////////////////////////// shadergraph support /////////////////////////////////////////////////////////////////////

inline void GetDecalVertexOffset_float(float3 worldPos, float displacement, out float3 result)
{
	#ifdef SHADERGRAPH_PREVIEW
		result = 0;
	#else
		//float3 extrudeOffset = ComputeExtrudeMask(GetAbsolutePositionWS(worldPos)) * 0.5;
		result = worldPos + GetFftWavesDisplacement(GetCameraAbsolutePosition(worldPos)) * saturate(float3(displacement, 1, displacement)) + 1;
	#endif
}



inline void GetDecalDepthTest_float(float4 screenPos, out float result)
{
	#ifdef SHADERGRAPH_PREVIEW
		result = 1;
	#else
		float sceneDepth = GetSceneDepth(screenPos.xy / screenPos.w);
		result = LinearEyeDepthUniversal(sceneDepth) > LinearEyeDepthUniversal(screenPos.z / screenPos.w);
	#endif
}


inline void TileWarpParticlesOffsetXZ_float(float3 vertex, float3 center, out float3 result)
{
	result = TileWarpParticlesOffsetXZ(vertex, center);
}


inline void GetWaterSurfaceCollisionForQuadParticles_float(float3 vertex, float3 center, out float3 result)
{
	result = GetWaterSurfaceCollisionForQuadParticles(vertex, center);
}

inline void GetWaterSurfaceCollisionForQuadParticlesAquarium_float(float3 vertex, float3 center, float levelOffset, out float3 result)
{
	result = GetWaterSurfaceCollisionForQuadParticlesAquarium(vertex, center, levelOffset);
}

void GetUnderwaterColorRefraction_float(float2 uv, float3 albedoColor, float2 refractionNormal, float3 worldPos, out float4 result) //shadergraph function

{
	result = GetUnderwaterColorRefraction(uv, albedoColor, refractionNormal, worldPos);
}

void GetUnderwaterColorRefractionAquarium_float(float2 uv, float3 albedoColor, float2 refractionNormal, out float4 result) //shadergraph function

{
	result = GetUnderwaterColorRefractionAquarium(uv, albedoColor, refractionNormal);
}


void GetUnderwaterColorAlbedo_float(float2 uv, float3 albedoColor, float3 worldPos, out float4 result) //shadergraph function

{
	result = GetUnderwaterColorAlbedo(uv, albedoColor, worldPos);
}

void GetUnderwaterColor_float(float2 uv, float3 albedoColor, float3 worldPos, out float4 result) //shadergraph function

{
	result = GetUnderwaterColor(uv, albedoColor, worldPos);
}

void GetDynamicWavesFoamParticlesVertexPosition_float(uint instanceID, uint vertexID, out float3 vertex, out float2 uv) //shadergraph function

{
	#ifdef SHADERGRAPH_PREVIEW
		vertex = 1;
		uv = 1;
	#else

		vertex = 1;
		uv = 1;
	#endif
}



void GetWetnessData_float(float2 screenUV, float sceneDepth, out float3 diffuseColor, out float wetMap, out float occlusion, out float smoothness, out float metallic) //shadergraph function

{
	#ifdef SHADERGRAPH_PREVIEW
		diffuseColor = float3(0, 0, 0);
		wetMap = 1;
		occlusion = 0;
		smoothness = 0;
		metallic = 0;
	#else
		GetWetnessData(screenUV, sceneDepth, diffuseColor, wetMap, occlusion, smoothness, metallic);
	#endif
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////




#endif