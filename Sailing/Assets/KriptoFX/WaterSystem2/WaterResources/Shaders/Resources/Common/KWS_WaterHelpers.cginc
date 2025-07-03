#ifndef KWS_WATER_PASS_HELPERS
#define KWS_WATER_PASS_HELPERS

#ifndef KWS_WATER_VARIABLES
	#include "KWS_WaterVariables.cginc"
#endif

#ifndef KWS_COMMON_HELPERS
	#include "../Common/KWS_CommonHelpers.cginc"
#endif


#ifdef KWS_BUILTIN
	#ifndef KWS_PLATFORM_SPECIFIC_HELPERS
		#include "../PlatformSpecific/KWS_PlatformSpecificHelpers_Builtin.cginc"
	#endif
#endif

#ifdef KWS_URP
	#ifndef KWS_PLATFORM_SPECIFIC_HELPERS
		#include "../PlatformSpecific/KWS_PlatformSpecificHelpers_URP.cginc"
	#endif
#endif

#ifdef KWS_HDRP
	#ifndef KWS_PLATFORM_SPECIFIC_HELPERS
		#include "../PlatformSpecific/KWS_PlatformSpecificHelpers_HDRP.cginc"
	#endif
#endif



float CalcMipLevel(float2 uv)
{
	float2 dx = ddx(uv);
	float2 dy = ddy(uv);
	float delta = max(dot(dx, dx), dot(dy, dy));
	return max(0.0, 0.5 * log2(delta));
}


//////////////////////////////////////////////    FFT_Waves_Pass    //////////////////////////////////////////////
#define MAX_FFT_WAVES_MAX_CASCADES 4

float KWS_WavesDomainSizes[MAX_FFT_WAVES_MAX_CASCADES];
float KWS_WavesDomainVisiableArea[MAX_FFT_WAVES_MAX_CASCADES];
float KWS_WindSpeed;
float KWS_WavesAreaScale;
float KWS_WavesCascades;

Texture2DArray KWS_FftWavesDisplace;
Texture2DArray KWS_FftWavesNormal;
SamplerState sampler_KWS_FftWavesNormal;
float4 KWS_FftWavesDisplace_TexelSize;
float4 KWS_FftWavesNormal_TexelSize;


inline float GetDomainSize(uint idx)
{
	return KWS_WavesDomainSizes[idx] * KWS_WavesAreaScale;
}

inline float GetDomainSize(uint idx, uint waterID)
{
	return KWS_WavesDomainSizes[idx] * KWS_WavesAreaScale;
}

inline float GetDomainVisibleArea(uint idx)
{
	return KWS_WavesDomainVisiableArea[idx] * KWS_WavesAreaScale;
}

float3 GetFftWavesDisplacementSlice(float3 worldPos, uint slice)
{
	worldPos += KWS_WaterWorldPosOffset;
	return KWS_FftWavesDisplace.SampleLevel(sampler_linear_repeat, float3(worldPos.xz / GetDomainSize(slice), slice), 0).xyz * KWS_WavesAreaScale;
}

float3 GetFftWavesDisplacementSliceBicubic(float3 worldPos, uint slice)
{
	worldPos += KWS_WaterWorldPosOffset;
	return Texture2DArraySampleLevelBicubic(KWS_FftWavesDisplace, sampler_linear_repeat, worldPos.xz / GetDomainSize(slice), KWS_FftWavesDisplace_TexelSize, slice, 0).xyz;
}

inline float GetFftFade(float distanceToCamera, int lodIdx, float farDistanceMinFade = 0.0)
{
	if (lodIdx == KWS_WavesCascades - 1)
	{
		float farDist = max(500, KW_WaterFarDistance * 0.5);
		return saturate(1.0 + farDistanceMinFade - saturate(distanceToCamera / farDist));
	}
	else
	{
		float fadeLod = saturate(distanceToCamera / GetDomainVisibleArea(lodIdx));
		fadeLod = 1 - fadeLod * fadeLod * fadeLod;
		return fadeLod;
	}
}

float3 GetFftWavesDisplacementLast(float3 worldPos)
{
	worldPos += KWS_WaterWorldPosOffset;
	int lastCascadeIdx = max(0, KWS_WavesCascades - 1);
	return KWS_FftWavesDisplace.SampleLevel(sampler_linear_repeat, float3(worldPos.xz / GetDomainSize(lastCascadeIdx), lastCascadeIdx), 0).xyz * KWS_WavesAreaScale;
}

float3 GetFftWavesDisplacementDetailsHQ(float3 worldPos)
{
	worldPos += KWS_WaterWorldPosOffset;
	float3 disp = Texture2DArraySampleLevelBicubic(KWS_FftWavesDisplace, sampler_linear_repeat, worldPos.xz / GetDomainSize(0), KWS_FftWavesDisplace_TexelSize, 0, 0).xyz;
	if (KWS_WavesCascades > 1) disp += Texture2DArraySampleLevelBicubic(KWS_FftWavesDisplace, sampler_linear_repeat, worldPos.xz / GetDomainSize(1), KWS_FftWavesDisplace_TexelSize, 1, 0).xyz;
	return disp * KWS_WavesAreaScale;
}

float3 GetFftWavesDisplacementDetails(float3 worldPos)
{
	worldPos += KWS_WaterWorldPosOffset;
	float3 disp = KWS_FftWavesDisplace.SampleLevel(sampler_linear_repeat, float3(worldPos.xz / GetDomainSize(0), 0), 0).xyz;
	if (KWS_WavesCascades > 1) disp += KWS_FftWavesDisplace.SampleLevel(sampler_linear_repeat, float3(worldPos.xz / GetDomainSize(1), 1), 0).xyz;
	return disp * KWS_WavesAreaScale;
}

float3 GetFftWavesDisplacementBuoyancy(float3 worldPos)
{
	worldPos += KWS_WaterWorldPosOffset;

	float3 finalData = 0;
	for (int idx = KWS_WavesCascades - 1; idx > 0; idx--)
	{
		finalData += KWS_FftWavesDisplace.SampleLevel(sampler_linear_repeat, float3(worldPos.xz / GetDomainSize(idx), idx), 0).xyz;
	}
	return finalData * KWS_WavesAreaScale;
}

float3 GetFftWavesDisplacementDynamicWaves(float3 worldPos)
{
	worldPos += KWS_WaterWorldPosOffset;
	int maxCascadeIdx = KWS_WavesCascades - 1;

	float3 finalData = 0;
	for (int idx = maxCascadeIdx; idx > 1; idx--)
	{
		finalData += KWS_FftWavesDisplace.SampleLevel(sampler_linear_repeat, float3(worldPos.xz / GetDomainSize(idx), idx), 0).xyz * saturate((idx + 0.25) / maxCascadeIdx);
	}
	return finalData * KWS_WavesAreaScale;
}

float3 GetFftWavesDisplacement(float3 worldPos)
{
	float distanceToCamera = GetWorldToCameraDistance(worldPos);
	float3 finalData = 0;
	worldPos += KWS_WaterWorldPosOffset;

	UNITY_LOOP for (int idx = KWS_WavesCascades - 1; idx > 0; idx--)
	{
		float fade = GetFftFade(distanceToCamera, idx);
		if (fade < 0.01) continue;

		finalData += fade * KWS_FftWavesDisplace.SampleLevel(sampler_linear_repeat, float3(worldPos.xz / GetDomainSize(idx), idx), 0).xyz;
	}
	
	return finalData * KWS_WavesAreaScale;
}

float3 GetFftWavesDisplacementWithAttenuation(float3 worldPos, float attenuation)
{
	float distanceToCamera = GetWorldToCameraDistance(worldPos);
	float3 finalData = 0;
	worldPos += KWS_WaterWorldPosOffset;

	UNITY_LOOP for (int idx = KWS_WavesCascades - 1; idx > 0; idx--)
	{
		float fade = GetFftFade(distanceToCamera, idx);
		if (fade < 0.01) continue;

		float windAttenuation = lerp(attenuation * saturate(1.2 - 0.2 * idx), 1, attenuation);
		
		finalData += windAttenuation * fade * KWS_FftWavesDisplace.SampleLevel(sampler_linear_repeat, float3(worldPos.xz / GetDomainSize(idx), idx), 0).xyz;
	}
	
	return finalData * KWS_WavesAreaScale;
}


float3 GetFftWavesNormalLod(float3 worldPos, float lod)
{
	float distanceToCamera = GetWorldToCameraDistance(worldPos);
	float3 finalData = float3(0, 1, 0);
	worldPos += KWS_WaterWorldPosOffset;

	UNITY_LOOP for (int idx = KWS_WavesCascades - 1; idx > 0; idx--)
	{
		float fade = GetFftFade(distanceToCamera, idx, 0.25);
		if (fade < 0.01) continue;

		float3 data = fade * KWS_FftWavesNormal.SampleLevel(sampler_trilinear_repeat, float3(worldPos.xz / GetDomainSize(idx), idx), lod).xyz;
		data.y = 1;
		finalData = KWS_BlendNormals(finalData, data);
	}
	
	return float3(finalData.x, 1, finalData.z);
}



float3 GetFftWavesNormalFoam(float3 worldPos, float attenuation)
{
	float distanceToCamera = GetWorldToCameraDistance(worldPos);
	float3 finalData = float3(0, 1, 0);
	float foam = 0;
	int idx = KWS_WavesCascades;
	worldPos += KWS_WaterWorldPosOffset;

	float2 noise1 = float2(SimpleNoise1(worldPos.xz * 0.005 + KWS_ScaledTime * 0.15), SimpleNoise1(worldPos.xz * 0.005 - (KWS_ScaledTime * 0.15 + 40)));
	float2 noise2 = float2(SimpleNoise1(worldPos.xz * 0.026 + KWS_ScaledTime * 0.06), SimpleNoise1(worldPos.xz * 0.024 - (KWS_ScaledTime * 0.07 + 40)));
	float animNoise1 = saturate(0.05 + KWS_Pow10(1 - saturate(max(noise1.x, noise1.y) * 0.2)));
	float animNoise2 = saturate(0.1 + KWS_Pow5(1 - saturate(max(noise2.x, noise2.y) * 0.75)));

	UNITY_UNROLL for (int i = 0; i <= MAX_FFT_WAVES_MAX_CASCADES; i++)
	{
		idx--;
		if (idx < 0) break;

		float fade = GetFftFade(distanceToCamera, idx, 0.25);
		float windAttenuation = lerp(attenuation * saturate(1.2 - 0.2 * idx), 1, attenuation);
		
		fade *= windAttenuation;

		//if (fade < 0.01) continue;
		float3 data = float3(fade, animNoise1 * animNoise2, fade);
		float3 normal = 0;

		if (idx == 0 || idx == 3) normal = Texture2DArraySampleBicubic(KWS_FftWavesNormal, sampler_linear_repeat, worldPos.xz / GetDomainSize(idx), KWS_FftWavesNormal_TexelSize, idx).xyz;
		else normal = KWS_FftWavesNormal.Sample(sampler_KWS_FftWavesNormal, float3(worldPos.xz / GetDomainSize(idx), idx)).xyz;

		data *= normal;

		foam += data.y;
		data.y = 1;
		finalData = KWS_BlendNormals(finalData, data);
	}
	
	//return finalData;
	return float3(finalData.x, foam, finalData.z);
}

float3 GetFftWavesNormalDomain(float3 worldPos, uint idx)
{
	return KWS_FftWavesNormal.Sample(sampler_KWS_FftWavesNormal, float3(worldPos.xz / GetDomainSize(idx), idx)).xyz;
}


float GetFftWavesHeight(float3 worldPos, uint iterations)
{
	float3 invertedDisplacedPosition = worldPos;
	for (uint i = 0; i < iterations; i++)
	{
		float3 displacement = GetFftWavesDisplacementBuoyancy(invertedDisplacedPosition);
		float3 error = (invertedDisplacedPosition + displacement) - worldPos;
		invertedDisplacedPosition -= error;
	}

	float3 disp = GetFftWavesDisplacement(invertedDisplacedPosition);
	return disp.y;
}


//////////////////////////////////////////////////////////////////////////////////////////////////////////////////




//////////////////////////////////////////////    PrePass (Mask/Normal/SSS/VolumeMask/Depth)   //////////////////////////////////////////////


//#define WATER_MASK_PASS_UNDERWATER_THRESHOLD 0.35
#define WATER_VOLUME_PRE_PASS_MAX_VALUE 1000000
#define WATER_LINE_HEIGHT_ENCODE_VALUE 10000

DECLARE_TEXTURE(KWS_WaterPrePassRT0);
DECLARE_TEXTURE(KWS_WaterPrePassRT1);
DECLARE_TEXTURE(KWS_WaterDepthRT);
DECLARE_TEXTURE(KWS_WaterIntersectionHalfLineTensionMaskRT);
float4 KWS_WaterPrePassRT0_TexelSize;
float4 KWS_WaterPrePass_RTHandleScale;

DECLARE_TEXTURE(KWS_WaterBackfacePrePassRT0);
DECLARE_TEXTURE(KWS_WaterBackfacePrePassRT1);
DECLARE_TEXTURE(KWS_WaterBackfaceDepthRT);

float4 KWS_WaterBackfacePrePassRT0_TexelSize;
float4 KWS_WaterBackfacePrePass_RTHandleScale;

inline float2 GetWaterPrePassUV(float2 uv)
{
	uv = GetRTHandleUV(uv, KWS_WaterPrePassRT0_TexelSize.xy, 1.0, KWS_WaterPrePass_RTHandleScale.xy);
	return uv;
}

inline float2 GetWaterBackfacePrePassUV(float2 uv)
{
	uv = GetRTHandleUV(uv, KWS_WaterBackfacePrePassRT0_TexelSize.xy, 1.0, KWS_WaterBackfacePrePass_RTHandleScale.xy);
	return uv;
}

inline float GetWaterSSS(float2 uv)
{
	return SAMPLE_TEXTURE_LOD(KWS_WaterPrePassRT0, sampler_linear_clamp, GetWaterPrePassUV(uv), 0).z;
}

inline float3 GetWaterNormals(float2 uv)
{
	float2 rawNormal = SAMPLE_TEXTURE_LOD(KWS_WaterPrePassRT1, sampler_linear_clamp, GetWaterPrePassUV(uv), 0).xy;
	#ifdef KWS_USE_AQUARIUM_RENDERING
		float2 rawNormalBackface = SAMPLE_TEXTURE_LOD(KWS_WaterBackfacePrePassRT1, sampler_point_clamp, GetWaterBackfacePrePassUV(uv), 0).xy;
		rawNormal += rawNormalBackface;
	#endif
	
	return float3(rawNormal.x, 1, rawNormal.y);
}


inline float GetWaterAquariumBackfaceMask(float2 uv)
{
	return SAMPLE_TEXTURE_LOD(KWS_WaterBackfacePrePassRT0, sampler_point_clamp, GetWaterBackfacePrePassUV(uv), 0).y;
}


//inside = 1, surface outside = 0.5, box fringe = 0.1
inline float GetWaterMaskFast(float2 uv, float2 offset = float2(0, 0))
{
	return SAMPLE_TEXTURE_LOD(KWS_WaterPrePassRT0, sampler_linear_clamp, GetWaterPrePassUV(uv), 0).y;
}


//inside = 1, surface outside = 0.5, box fringe = 0.1
inline float GetWaterMask(float2 uv, float2 offset = float2(0, 0))
{
	#ifdef KWS_SHARED_API_INCLUDED
		return GetWaterMaskFast(uv);
	#else
		float4 mask = SAMPLE_TEXTURE_GATHER_GREEN(KWS_WaterPrePassRT0, sampler_linear_clamp, GetWaterPrePassUV(uv) + KWS_WaterPrePassRT0_TexelSize.xy * offset);
		return max(mask.x, max(mask.y, max(mask.z, mask.w)));
	#endif

	//float mask = SAMPLE_TEXTURE_LOD(KWS_WaterPrePassRT0, sampler_point_clamp, GetWaterPrePassUV(uv), 0).y;
	
	//float center = SAMPLE_TEXTURE_LOD(KW_WaterMaskScatterNormals, sampler_point_clamp, GetWaterPrePassUV(uv), 0).x;
	//float up = SAMPLE_TEXTURE_LOD_OFFSET(KW_WaterMaskScatterNormals, sampler_point_clamp, GetWaterPrePassUV(uv), 0, int2(0, 1)).x;
	//float down = SAMPLE_TEXTURE_LOD_OFFSET(KW_WaterMaskScatterNormals, sampler_point_clamp, GetWaterPrePassUV(uv), 0, int2(0, -1)).x;
	//float diff = (up + down) * 0.5 - center;

	//if((center == 0.0 || center == 1.0) && down == up) return down;
	
	//return mask;

}


inline float GetUnderwaterMask(float waterMask)
{
	return waterMask > 0.5;
}

inline bool GetSurfaceMask(float waterMask)
{
	return abs(waterMask - 0.25) < 0.01;
}

inline bool GetUnderwaterSurfaceMask(float waterMask)
{
	return abs(waterMask - 0.75) < 0.01;
}


inline float GetWaterHalfLineTensionMask(float2 uv)
{
	float intersectionMask = 0;
	float aquariumMask = 0;
	float2 scaledUV = GetWaterPrePassUV(uv);

	#ifdef KWS_CAMERA_UNDERWATER
		intersectionMask = SAMPLE_TEXTURE_LOD(KWS_WaterIntersectionHalfLineTensionMaskRT, sampler_linear_clamp, scaledUV, 0).x;
		intersectionMask *= 1.4;
		if (intersectionMask >= 0.99) intersectionMask = saturate((1.2 - intersectionMask) * 5);
	#endif
	#ifdef KWS_USE_AQUARIUM_RENDERING
		aquariumMask = SAMPLE_TEXTURE_LOD(KWS_WaterPrePassRT0, sampler_linear_clamp, scaledUV, 0).w;
		intersectionMask = max(intersectionMask, aquariumMask);
	#endif

	return intersectionMask;
}


inline float GetWaterDepth(float2 uv)
{
	return SAMPLE_TEXTURE_LOD(KWS_WaterDepthRT, sampler_point_clamp, GetWaterPrePassUV(uv), 0).x;
}


inline float GetWaterBackfaceDepth(float2 uv)
{
	return SAMPLE_TEXTURE_LOD(KWS_WaterBackfaceDepthRT, sampler_point_clamp, GetWaterBackfacePrePassUV(uv), 0).x;
}

inline uint GetWaterLocalZonesTransparent(float2 uv)
{
	return SAMPLE_TEXTURE_LOD(KWS_WaterPrePassRT0, sampler_point_clamp, GetWaterPrePassUV(uv), 0).x * 100.0;
}

//Front depth (x), Back depth (y)
inline float2 GetWaterVolumeDepth(float2 uv, float surfaceZ, float sceneZ, float waterMask)
{
	float2 volumeDepth = 0;
	
	volumeDepth.x = surfaceZ;
	volumeDepth.y = sceneZ;

	#if KWS_USE_AQUARIUM_RENDERING
		volumeDepth.y = max(volumeDepth.y, GetWaterBackfaceDepth(uv));
	#endif

	bool underwaterMask = GetUnderwaterMask(waterMask);
	volumeDepth.y = lerp(volumeDepth.y, max(volumeDepth.x, sceneZ), underwaterMask);
	volumeDepth.x = lerp(volumeDepth.x, 1, underwaterMask);

	if (volumeDepth.x < sceneZ) volumeDepth.x = 0;

	return volumeDepth;
}

//Front depth (x), Back depth (y)
inline float2 GetWaterVolumeDepth(float2 uv, float sceneZ, float waterMask)
{
	float2 volumeDepth = 0;
	
	volumeDepth.x = GetWaterDepth(uv).x;
	volumeDepth.y = sceneZ;

	#if KWS_USE_AQUARIUM_RENDERING
		volumeDepth.y = max(volumeDepth.y, GetWaterBackfaceDepth(uv));
	#endif

	bool underwaterMask = GetUnderwaterMask(waterMask);
	volumeDepth.y = lerp(volumeDepth.y, max(volumeDepth.x, sceneZ), underwaterMask);
	volumeDepth.x = lerp(volumeDepth.x, 1, underwaterMask);

	if (volumeDepth.x < sceneZ) volumeDepth.x = 0;

	return volumeDepth;
}

inline float GetBoxExtrude(float3 pos, float3x3 rotationMatrix, float3 size)
{
	float3 rotatedPos = mul(rotationMatrix, pos).xyz;
	float3 d = abs(rotatedPos) - size;
	return length(max(d, 0)) + KWS_MAX(min(d, 0));
}


/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////





//////////////////////////////////////////////    VolumetricLighting_Pass    //////////////////////////////////////////////

DECLARE_TEXTURE(KWS_VolumetricLightRT);
DECLARE_TEXTURE(KWS_VolumetricLightAdditionalDataRT);
DECLARE_TEXTURE(KWS_VolumetricLightSurfaceRT);

float4 KWS_VolumetricLightRT_TexelSize;
float4 KWS_VolumetricLight_RTHandleScale;

DECLARE_TEXTURE(KWS_VolumetricLightRT_Last);
float4 KWS_VolumetricLightRT_Last_TexelSize;
float4 KWS_VolumetricLightRT_Last_RTHandleScale;

struct VolumetricLightAdditionalData
{
	half SurfaceDirShadow;
	half SceneDirShadow;
	half AdditionalLightsAttenuation;
};



inline float GetMaxRayDistanceRelativeToTransparent(float transparent)
{
	return min(KWS_MAX_TRANSPARENT, transparent * 1.5);
}


inline half4 GetVolumetricLight(float2 uv)
{
	return SAMPLE_TEXTURE_LOD(KWS_VolumetricLightRT, sampler_linear_clamp, saturate(uv), 0);
}

//(R) surface dir shadow, (G) scene dir shadow, (B) additional lights attenuation
inline VolumetricLightAdditionalData GetVolumetricLightAdditionalData(float2 uv)
{
	float3 rawData = SAMPLE_TEXTURE_LOD(KWS_VolumetricLightAdditionalDataRT, sampler_linear_clamp, saturate(uv), 0).xyz;
	#if defined(KWS_USE_DYNAMIC_WAVES)
		float4 shadowFix = SAMPLE_TEXTURE_GATHER(KWS_VolumetricLightAdditionalDataRT, sampler_linear_clamp, saturate(uv));
		rawData.x = rawData.x * shadowFix.x * shadowFix.y * shadowFix.z * shadowFix.w;
	#endif


	VolumetricLightAdditionalData volumeData;
	volumeData.SurfaceDirShadow = rawData.x;
	volumeData.SceneDirShadow = rawData.y;
	volumeData.AdditionalLightsAttenuation = rawData.z;
	return volumeData;
}


inline float3 GetVolumetricSurfaceLight(float2 uv)
{
	float3 surfaceLight = SAMPLE_TEXTURE_LOD(KWS_VolumetricLightSurfaceRT, sampler_linear_clamp, saturate(uv), 0).xyz;
	if(surfaceLight.r < 0.00001) return GetAmbientColor(GetExposure());
	else return surfaceLight;
}

inline float GetVolumeLightInDepthTransmitance(float waterHeight, float currentHeight, float transparent)
{
	//at far distance currentHeight has some float precission error (water depth -> world pos) and random lines. In this case I can just add small heigh offset relative to water surface <---> camera
	//float distanceToCamera = saturate((_WorldSpaceCameraPos.y - waterHeight) * 0.01) * 10;
	float distanceToWaterSurface = waterHeight - _WorldSpaceCameraPos.y - 1;
	float lightInDepthTransmitance = saturate(exp2(-0.5 * distanceToWaterSurface / max(2, transparent)) * 1.1 - 0.1);
	
	return lightInDepthTransmitance;
}


inline float GetVolumeLightSunAngleAttenuation(float3 lightDir, float3 normal = float3(0, 1, 0))
{
	float dotVal = dot(lightDir, normal);
	return smoothstep(-0.25, 1.0, dotVal);
	//return saturate(dot(lightDir, normal));

}

inline half3 GetVolumetricSurfaceLight(float4 volumeScattering, float3 normal, float exposure)
{
	float3 ambient = GetAmbientColor(exposure);
	float3 dirLight = GetVolumeLightSunAngleAttenuation(GetMainLightDir(), normal) * GetMainLightColor(exposure);

	return (ambient + dirLight * volumeScattering.a + volumeScattering.rgb);
}

float4 ComputeAbsorbtion(float transparent, float3 dyeColor, float rayLength)
{
	float dyeOverrideFactor = dot(dyeColor, 0.33);
	float3 absorbtionColor = lerp(1 - dyeColor, float3(1.0, 0.12, 0.02), dyeOverrideFactor);
	float absorbtionMultiplier = lerp(0.2, 0.1, saturate((transparent - KWS_MAX_TRANSPARENT) / KWS_MAX_TRANSPARENT));

	float3 finalAbsorbtion = max(float3(0.0005, 0.001, 0.025), exp2(-KWS_VOLUME_LIGHT_ABSORBTION_FACTOR * pow(rayLength, 1.5) * absorbtionColor * absorbtionMultiplier));

	rayLength = min(rayLength, min(KWS_MAX_TRANSPARENT, transparent));
	float extintionIntegralApproximationCoeff = KWS_Pow2(transparent * 0.2);

	float extinction = 1 - saturate(exp2(-rayLength / extintionIntegralApproximationCoeff + KWS_VOLUME_LIGHT_TRANSMITANCE_NEAR_OFFSET_FACTOR));

	return saturate(float4(finalAbsorbtion, extinction));
}

inline half4 GetVolumetricLightWithAbsorbtion(float2 uv, float2 refractionUV, float transparent, float3 tubidityColor, float3 dyeColor, float3 sceneColor, float2 volumeDepth, float exposure, float3 sceneColorAdditiveAlbedo)
{
	float3 frontPos = GetWorldSpacePositionFromDepth(refractionUV, volumeDepth.x);
	float3 backPos = GetWorldSpacePositionFromDepth(refractionUV, volumeDepth.y);
	float rayLength = length(backPos - frontPos);
	if (volumeDepth.x == 0) rayLength = 0.1;

	float4 absorbtion = ComputeAbsorbtion(transparent, dyeColor, rayLength);

	float waterSurfaceShadow = 1;
	half sceneAttenuation = GetVolumeLightInDepthTransmitance(KWS_WaterPosition.y, backPos.y, transparent);
	
	#if KWS_USE_VOLUMETRIC_LIGHT
		float4 scattering = GetVolumetricLight(refractionUV);
		//float4 scatteringWithOffset = GetVolumetricLight(uv);
		//if ((scattering.x + scattering.y + scattering.z) <= 0.1) scattering = scatteringWithOffset;
		
		VolumetricLightAdditionalData volumeLightData = GetVolumetricLightAdditionalData(uv);

		sceneAttenuation = max(sceneAttenuation, volumeLightData.AdditionalLightsAttenuation);
		waterSurfaceShadow = volumeLightData.SurfaceDirShadow;
		//absorbtion.a = scattering.a;
	#else
		float phaseG = GetVolumeLightSunAngleAttenuation(GetMainLightDir());
		float3 sceneLight = 0.5 * GetAmbientColor(exposure) + GetMainLightColor(exposure) * phaseG;
		float4 scattering = half4(0.5 * tubidityColor * saturate(sceneLight), 1.0);
	#endif
	
	sceneColor *= sceneAttenuation;

	#ifdef KWS_SHARED_API_INCLUDED
		float3 albedo = sceneColorAdditiveAlbedo * dot(scattering.xyz, 0.33);
		float3 finalColor = lerp(absorbtion.rgb * sceneColor + clamp(albedo, 0, 2), scattering.xyz, absorbtion.a);
	#else
		float3 finalColor = lerp(absorbtion.rgb * sceneColor, scattering.xyz, absorbtion.a);
	#endif
	
	return float4(finalColor, waterSurfaceShadow);
}

inline half4 GetVolumetricLightWithAbsorbtionByDistance(float2 uv, float2 refractionUV, float transparent, float3 tubidityColor, float3 dyeColor, float3 sceneColor, float rayLength, float exposure, float3 sceneColorAdditiveAlbedo)
{
	float4 absorbtion = ComputeAbsorbtion(transparent, dyeColor, rayLength);

	#if KWS_USE_VOLUMETRIC_LIGHT
		float4 scattering = GetVolumetricLight(refractionUV);
		//float4 scatteringWithOffset = GetVolumetricLight(uv);
		//if ((scattering.x + scattering.y + scattering.z) <= 0.1) scattering = scatteringWithOffset;
		
		//absorbtion.a = scattering.a;
	#else
		float phaseG = GetVolumeLightSunAngleAttenuation(GetMainLightDir());
		float3 sceneLight = 0.5 * GetAmbientColor(exposure) + GetMainLightColor(exposure) * phaseG;
		float4 scattering = half4(0.5 * tubidityColor * saturate(sceneLight), 1.0);
	#endif

	#ifdef KWS_SHARED_API_INCLUDED
		float3 albedo = sceneColorAdditiveAlbedo * dot(scattering.xyz, 0.33);
		float3 finalColor = lerp(absorbtion.rgb * sceneColor + clamp(albedo, 0, 2), scattering.xyz, absorbtion.a);
	#else
		float3 finalColor = lerp(absorbtion.rgb * sceneColor, scattering.xyz, absorbtion.a);
	#endif
	
	return float4(finalColor, absorbtion.a);
}


inline half3 GetSurfaceLightWithAbsorbtion(float2 uv, float transparent, float3 tubidityColor, float3 dyeColor, float exposure)
{
	float dyeOverrideFactor = dot(dyeColor, 0.33);
	float3 absorbtionColor = lerp(1 - dyeColor, float3(1.0, 0.13, 0.02), dyeOverrideFactor);
	float absorbtionMultiplier = lerp(1, 0.0, saturate((transparent) / KWS_MAX_TRANSPARENT));

	float extinction = 0;

	float3 finalAbsorbtion = absorbtionColor;

	float phaseG = GetVolumeLightSunAngleAttenuation(GetMainLightDir());
	float3 sceneLight = 0.5 * GetAmbientColor(exposure) + GetMainLightColor(exposure) * phaseG;
	float4 scattering = half4(0.5 * tubidityColor * saturate(sceneLight), 1.0);

	float3 finalColor = lerp(finalAbsorbtion * absorbtionMultiplier, scattering.xyz, 0.5);

	return finalColor;
}

inline half3 GetSurfaceLightWithAbsorbtionByDistance(float2 uv, float transparent, float3 tubidityColor, float3 dyeColor, float rayLength, float exposure)
{
	float dyeOverrideFactor = dot(dyeColor, 0.33);
	float3 absorbtionColor = lerp(1 - dyeColor, float3(1.0, 0.13, 0.02), dyeOverrideFactor);
	float absorbtionMultiplier = lerp(1, 0.0, saturate((transparent) / KWS_MAX_TRANSPARENT));

	float extinction = 1 - saturate(exp2(-rayLength));

	float3 finalAbsorbtion = max(float3(0.0005, 0.001, 0.025), exp2(-rayLength * absorbtionColor));

	float phaseG = GetVolumeLightSunAngleAttenuation(GetMainLightDir());
	float3 sceneLight = 0.5 * GetAmbientColor(exposure) + GetMainLightColor(exposure) * phaseG;
	float4 scattering = half4(0.5 * tubidityColor * saturate(sceneLight), 1.0);

	float3 finalColor = lerp(finalAbsorbtion * absorbtionMultiplier, scattering.xyz, extinction);

	return finalColor;
}


inline half4 GetVolumetricLightLastFrame(float2 uv)
{
	//float2 scaledUV = GetRTHandleUV(uv, KWS_VolumetricLightRT_TexelSize.xy, 1.0, KWS_VolumetricLight_RTHandleScale.xy);
	//scaledUV += KWS_VolumetricLightRT_TexelSize.xy * 0.5;
	return SAMPLE_TEXTURE_LOD(KWS_VolumetricLightRT_Last, sampler_point_clamp, uv, 0);
}


/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


//////////////////////////////////////////////    ScreenSpaceReflection_Pass    //////////////////////////////////////////////
DECLARE_TEXTURE(KWS_ScreenSpaceReflectionRT);
float4 KWS_ScreenSpaceReflectionRT_TexelSize;
float4 KWS_ScreenSpaceReflection_RTHandleScale;
#define KWS_ScreenSpaceReflectionMaxMip 4

inline float2 GetScreenSpaceReflectionNormalizedUV(float2 uv)
{
	uv = GetRTHandleUV(uv, KWS_ScreenSpaceReflectionRT_TexelSize.xy, 0.5, KWS_ScreenSpaceReflection_RTHandleScale.xy);
	//uv -= KWS_ScreenSpaceReflectionRT_TexelSize.xy * 0.5;
	return uv;
	//return clamp(uv, 0.001, 0.999) * KWS_ScreenSpaceReflection_RTHandleScale.xy;

}

inline half4 GetScreenSpaceReflection(float2 uv, float3 worldPos)
{
	float2 ssrUV = GetScreenSpaceReflectionNormalizedUV(uv);
	float mipLevel = CalcMipLevel(ssrUV * KWS_ScreenSpaceReflectionRT_TexelSize.zw);

	float distance = length(worldPos.xz - GetCameraAbsolutePosition().xz);
	float anisoScaleRelativeToDistance = saturate(distance * 0.05);

	float lod = min(mipLevel, KWS_ScreenSpaceReflectionMaxMip);
	//lod = anisoScaleRelativeToDistance * 2;

	float4 res = SAMPLE_TEXTURE_LOD(KWS_ScreenSpaceReflectionRT, sampler_trilinear_clamp, ssrUV, lod);
	//res.a = saturate(res.a); //I use negative alpha to minimize edge bilinear interpolation artifacts.
	return res;
}

float KWS_ScreenSpaceBordersStretching;
inline half4 GetScreenSpaceReflectionWithStretchingMask(float2 uv, float3 worldPos)
{
	#if defined(STEREO_INSTANCING_ON)
		uv -= mul((float2x2)UNITY_MATRIX_V, float2(0, KWS_ReflectionClipOffset)).xy;
	#else
		uv.y -= KWS_ReflectionClipOffset;
	#endif

	float AngleStretch = max(0.25, saturate(-KWS_CameraForward.y * 1.5));
	float ScreenStretch = saturate(abs(uv.x * 2 - 1) - 0.8);
	float uvOffset = -AngleStretch * ScreenStretch * KWS_ScreenSpaceBordersStretching * 10;
	uv.x = uv.x * (1 + uvOffset * 2) - uvOffset;
	//float stretchingMask = 1 - abs(refl_uv.x * 2 - 1);
	//refl_uv.x = lerp(refl_uv.x * (1 - Test4.x * 2) + Test4.x, refl_uv.x, AngleStretch * ScreenStretch);
	return GetScreenSpaceReflection(uv, worldPos);
}


float GetAnisoScaleRelativeToWind(float windSpeed)
{
	float normalizedWind = saturate((windSpeed - 0.1) / 10.0);
	return (normalizedWind) * KWS_AnisoReflectionsScale;
}

float2 GetScreenSpaceReflectionUV(float3 reflDir, float2 orthoUV)
{
	UNITY_BRANCH
	if (unity_OrthoParams.w == 1.0) return orthoUV;
	else
	{
		reflDir.y = -reflDir.y;
		float4 projected = mul(UNITY_MATRIX_VP, float4(reflDir, 0));
		float2 uv = (projected.xy / projected.w) * 0.5f + 0.5f;
		
		#ifdef UNITY_UV_STARTS_AT_TOP
			uv.y = 1 - uv.y;
		#endif

		return uv;
	}
}



////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


//////////////////////////////////////////////    Planar Reflection    //////////////////////////////////////////////////////////
DECLARE_TEXTURE(KWS_PlanarReflectionRT);
int KWS_PlanarReflectionInstanceID;
float4 KWS_PlanarReflectionRT_TexelSize;
TextureCube KWS_CubemapReflectionRT;

#define KWS_PlanarReflectionMaxMip 4

inline half3 GetPlanarReflection(float2 refl_uv)
{
	float mipLevel = CalcMipLevel(refl_uv * KWS_PlanarReflectionRT_TexelSize.zw);
	float lod = min(mipLevel, KWS_PlanarReflectionMaxMip);
	return SAMPLE_TEXTURE_LOD(KWS_PlanarReflectionRT, sampler_trilinear_clamp, refl_uv, lod).xyz;
}

inline half3 GetPlanarReflectionRaw(float2 refl_uv)
{
	return SAMPLE_TEXTURE_LOD(KWS_PlanarReflectionRT, sampler_trilinear_clamp, refl_uv, 0).xyz;
}

inline half3 GetPlanarReflectionWithClipOffset(float2 refl_uv)
{
	#if defined(STEREO_INSTANCING_ON)
		refl_uv -= mul((float2x2)UNITY_MATRIX_V, float2(0, KWS_ReflectionClipOffset)).xy;
	#else
		refl_uv.y -= KWS_ReflectionClipOffset;
	#endif
	return GetPlanarReflection(refl_uv);
}


///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////




//////////////////////////////////////////////    Reflection Effects Pass    //////////////////////////////////////////////////////////

float Fresnel_IOR(float3 viewDir, float3 normal, float ior)
{
	float cosi = clamp(-1, 1, dot(viewDir, normal));
	float etai = 1, etat = ior;
	if (cosi > 0)
	{
		float temp = etat;
		etat = etai;
		etai = temp;
	}
	
	float sint = etai / etat * sqrt(max(0.f, 1 - cosi * cosi));
	
	if (sint >= 1)
	{
		return 1;
	}
	else
	{
		float cost = sqrt(max(0.f, 1 - sint * sint));
		cosi = abs(cosi);
		float Rs = ((etat * cosi) - (etai * cost)) / ((etat * cosi) + (etai * cost));
		float Rp = ((etai * cosi) - (etat * cost)) / ((etai * cosi) + (etat * cost));
		return (Rs * Rs + Rp * Rp) / 2;
	}
	// As a consequence of the conservation of energy, transmittance is given by:
	// kt = 1 - kr;

}

inline half SelfSmithJointGGXVisibilityTerm(half NdotL, half NdotV, half roughness)
{
	half a = roughness;
	half lambdaV = NdotL * (NdotV * (1 - a) + a);
	half lambdaL = NdotV * (NdotL * (1 - a) + a);

	return 0.5f / (lambdaV + lambdaL + 1e-5f);
}


inline half SelfGGXTerm(half NdotH, half roughness)
{
	half a2 = roughness * roughness;
	half d = (NdotH * a2 - NdotH) * NdotH + 1.0f; // 2 mad
	return 0.31830988618f * a2 / (d * d + 1e-7f); // This function is not intended to be running on Mobile,
	// therefore epsilon is smaller than what can be represented by half

}

inline half ComputeSpecular(half nl, half nv, half nh, half viewDistNormalized, half smoothness)
{
	half V = SelfSmithJointGGXVisibilityTerm(nl, nv, smoothness);
	half D = SelfGGXTerm(nh, viewDistNormalized * 0.1 + smoothness);

	half specularTerm = V * D;
	specularTerm = max(0, specularTerm * nl * KWS_SunStrength);

	return specularTerm;
}



float ComputeWaterFresnel(float3 normal, float3 viewDir, float minValue = 0.05)
{
	float x = 1 - saturate(dot(normal, viewDir));
	return minValue + (1 - minValue) * x * x * x * x ; //fresnel aproximation http://wiki.nuaj.net/images/thumb/1/16/Fresnel.jpg/800px-Fresnel.jpg

}

half3 ComputeSSS(float2 screenUV, float sssMask, half3 underwaterColor, half shadowMask, half transparent)
{
	float3 sssColor = underwaterColor;
	return sssMask * shadowMask * sssColor * (saturate(transparent * 0.05) + 0.1);
}

half3 ComputeSunlight(half3 normal, half3 viewDir, float3 lightDir, float3 lightColor, half shadowMask, float viewDist, float waterFarDistance, half transparent)
{
	half3 halfDir = normalize(lightDir + viewDir);
	half nh = saturate(dot(normal, halfDir));
	half nl = saturate(dot(normal, lightDir));
	half lh = saturate(dot(lightDir, halfDir));
	half fresnel = saturate(dot(normal, viewDir));
	
	float viewDistNormalized = saturate(viewDist / (waterFarDistance * 2));
	half3 specular = ComputeSpecular(nl, fresnel, nh, viewDistNormalized, KWS_SunCloudiness);
	specular = clamp(specular * 10 - 2.5 * saturate(1 - KWS_SunCloudiness * 10), 0, KWS_SunMaxValue);
	//half sunset = saturate(0.01 + dot(lightDir, float3(0, 1, 0))) * 30;

	return shadowMask * specular * lightColor;
}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////






//////////////////////////////////////////////    Caustic_Pass    //////////////////////////////////////////////////////////
Texture2DArray KWS_CausticRTArray;
float4 KWS_CausticRTArray_TexelSize;
float KWS_CaustisDispersionStrength;

inline float3 GetCausticSlice(float2 uv, uint causticSlice)
{
	float3 caustic = 1;
	#ifdef USE_DISPERSION
		float2 offset = KWS_CausticRTArray_TexelSize.x * KWS_CaustisDispersionStrength;
		caustic.x = KWS_CausticRTArray.Sample(sampler_linear_repeat, float3(uv - offset, causticSlice)).x;
		caustic.y = KWS_CausticRTArray.Sample(sampler_linear_repeat, float3(uv, causticSlice)).x;
		caustic.z = KWS_CausticRTArray.Sample(sampler_linear_repeat, float3(uv + offset, causticSlice)).x;
	#else
		caustic = KWS_CausticRTArray.Sample(sampler_linear_repeat, float3(uv, causticSlice)).x;
	#endif

	return caustic;
}

inline half GetCausticLod(float2 uv, uint causticSlice, float lod)
{
	return KWS_CausticRTArray.SampleLevel(sampler_linear_repeat, float3(uv, causticSlice), lod).x;
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



//////////////////////////////////////////////    Flowmap_Pass    /////////////////////////////////////////////////////////////////////////////////////////
#define RIVER_FLOW_FRESNEL_MULTIPLIER 0.5
#define RIVER_FLOW_NORMAL_MULTIPLIER 1.5

struct FlowMapData
{
	float2 uvOffset1;
	float2 uvOffset2;
	float lerpValue;
};

inline FlowMapData GetFlowmapData(float2 direction, float timeScale)
{
	FlowMapData data;

	/*float time = KWS_ScaledTime * timeScale * 0.5;
	float2 d = sin(uv * 3.1415);
	time -= (d.x + d.y) * 0.25 + 0.5;
	
	float progressA = frac(time) - 0.5;
	float progressB = frac(time + 0.5) - 0.5;

	float2 jump = float2(0.248, 0.201);
	float2 offsetA = (time - progressA) * jump;
	float2 offsetB = (time - progressB) * jump + 0.5;

	data.uvOffset1 = -progressA * direction + offsetA;
	data.uvOffset2 = -progressB * direction + offsetB;
	data.lerpValue = saturate(abs(progressA * 2.0));*/
	
	float time = KWS_ScaledTime * timeScale * 0.5;
	half time1 = frac(time + 0.5);
	half time2 = frac(time);

	data.uvOffset1 = -direction * time1;
	data.uvOffset2 = -direction * time2;
	data.lerpValue = abs((0.5 - time1) / 0.5);
	
	return data;
}

inline FlowMapData GetFlowmapData(float2 uv, float2 direction, float timeScale)
{
	FlowMapData data;

	float time = KWS_ScaledTime * timeScale * 0.5;
	float2 d = sin(uv * 3.1415);
	time -= (d.x + d.y) * 0.25 + 0.5;
	
	float progressA = frac(time) - 0.5;
	float progressB = frac(time + 0.5) - 0.5;

	float2 jump = float2(0.248, 0.201);
	float2 offsetA = (time - progressA) * jump;
	float2 offsetB = (time - progressB) * jump + 0.5;

	data.uvOffset1 = -progressA * direction + offsetA;
	data.uvOffset2 = -progressB * direction + offsetB;
	data.lerpValue = saturate(abs(progressA * 2.0));
	
	//float time = KWS_ScaledTime * timeScale * 0.5;
	//half time1 = frac(time + 0.5);
	//half time2 = frac(time);

	//data.uvOffset1 = -direction * time1;
	//data.uvOffset2 = -direction * time2;
	//data.lerpValue = abs((0.5 - time1) / 0.5);
	
	return data;
}


inline float3 GetFftWavesDisplacementWithFlowMap(float3 worldPos, float2 direction, float timeScale)
{
	FlowMapData data = GetFlowmapData(direction, timeScale);
	
	float3 disp1 = GetFftWavesDisplacementDetails(worldPos + data.uvOffset1.xyy);
	float3 disp2 = GetFftWavesDisplacementDetails(worldPos + data.uvOffset2.xyy);
	
	float3 result = lerp(disp1, disp2, data.lerpValue);

	return result;
}


inline float3 GetFftWavesNormalFoamWithFlowmap(float3 worldPos, float2 direction, float timeScale, float time)
{
	FlowMapData data = GetFlowmapData(direction, timeScale);
	
	float3 result1 = GetFftWavesNormalDomain(worldPos + float3(data.uvOffset1.x, 0, data.uvOffset1.y), 0);
	float3 result2 = GetFftWavesNormalDomain(worldPos + float3(data.uvOffset2.x, 0, data.uvOffset2.y), 0);
	float3 normal = lerp(result1, result2, data.lerpValue);
	normal.y = 1;
	return normal;

	///
	//time *= timeScale;
	//float2 noise1 = float2(SimpleNoise1(worldPos.xz * 3 + time * 1.4), SimpleNoise1(worldPos.xz * 3.2 - (time * 1.3 + 40))) * 2 - 1;
	//float2 noise2 = float2(SimpleNoise1(worldPos.xz * 8 - time * 2), SimpleNoise1(worldPos.xz * 7 + ( time * 2.2 + 40))) * 2 - 1;
	//noise1 *= 0.01;
	//noise2 *= 0.007;

	//float3 result1 = KWS_WaterDynamicWavesFlowMapNormal.Sample(sampler_linear_repeat, (noise1 + noise2 + worldPos.xz + data.uvOffset1)* 0.2).xzy * 2 - 1;
	//float3 result2 = KWS_WaterDynamicWavesFlowMapNormal.Sample(sampler_linear_repeat, (noise1 + noise2 + worldPos.xz + data.uvOffset2)* 0.2).xzy * 2 - 1;

	//float3 normal = lerp(result1, result2, data.lerpValue);
	//normal.y = 1;
	//normal.xz *= 0.35;
	//return normal;

	//return lerp(normal, normal2, Test4.x > 0);

}



///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



//////////////////////////////////////////////    Otrho depth    //////////////////////////////////////////////////////////
Texture2D KWS_WaterOrthoDepthRT;
Texture2D KWS_WaterOrthoDepthSDF;
Texture2D KWS_WaterOrthoDepthBackfaceRT;

float3 KWS_OrthoDepthPos;
float4 KWS_OrthoDepthNearFarSize;
float4x4 KWS_OrthoDepthCameraMatrix;
float4 KWS_WaterOrthoDepthRT_TexelSize;
float4 KWS_WaterOrthoDepthSDF_TexelSize;

float4 ReconstructWaterOrthoDepthPos(float3 worldPos)
{
	return mul(KWS_OrthoDepthCameraMatrix, float4(worldPos, 1));
}

float2 GetWaterOrthoDepthUV(float3 worldPos)
{
	return (worldPos.xz - KWS_OrthoDepthPos.xz) / KWS_OrthoDepthNearFarSize.zw + 0.5;
}

float GetWaterOrthoDepth(float2 uv)
{
	float cameraHeight = KWS_OrthoDepthNearFarSize.x;
	float far = KWS_OrthoDepthNearFarSize.y;
	float terrainDepth = KWS_WaterOrthoDepthRT.SampleLevel(sampler_linear_clamp, uv, 0).r;
	
	float worldDepth = cameraHeight - (1.0 - terrainDepth) * far;
	return worldDepth;
}

//float GetWaterOrthoDepthBicubic(float2 uv)
//{
//	float cameraHeight = KWS_OrthoDepthNearFarSize.x;
//	float far = KWS_OrthoDepthNearFarSize.y;
//	float terrainDepth = KWS_WaterOrthoDepthRT.SampleLevel(sampler_linear_clamp, uv, 0).r;

//	float worldDepth = cameraHeight - (1.0 - terrainDepth) * far;
//	return worldDepth;
//}


float GetWaterOrthoDepthSDF(float2 uv)
{
	//if (IsOutsideUvBorders(uv)) return 0;
	return KWS_WaterOrthoDepthSDF.SampleLevel(sampler_linear_clamp, uv, 0).x;
}


float GetWaterOrthoDepth(float3 worldPos)
{
	float2 uv = GetWaterOrthoDepthUV(worldPos);
	return GetWaterOrthoDepth(uv);
}

float GetWaterOrthoDepthSDF(float3 worldPos)
{
	float2 uv = GetWaterOrthoDepthUV(worldPos);
	return GetWaterOrthoDepthSDF(uv);
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////









//////////////////////////////////////////////    Local Water Zones   /////////////////////////////////////////////////////////////
#define MAX_VISIBLE_LOCAL_ZONES 8

struct LocalZoneData
{
	float3 center;
	float3 halfSize;
	float2x2 rotationMatrix; // (c0.x, c0.y, c1.x, c1.y)
	uint id;
	float2 uv;

	float overrideColorSettings;
	float transparent;
	float4 waterColor;
	float4 turbidityColor;
	float useSphereBlending;
	
	float overrideWindSettings;
	float windStrengthMultiplier;
	float windEdgeBlending;
	        
	float  overrideHeight;
	float heightEdgeBlending;
	float clipWaterBelowZone;
};


StructuredBuffer<int> KWS_GlobalTileIndices_LocalZone;
StructuredBuffer<uint2> KWS_TileIndexRanges_LocalZone;
StructuredBuffer<LocalZoneData> KWS_ZoneData_LocalZone;


float2 _GridSize_LocalZone;
float3 _WorldMin_LocalZone;
float3 _WorldSize_LocalZone;
uint KWS_WaterLocalZonesCount;

bool GetTileRange_LocalZone(float3 worldPos, out uint offset, out uint count)
{
	offset = 0;
	count = 0;
	
	float3 localPos = worldPos - _WorldMin_LocalZone.xyz;
	float2 uvGrid = float2(localPos.x / _WorldSize_LocalZone.x, localPos.z / _WorldSize_LocalZone.z);

	int x = floor(uvGrid.x * _GridSize_LocalZone.x);
	int y = floor(uvGrid.y * _GridSize_LocalZone.y);

	if (x < 0 || x >= _GridSize_LocalZone.x || y < 0 || y >= _GridSize_LocalZone.y)
	{
		return false;
	}
	uint tileIndex = y * _GridSize_LocalZone.x + x;

	uint2 range = KWS_TileIndexRanges_LocalZone[tileIndex];
	offset = range.x;
	uint size = range.y;
	count = offset +size;

	return true;
}

bool GetWaterZone_LocalZone(float3 worldPos, uint idx, out LocalZoneData zone)
{
	zone = (LocalZoneData)0;
	
	uint zoneID = KWS_GlobalTileIndices_LocalZone[idx];
	if (zoneID == -1) return false;

	zone = KWS_ZoneData_LocalZone[zoneID];

	float2 pos = worldPos.xz - zone.center.xz;
	float2 localPos = mul(pos, zone.rotationMatrix);

	if (abs(localPos.x) > zone.halfSize.x || abs(localPos.y) > zone.halfSize.z)
		return false;

	zone.uv = localPos / zone.halfSize.xz * 0.5 + 0.5;

	return true;
}

float GetLocalWaterZoneBlendFactor(float2 uv, float blendFactor)
{
	uv = abs(uv * 2 - 1);
	float uvEdgeMask = max(uv.x, uv.y);
	uvEdgeMask = 1 - saturate(pow(uvEdgeMask * 1.01, blendFactor * 10));
	return uvEdgeMask;
}

float GetLocalWaterZoneSphereBlendFactor(float2 uv, float blendFactor)
{
	float2 localUV = uv * 2 - 1;
	float dist = length(localUV);
	float mask = 1 - saturate(pow(dist * 1.01, blendFactor * 4));
	return mask;
}

float GetLocalWaterZoneWindBlendFactor(float2 uv, float blendFactor)
{
	uv = abs(uv * 2 - 1);
	float uvEdgeMask = max(uv.x, uv.y);
	float fade = saturate(pow(uvEdgeMask * 1.01, blendFactor * 10));
	return lerp(1, fade, blendFactor);
}

void EvaluateBlendedZoneData(inout LocalZoneData blendedZone, float3 rayStart, float3 rayDir, float rayLengthToSurface, float surfaceHeight, float noise)
{
	UNITY_LOOP
	for (uint zoneIdx = 0; zoneIdx < KWS_WaterLocalZonesCount; zoneIdx++)
	{
		LocalZoneData zone = KWS_ZoneData_LocalZone[zoneIdx];
		float3 surfaceOffset = float3(0, max(0, zone.center.y + zone.halfSize.y - surfaceHeight) * 0.5f, 0);

		float tEntry;

		float2 distanceToBox = abs(mul((rayStart.xz - zone.center.xz), zone.rotationMatrix)) / zone.halfSize.xz;
		float distanceToBorder = max(distanceToBox.x, distanceToBox.y);
		float zoneMinHeight = zone.center.y - zone.halfSize.y;

		if (zone.overrideHeight > 0.5 && zone.clipWaterBelowZone && distanceToBorder < 1.1 && rayStart.y < zoneMinHeight && rayStart.y > KWS_WaterPosition.y)
		{
			blendedZone.transparent = 0;
			return;
		}
		
		if (zone.overrideColorSettings > 0.5)
		{
			float density = 0;
			if (zone.useSphereBlending > 0.5)
			{
				density = KWS_SDF_SphereDensity(rayStart, rayDir, zone.center, zone.halfSize.x, rayLengthToSurface, tEntry);
			}
			else
			{
				float2 boxSDF = KWS_SDF_IntersectionBox(rayStart , rayDir, zone.rotationMatrix, zone.center, zone.halfSize);
				density = boxSDF.x < boxSDF.y && boxSDF.y > 0 && boxSDF.x < rayLengthToSurface;
				tEntry = boxSDF.x;
			}
			
			
			if (density > 0)
			{
				density = saturate(density * 2);
				density = lerp(0, density, saturate(blendedZone.transparent / max(1, tEntry)));
			
				blendedZone.transparent = lerp(blendedZone.transparent, zone.transparent, density);
				blendedZone.turbidityColor = lerp(blendedZone.turbidityColor, zone.turbidityColor, density);
				blendedZone.waterColor = lerp(blendedZone.waterColor, zone.waterColor, density);
			}
		}
		
		
	}
}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////









//////////////////////////////////////////////    Dynamic waves    //////////////////////////////////////////////////////////
#define MAX_ZONES_PER_TILE 8


Texture2D KWS_DynamicWaves;
Texture2D KWS_DynamicWavesNormals;
Texture2D KWS_DynamicWavesLast;
Texture2D KWS_DynamicWavesAdditionalDataRT;
Texture2D KWS_DynamicWavesColorDataRT;
Texture2D KWS_DynamicWavesMaskRT;
Texture2D KWS_DynamicWavesMaskColorRT;
Texture2D KWS_DynamicWavesDepthMask;

Texture2D KWS_DynamicWavesMovable;
Texture2D KWS_DynamicWavesNormalsMovable;
Texture2D KWS_DynamicWavesAdditionalDataRTMovable;
Texture2D KWS_DynamicWavesDepthMaskMovable;


Texture2D KWS_DynamicWaves0;
Texture2D KWS_DynamicWaves1;
Texture2D KWS_DynamicWaves2;
Texture2D KWS_DynamicWaves3;
Texture2D KWS_DynamicWaves4;
Texture2D KWS_DynamicWaves5;
Texture2D KWS_DynamicWaves6;
Texture2D KWS_DynamicWaves7;

Texture2D KWS_DynamicWavesNormals0;
Texture2D KWS_DynamicWavesNormals1;
Texture2D KWS_DynamicWavesNormals2;
Texture2D KWS_DynamicWavesNormals3;
Texture2D KWS_DynamicWavesNormals4;
Texture2D KWS_DynamicWavesNormals5;
Texture2D KWS_DynamicWavesNormals6;
Texture2D KWS_DynamicWavesNormals7;

Texture2D KWS_DynamicWavesAdditionalDataRT0;
Texture2D KWS_DynamicWavesAdditionalDataRT1;
Texture2D KWS_DynamicWavesAdditionalDataRT2;
Texture2D KWS_DynamicWavesAdditionalDataRT3;
Texture2D KWS_DynamicWavesAdditionalDataRT4;
Texture2D KWS_DynamicWavesAdditionalDataRT5;
Texture2D KWS_DynamicWavesAdditionalDataRT6;
Texture2D KWS_DynamicWavesAdditionalDataRT7;

Texture2D KWS_DynamicWavesColorDataRT0;
Texture2D KWS_DynamicWavesColorDataRT1;
Texture2D KWS_DynamicWavesColorDataRT2;
Texture2D KWS_DynamicWavesColorDataRT3;
Texture2D KWS_DynamicWavesColorDataRT4;
Texture2D KWS_DynamicWavesColorDataRT5;
Texture2D KWS_DynamicWavesColorDataRT6;
Texture2D KWS_DynamicWavesColorDataRT7;

Texture2D KWS_DynamicWavesDepthMask0;
Texture2D KWS_DynamicWavesDepthMask1;
Texture2D KWS_DynamicWavesDepthMask2;
Texture2D KWS_DynamicWavesDepthMask3;
Texture2D KWS_DynamicWavesDepthMask4;
Texture2D KWS_DynamicWavesDepthMask5;
Texture2D KWS_DynamicWavesDepthMask6;
Texture2D KWS_DynamicWavesDepthMask7;

float4 KWS_DynamicWaves0_TexelSize;
float4 KWS_DynamicWaves1_TexelSize;
float4 KWS_DynamicWaves2_TexelSize;
float4 KWS_DynamicWaves3_TexelSize;
float4 KWS_DynamicWaves4_TexelSize;
float4 KWS_DynamicWaves5_TexelSize;
float4 KWS_DynamicWaves6_TexelSize;
float4 KWS_DynamicWaves7_TexelSize;

float4 KWS_DynamicWaves_TexelSize;
float4 KWS_DynamicWavesDepthMask_TexelSize;
float4 KWS_DynamicWavesAdditionalDataRT_TexelSize;
float KWS_DynamicWavesTimeScale;

float3 KWS_DynamicWavesZonePosition;
float3 KWS_DynamicWavesZoneSize;
float4 KWS_DynamicWavesZoneRotationMatrix;

float3 KWS_DynamicWavesZonePositionMovable;
float3 KWS_DynamicWavesZoneSizeMovable;
float4 KWS_DynamicWavesZoneBoundsMovable;
float4 KWS_DynamicWavesOrthoDepthNearFarSizeMovable;

float3 KWS_DynamicWavesZonePositionArray[MAX_ZONES_PER_TILE];
float3 KWS_DynamicWavesZoneSizeArray[MAX_ZONES_PER_TILE];
float4 KWS_DynamicWavesOrthoDepthNearFarSizeArray[MAX_ZONES_PER_TILE];
float4 KWS_DynamicWavesZoneRotationMatrixArray[MAX_ZONES_PER_TILE];



#define DYNAMIC_WAVE_COLOR_MAX_TRANSPARENT 4
#define DYNAMIC_WAVE_PROCEDURAL_MASK_TYPE_SPHERE 1

#define KWS_DYNAMIC_WAVE_PARTICLE_TYPE_FOAM 0
#define KWS_DYNAMIC_WAVE_PARTICLE_TYPE_SPLASH 1
#define KWS_DYNAMIC_WAVE_PARTICLE_TYPE_SPLASH_SURFACE 2


struct KWS_DynamicWavesMask
{
	uint zoneInteractionType;
	float force;
	float waterHeight;
	uint useColor;

	float4 size;
	float4 position;

	float3 forceDirection;
	uint useWaterIntersection;

	float4 color;
	float4x4 matrixTRS;
};
StructuredBuffer<KWS_DynamicWavesMask> KWS_DynamicWavesMaskBuffer;

struct FoamParticle
{
	float3 position;
	float initialRandom01;

	float3 prevPosition;
	float prevLifetime;

	float3 velocity;
	float currentLifetime;

	float4 color;
	float4 prevColor;

	float isFreeMoving;
	float shorelineMask;
	float maxLifeTime;
	float _pad;
};

struct SplashParticle
{
	float initialRandom01;
	float3 position;

	float3 velocity;
	float currentLifetime;

	float shorelineMask;
	float distanceToSurface;
	float uvOffset;
	float initialSpeed;

	float3 prevPosition;
	float prevLifetime;
};
StructuredBuffer<SplashParticle> KWS_SplashParticlesBuffer;
StructuredBuffer<FoamParticle> KWS_FoamParticlesBuffer;


struct ZoneData
{
	float3 center;
	float3 halfSize;
	float2x2 rotationMatrix; // (c0.x, c0.y, c1.x, c1.y)
	uint id;
	float2 uv;
	float flowSpeedMultiplier;
};


StructuredBuffer<int> KWS_GlobalTileIndices;
StructuredBuffer<uint2> KWS_TileIndexRanges;
StructuredBuffer<ZoneData> KWS_ZoneData;


float2 _GridSize;
float3 _WorldMin;
float3 _WorldSize;
uint KWS_WaterDynamicWavesZonesCount;
float KWS_MovableZoneFlowSpeedMultiplier;

bool GetTileRange(float3 worldPos, out uint offset, out uint count)
{
	offset = 0;
	count = 0;
	float3 localPos = worldPos - _WorldMin.xyz;
	float2 uvGrid = float2(localPos.x / _WorldSize.x, localPos.z / _WorldSize.z);

	int x = floor(uvGrid.x * _GridSize.x);
	int y = floor(uvGrid.y * _GridSize.y);

	//if (x < 0 || x >= _GridSize.x || y < 0 || y >= _GridSize.y)
	//{
	//	return false;
	//}
	uint tileIndex = y * _GridSize.x + x;

	uint2 range = KWS_TileIndexRanges[tileIndex];
	offset = range.x;
	uint size = range.y;
	count = offset +size;

	return true;
}

bool GetWaterZone(float3 worldPos, uint idx, out ZoneData zone)
{
	zone = (ZoneData)0;
	uint zoneID = KWS_GlobalTileIndices[idx];
	if (zoneID == -1) return false;

	zone = KWS_ZoneData[zoneID];

	float2 pos = worldPos.xz - zone.center.xz;
	//float2x2 invRot = float2x2(
	//	zone.rotMatrix.x, zone.rotMatrix.y,
	//	zone.rotMatrix.z, zone.rotMatrix.w
	//);

	float2 localPos = mul(pos, zone.rotationMatrix);

	if (abs(localPos.x) > zone.halfSize.x || abs(localPos.y) > zone.halfSize.z)
		return false;

	zone.uv = localPos / zone.halfSize.xz * 0.5 + 0.5;

	return true;
}

inline float2 RotateDynamicWavesCoord(float2 coord)
{
	return float2(dot(coord, KWS_DynamicWavesZoneRotationMatrix.xy), dot(coord, KWS_DynamicWavesZoneRotationMatrix.zw));
}

inline float2 RotateDynamicWavesCoord(uint id, float2 coord)
{
	float4 mat = KWS_DynamicWavesZoneRotationMatrixArray[id];
	float2x2 invRot = float2x2(mat.x, mat.z, mat.y, mat.w);
	float2 rotated = mul(coord, invRot);

	return rotated;
}


inline float2 RotateDynamicWavesCoordInverse(float2 coord)
{
	return float2(dot(coord, KWS_DynamicWavesZoneRotationMatrix.xz), dot(coord, KWS_DynamicWavesZoneRotationMatrix.yw));
}

inline float2 GetDynamicWavesUV(float3 worldPos)
{
	return (worldPos.xz - KWS_DynamicWavesZonePosition.xz) / KWS_DynamicWavesZoneSize.xz + 0.5;
}

inline float2 GetDynamicWavesUVRotated(float3 worldPos)
{
	float2 local = worldPos.xz - KWS_DynamicWavesZonePosition.xz;

	float2x2 invRot = float2x2(
		KWS_DynamicWavesZoneRotationMatrix.x, KWS_DynamicWavesZoneRotationMatrix.z,
		KWS_DynamicWavesZoneRotationMatrix.y, KWS_DynamicWavesZoneRotationMatrix.w
	);

	float2 rotated = mul(invRot, local);
	return rotated / KWS_DynamicWavesZoneSize.xz + 0.5;
}


bool GetWaterZoneMovable(float3 worldPos, out float2 uv)
{
	uv = 0;

	float4 zoneBounds = KWS_DynamicWavesZoneBoundsMovable;
	if (worldPos.x <= zoneBounds.x || worldPos.x >= zoneBounds.z ||
	worldPos.z <= zoneBounds.y || worldPos.z >= zoneBounds.w)
	{
		return false;
	}

	uv.x = (worldPos.x - zoneBounds.x) / (zoneBounds.z - zoneBounds.x);
	uv.y = (worldPos.z - zoneBounds.y) / (zoneBounds.w - zoneBounds.y);
	
	return true;
}

float GetWaterToZoneHeight(uint id)
{
	return abs(KWS_DynamicWavesZonePositionArray[id].y + KWS_DynamicWavesZoneSizeArray[id].y * 0.5 - KWS_WaterPosition.y);
}

float GetWaterToZoneHeight()
{
	return abs(KWS_DynamicWavesZonePosition.y + KWS_DynamicWavesZoneSize.y * 0.5 - KWS_WaterPosition.y);
}

float GetWaterToZoneHeightMovable()
{
	return abs(KWS_DynamicWavesZonePositionMovable.y + KWS_DynamicWavesZoneSizeMovable.y * 0.5 - KWS_WaterPosition.y);
}

float EncodeDynamicWavesHeight(float worldHeight)
{
	float minY = KWS_DynamicWavesZonePosition.y - KWS_DynamicWavesZoneSize.y;
	float maxY = KWS_DynamicWavesZonePosition.y + KWS_DynamicWavesZoneSize.y;
	return saturate((worldHeight - minY) / (maxY - minY));
}

float DecodeDynamicWavesHeight(uint id, float worldHeight)
{
	float zoneCenter = KWS_DynamicWavesZonePositionArray[id].y;
	float zoneSize = KWS_DynamicWavesZoneSizeArray[id].y;

	return lerp(zoneCenter - zoneSize, zoneCenter + zoneSize, worldHeight);
}

inline float4 GetDynamicWavesMask(float2 uv)
{
	float threshold = 4.0 / 255.0;
	float4 data = KWS_DynamicWavesMaskRT.SampleLevel(sampler_linear_clamp, uv, 0);
	data.xyz = data.xyz * 2 - 1;
	data.xyz *= step(threshold, abs(data.xyz));
	return data;
}

float4 PackSimulation(float4 value)
{
	value.xy = clamp(value.xy, -10, 10) / 20.0 + 0.5;
	value.z = (clamp(value.z, -2, 20) + 2) / 22.0;
	value.w = value.w / GetWaterToZoneHeight();
	return value;
}

float4 UnpackSimulation(float4 value)
{
	value.xy = (value.xy - 0.5) * 20.0;
	value.z = value.z * 22.0 - 2.0;
	value.w = value.w * GetWaterToZoneHeight();
	return value;
}


float4 UnpackSimulationMovable(float4 value)
{
	value.xy = (value.xy - 0.5) * 20.0;
	value.z = value.z * 22.0 - 2.0;
	value.w = value.w * GetWaterToZoneHeightMovable();
	return value;
}

float4 PackSimulation(uint id, float4 value)
{
	value.xy = clamp(value.xy, -10, 10) / 20.0 + 0.5;
	value.z = (clamp(value.z, -2, 20) + 2) / 22.0;
	value.w = value.w / GetWaterToZoneHeight(id);
	return value;
}

float4 UnpackSimulation(uint id, float4 value)
{
	value.xy = (value.xy - 0.5) * 20.0;
	value.z = value.z * 22.0 - 2.0;
	value.w = value.w * GetWaterToZoneHeight(id);
	return value;
}

inline float4 GetDynamicWavesMaskColor(float2 uv)
{
	return KWS_DynamicWavesMaskColorRT.SampleLevel(sampler_linear_clamp, uv, 0);
}

inline float4 GetDynamicWavesZone(float2 uv)
{
	float4 data = UnpackSimulation(KWS_DynamicWaves.SampleLevel(sampler_linear_clamp, uv, 0));
	data.xy = RotateDynamicWavesCoord(data.xy);
	return data;
}

inline float4 GetDynamicWavesZoneMovable(float2 uv)
{
	return UnpackSimulationMovable(KWS_DynamicWavesMovable.SampleLevel(sampler_linear_clamp, uv, 0));
}

inline float4 GetDynamicWavesZone(uint id, float2 uv)
{
	#if defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2)
		float4 rawData = SampleTextureArray2(KWS_DynamicWaves0, KWS_DynamicWaves1, id, uv);
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4)
		float4 rawData = SampleTextureArray4(KWS_DynamicWaves0, KWS_DynamicWaves1, KWS_DynamicWaves2, KWS_DynamicWaves3, id, uv);
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8)
		float4 rawData = SampleTextureArray8(KWS_DynamicWaves0, KWS_DynamicWaves1, KWS_DynamicWaves2, KWS_DynamicWaves3, KWS_DynamicWaves4, KWS_DynamicWaves5, KWS_DynamicWaves6, KWS_DynamicWaves7, id, uv);
	#else
		float4 rawData = KWS_DynamicWaves0.SampleLevel(sampler_linear_clamp, uv, 0);
	#endif

	
	rawData = UnpackSimulation(id, rawData);
	rawData.xy = RotateDynamicWavesCoord(id, rawData.xy);
	return rawData;
}


inline float4 GetDynamicWavesZoneBicubic(float2 uv)
{
	float4 data = UnpackSimulation(Texture2DSampleLevelBicubic(KWS_DynamicWaves, sampler_linear_clamp, uv, KWS_DynamicWaves_TexelSize, 0));
	data.xy = RotateDynamicWavesCoord(data.xy);
	return data;
}

inline float4 GetDynamicWavesZoneBicubic(uint id, float2 uv)
{
	#if defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2)
		float4 rawData = SampleTextureArray2_FirstBicubic(KWS_DynamicWaves0, KWS_DynamicWaves1, id, uv, KWS_DynamicWaves0_TexelSize);
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4)
		float4 rawData = SampleTextureArray4_FirstBicubic(KWS_DynamicWaves0, KWS_DynamicWaves1, KWS_DynamicWaves2, KWS_DynamicWaves3, id, uv, KWS_DynamicWaves0_TexelSize);
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8)
		float4 rawData = SampleTextureArray8_FirstBicubic(KWS_DynamicWaves0, KWS_DynamicWaves1, KWS_DynamicWaves2, KWS_DynamicWaves3, KWS_DynamicWaves4, KWS_DynamicWaves5, KWS_DynamicWaves6, KWS_DynamicWaves7, id, uv, KWS_DynamicWaves0_TexelSize);
	#else
		float4 rawData = Texture2DSampleLevelBicubic(KWS_DynamicWaves0, sampler_linear_clamp, uv, KWS_DynamicWaves0_TexelSize, 0);
	#endif

	rawData = UnpackSimulation(id, rawData);
	rawData.xy = RotateDynamicWavesCoord(id, rawData.xy);
	return rawData;
}



inline float4 GetDynamicWavesZoneAdditionalData(float2 uv)
{
	return KWS_DynamicWavesAdditionalDataRT.SampleLevel(sampler_linear_clamp, uv, 0);
}

inline float4 GetDynamicWavesZoneAdditionalDataMovable(float2 uv)
{
	return KWS_DynamicWavesAdditionalDataRTMovable.SampleLevel(sampler_linear_clamp, uv, 0);
}

inline float4 GetDynamicWavesZoneAdditionalData(uint id, float2 uv)
{
	#if defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2)
		float4 rawData = SampleTextureArray2(KWS_DynamicWavesAdditionalDataRT0, KWS_DynamicWavesAdditionalDataRT1, id, uv);
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4)
		float4 rawData = SampleTextureArray4(KWS_DynamicWavesAdditionalDataRT0, KWS_DynamicWavesAdditionalDataRT1, KWS_DynamicWavesAdditionalDataRT2, KWS_DynamicWavesAdditionalDataRT3, id, uv);
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8)
		float4 rawData = SampleTextureArray8(KWS_DynamicWavesAdditionalDataRT0, KWS_DynamicWavesAdditionalDataRT1, KWS_DynamicWavesAdditionalDataRT2,
		KWS_DynamicWavesAdditionalDataRT3, KWS_DynamicWavesAdditionalDataRT4, KWS_DynamicWavesAdditionalDataRT5, KWS_DynamicWavesAdditionalDataRT6, KWS_DynamicWavesAdditionalDataRT7, id, uv);
	#else
		float4 rawData = KWS_DynamicWavesAdditionalDataRT0.SampleLevel(sampler_linear_clamp, uv, 0);
	#endif
	
	return rawData;
}

inline float4 GetDynamicWavesZoneAdditionalDataBicubic(uint id, float2 uv)
{
	#if defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2)
		float4 rawData = SampleTextureArray2_FirstBicubic(KWS_DynamicWavesAdditionalDataRT0, KWS_DynamicWavesAdditionalDataRT1, id, uv, KWS_DynamicWaves0_TexelSize);
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4)
		float4 rawData = SampleTextureArray4_FirstBicubic(KWS_DynamicWavesAdditionalDataRT0, KWS_DynamicWavesAdditionalDataRT1, KWS_DynamicWavesAdditionalDataRT2, KWS_DynamicWavesAdditionalDataRT3, id, uv, KWS_DynamicWaves0_TexelSize);
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8)
		float4 rawData = SampleTextureArray8_FirstBicubic(KWS_DynamicWavesAdditionalDataRT0, KWS_DynamicWavesAdditionalDataRT1, KWS_DynamicWavesAdditionalDataRT2,
		KWS_DynamicWavesAdditionalDataRT3, KWS_DynamicWavesAdditionalDataRT4, KWS_DynamicWavesAdditionalDataRT5, KWS_DynamicWavesAdditionalDataRT6, KWS_DynamicWavesAdditionalDataRT7, id, uv, KWS_DynamicWaves0_TexelSize);
	#else
		float4 rawData = Texture2DSampleLevelBicubic(KWS_DynamicWavesAdditionalDataRT0, sampler_linear_clamp, uv, KWS_DynamicWaves0_TexelSize, 0);
	#endif
	
	return rawData;
}

inline float4 GetDynamicWavesZoneAdditionalDataBicubic(float2 uv)
{
	return Texture2DSampleLevelBicubic(KWS_DynamicWavesAdditionalDataRT, sampler_linear_clamp, uv, KWS_DynamicWavesAdditionalDataRT_TexelSize, 0);
}

inline float4 GetDynamicWavesZoneColorData(float2 uv)
{
	return KWS_DynamicWavesColorDataRT.SampleLevel(sampler_linear_clamp, uv, 0);
}

inline float4 GetDynamicWavesZoneColorData(uint id, float2 uv)
{
	#if defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2)
		float4 rawData = SampleTextureArray2(KWS_DynamicWavesColorDataRT0, KWS_DynamicWavesColorDataRT1, id, uv);
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4)
		float4 rawData = SampleTextureArray4(KWS_DynamicWavesColorDataRT0, KWS_DynamicWavesColorDataRT1, KWS_DynamicWavesColorDataRT2, KWS_DynamicWavesColorDataRT3, id, uv);
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8)
		float4 rawData = SampleTextureArray8(KWS_DynamicWavesColorDataRT0, KWS_DynamicWavesColorDataRT1, KWS_DynamicWavesColorDataRT2, KWS_DynamicWavesColorDataRT3, KWS_DynamicWavesColorDataRT4, KWS_DynamicWavesColorDataRT5, KWS_DynamicWavesColorDataRT6, KWS_DynamicWavesColorDataRT7, id, uv);
	#else
		float4 rawData = KWS_DynamicWavesColorDataRT0.SampleLevel(sampler_linear_clamp, uv, 0);
	#endif

	return rawData;
}

inline float3 GetDynamicWavesZoneNormals(float2 uv)
{
	return KWS_DynamicWavesNormals.SampleLevel(sampler_linear_clamp, uv, 0).xyz;
}

inline float3 GetDynamicWavesZoneNormalsMovable(float2 uv)
{
	return KWS_DynamicWavesNormalsMovable.SampleLevel(sampler_linear_clamp, uv, 0).xyz;
}


inline float3 GetDynamicWavesZoneNormals(uint id, float2 uv)
{
	#if defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2)
		float3 rawData = SampleTextureArray2(KWS_DynamicWavesNormals0, KWS_DynamicWavesNormals1, id, uv).xyz;
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4)
		float3 rawData = SampleTextureArray4(KWS_DynamicWavesNormals0, KWS_DynamicWavesNormals1, KWS_DynamicWavesNormals2, KWS_DynamicWavesNormals3, id, uv).xyz;
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8)
		float3 rawData = SampleTextureArray8(KWS_DynamicWavesNormals0, KWS_DynamicWavesNormals1, KWS_DynamicWavesNormals2, KWS_DynamicWavesNormals3, KWS_DynamicWavesNormals4, KWS_DynamicWavesNormals5, KWS_DynamicWavesNormals6, KWS_DynamicWavesNormals7, id, uv).xyz;
	#else
		float3 rawData = KWS_DynamicWavesNormals0.SampleLevel(sampler_linear_clamp, uv, 0).xyz;
	#endif

	return rawData;
}

inline float3 GetDynamicWavesZoneNormalsBicubic(uint id, float2 uv)
{
	#if defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2)
		float3 rawData = SampleTextureArray2_FirstBicubic(KWS_DynamicWavesNormals0, KWS_DynamicWavesNormals1, id, uv, KWS_DynamicWaves0_TexelSize).xyz;
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4)
		float3 rawData = SampleTextureArray4_FirstBicubic(KWS_DynamicWavesNormals0, KWS_DynamicWavesNormals1, KWS_DynamicWavesNormals2, KWS_DynamicWavesNormals3, id, uv, KWS_DynamicWaves0_TexelSize).xyz;
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8)
		float3 rawData = SampleTextureArray8_FirstBicubic(KWS_DynamicWavesNormals0, KWS_DynamicWavesNormals1, KWS_DynamicWavesNormals2, KWS_DynamicWavesNormals3, KWS_DynamicWavesNormals4, KWS_DynamicWavesNormals5, KWS_DynamicWavesNormals6, KWS_DynamicWavesNormals7, id, uv, KWS_DynamicWaves0_TexelSize).xyz;
	#else
		
		float3 rawData = Texture2DSampleLevelBicubic(KWS_DynamicWavesNormals0, sampler_linear_clamp, uv, KWS_DynamicWaves0_TexelSize, 0).xyz;
	#endif

	return rawData;
}

inline float UnpackDepthMask(float maskDepth)
{
	if (maskDepth > 0)
	{
		float cameraHeight = KWS_OrthoDepthNearFarSize.x;
		float far = KWS_OrthoDepthNearFarSize.y;
		float near = 0.0001;

		float worldDepth = cameraHeight - far + near + maskDepth * far;
		return worldDepth;
	}
	else return -100000;
}

inline float UnpackDepthMaskMovable(float maskDepth)
{
	if (maskDepth > 0)
	{
		float cameraHeight = KWS_DynamicWavesOrthoDepthNearFarSizeMovable.x;
		float far = KWS_DynamicWavesOrthoDepthNearFarSizeMovable.y;
		float near = 0.0001;

		float worldDepth = cameraHeight - far + near + maskDepth * far;
		return worldDepth;
	}
	else return -100000;
}


inline float UnpackDepthMask(float maskDepth, uint id)
{
	if (id >= MAX_ZONES_PER_TILE) return -100000;  //fix Shader warning: use of potentially uninitialized variable (UnpackDepthMask)

	if (maskDepth > 0)
	{
		float4 nearFarSize = KWS_DynamicWavesOrthoDepthNearFarSizeArray[id];
		float cameraHeight = nearFarSize.x;
		float far = nearFarSize.y;
		float near = 0.0001;

		float worldDepth = cameraHeight - far + near + maskDepth * far;
		return worldDepth;
	}
	else return -100000;
}

inline float GetDynamicWavesZoneDepthMaskBicubic(float2 uv)
{
	//return 0;
	float maskDepth = Texture2DSampleLevelBicubic(KWS_DynamicWavesDepthMask, sampler_linear_clamp, uv, KWS_DynamicWavesDepthMask_TexelSize, 0).x;
	return UnpackDepthMask(maskDepth);
}

inline float GetDynamicWavesZoneDepthMask(float2 uv)
{
	//return 0;
	float maskDepth = KWS_DynamicWavesDepthMask.SampleLevel(sampler_linear_clamp, uv, 0).x;
	return UnpackDepthMask(maskDepth);
}

inline float GetDynamicWavesZoneDepthMaskMovable(float2 uv)
{	
	float maskDepth = KWS_DynamicWavesDepthMaskMovable.SampleLevel(sampler_linear_clamp, uv, 0).x;
	return UnpackDepthMaskMovable(maskDepth);
	
}

inline float GetDynamicWavesZoneDepthMask(uint id, float2 uv)
{
	#if defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2)
		float maskDepth = SampleTextureArray2(KWS_DynamicWavesDepthMask0, KWS_DynamicWavesDepthMask1, id, uv).x;
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4)
		float maskDepth = SampleTextureArray4(KWS_DynamicWavesDepthMask0, KWS_DynamicWavesDepthMask1, KWS_DynamicWavesDepthMask2, KWS_DynamicWavesDepthMask3, id, uv).x;
	#elif defined(KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8)
		float maskDepth = SampleTextureArray8(KWS_DynamicWavesDepthMask0, KWS_DynamicWavesDepthMask1, KWS_DynamicWavesDepthMask2, KWS_DynamicWavesDepthMask3, KWS_DynamicWavesDepthMask4, KWS_DynamicWavesDepthMask5, KWS_DynamicWavesDepthMask6, KWS_DynamicWavesDepthMask7, id, uv).x;
	#else
		float maskDepth = KWS_DynamicWavesDepthMask0.SampleLevel(sampler_linear_clamp, uv, 0).x;
	#endif

	return UnpackDepthMask(maskDepth);
}


inline float GetDynamicWavesBorderFading(float2 uv)
{
	uv = abs(uv * 2 - 1);
	float uvEdgeMask = max(uv.x, uv.y);
	uvEdgeMask = 1 - saturate(pow(uvEdgeMask * 1.01, 8));
	return uvEdgeMask;
}

inline float2 NormalizeDynamicWavesVelocity(float2 velocity)
{
	float flowStrength = max(length(velocity), 0.001);
	return (velocity / flowStrength) * saturate(1 - exp(-flowStrength));
}


float GetDynamicWavesHeightOffset(float3 worldPos)
{
	float3 disp = GetFftWavesHeight(worldPos, 3);

	float2 uv = GetDynamicWavesUV(worldPos);
	float4 dynamicWaves = GetDynamicWavesZone(uv);
	float zoneFade = GetDynamicWavesBorderFading(uv);

	float waterLevel = disp.y + dynamicWaves.z * zoneFade + dynamicWaves.w + KWS_WaterPosition.y;
	return waterLevel;
}

float GetDynamicWavesHeightOffset(float3 worldPos, float2 dynamicWavesUV, float4 dynamicWaves)
{
	float3 disp = GetFftWavesHeight(worldPos, 3);
	float zoneFade = GetDynamicWavesBorderFading(dynamicWavesUV);

	float waterLevel = disp.y + dynamicWaves.z * zoneFade + dynamicWaves.w + KWS_WaterPosition.y;
	return waterLevel;
}

float GetDynamicWavesWaterfallTreshold(float3 zoneNormal)
{
	float waterfallThreshold = saturate(2 * saturate(1 - dot(zoneNormal, float3(0, 1, 0))) - 0.05);
	return waterfallThreshold;
}

//////////////////////////////////////////////////////////////////////////////////////




//////////////////////////////////////////////   Underwater    //////////////////////////////////////////////////////////
#define UNDERWATER_DEPTH_SCALE 0.75




/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////




//////////////////////////////////////////////   Refraction    //////////////////////////////////////////////////////////

inline void FixRefractionSurfaceLeaking(float surfaceDepthZEye, float sceneZ, float sceneZEye, float2 screenUV, inout float refractedSceneZ, inout float refractedSceneZEye, inout float2 refractionUV)
{
	if (surfaceDepthZEye > refractedSceneZEye)
	{
		refractedSceneZ = sceneZ;
		refractedSceneZEye = sceneZEye;
		refractionUV = screenUV;
	}
}

half3 ComputeWaterRefractRay(half3 viewDir, half3 normal, half depth)
{
	half nv = dot(normal, viewDir);
	half v2 = dot(viewDir, viewDir);
	half knormal = (sqrt(((1.7689 - 1.0) * v2) / (nv * nv) + 1.0) - 1.0) * nv;
	half3 result = depth * (viewDir + (knormal * normal));
	return result;
}



inline float2 GetRefractedUV_IOR(float3 viewDir, half3 normal, float3 worldPos, float sceneZEye, float waterSurfaceZEye, float transparent)
{
	float zFix = saturate(sceneZEye - waterSurfaceZEye);
	float aproximatedDepth = min(transparent, KWS_RefractionAproximatedDepth * zFix);
	float3 refractedRay = ComputeWaterRefractRay(-viewDir, normal, aproximatedDepth);
	refractedRay.y *= 0.4; //fix information lost in the near camera
	
	float4 refractedClipPos = mul(UNITY_MATRIX_VP, float4(GetCameraRelativePosition(worldPos + refractedRay), 1.0));
	float4 refractionScreenPos = ComputeScreenPos(refractedClipPos);
	float2 uv = refractionScreenPos.xy / refractionScreenPos.w;

	/*float2 overflowUV = abs(uv * 2 - 1);
	float overflowFix = saturate(max(overflowUV.x, overflowUV.y) - 0.6);
	overflowFix *= overflowFix;
	overflowFix = lerp(0, overflowFix, zFix);
	uv = lerp(uv, uv * 0.5 + 0.25, overflowFix);*/
	
	if (uv.x >= 0.995) uv.x = 1.99 - uv.x;
	if (uv.y >= 0.995) uv.y = 1.99 - uv.y;
	if (uv.y <= 0) uv.y = -uv.y;
	if (uv.x <= 0) uv.x = -uv.x;
	
	return uv;
}

inline float2 GetRefractedUV_Simple(float2 uv, half3 normal)
{
	return uv + normal.xz * KWS_RefractionSimpleStrength * 0.5;
}
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////





float4 LocalPosToToClipPosOrtho(float3 vertex)
{
	float3 worldPos = GetCameraRelativePositionOrtho(mul(UNITY_MATRIX_M, float4(vertex.xyz, 1.0)).xyz);
	return mul(KWS_MATRIX_VP_ORTHO, float4(GetCameraAbsolutePosition(worldPos), 1));
}

float4 WorldPosToToClipPosOrtho(float3 worldPos)
{
	worldPos = GetCameraRelativePositionOrtho(worldPos);
	return mul(KWS_MATRIX_VP_ORTHO, float4(worldPos, 1));
}


inline half GetSurfaceToSceneFading(float zEye, float surfaceZEye, float multiplier)
{
	return saturate((zEye - surfaceZEye) * multiplier);
}

inline half GetWaterRawFade(float3 worldPos, float surfaceDepthZEye, float refractedSceneZEye, half surfaceMask, half depthAngleFix)
{
	return (refractedSceneZEye - surfaceDepthZEye) * lerp(depthAngleFix, 1, unity_OrthoParams.w);
}




//////////////////////////////////////////////  Scene Color Pass    //////////////////////////////////////////////////////////

float4 KWS_CameraOpaqueTextureAfterWaterPass_TexelSize;
float4 KWS_CameraOpaqueTextureAfterWaterPass_RTHandleScale;

DECLARE_TEXTURE(KWS_CameraOpaqueTextureAfterWaterPass);


inline float2 GetSceneColorAfterWaterPassNormalizedUV(float2 uv)
{
	float2 maxCoord = 1.0f - KWS_CameraOpaqueTextureAfterWaterPass_TexelSize.xy * 0.5;
	return min(uv, maxCoord) * KWS_CameraOpaqueTextureAfterWaterPass_RTHandleScale.xy;
}

inline half3 GetSceneColorAfterWaterPass(float2 uv)
{
	#ifdef KWS_HDRP
		return GetSceneColor(uv);
	#else
		return SAMPLE_TEXTURE_LOD(KWS_CameraOpaqueTextureAfterWaterPass, sampler_linear_clamp, GetSceneColorAfterWaterPassNormalizedUV(uv), 0).xyz;
	#endif
}






#endif