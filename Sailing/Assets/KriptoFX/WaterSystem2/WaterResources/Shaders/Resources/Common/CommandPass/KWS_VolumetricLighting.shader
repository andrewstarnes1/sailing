Shader "Hidden/KriptoFX/KWS/VolumetricLighting"
{
	HLSLINCLUDE

	#include "../KWS_WaterHelpers.cginc"

	#define CAUSTIC_LOD 3.5

	uint KWS_Frame;
	float KWS_VolumetricLightTemporalAccumulationFactor;
	float2 KWS_VolumetricLightDownscaleFactor;

	half MaxDistance;
	uint KWS_RayMarchSteps;
	half4 KWS_LightAnisotropy;

	float KWS_VolumeLightMaxDistance;
	float KWS_VolumeDepthFade;

	struct RaymarchData
	{
		float2 uv;
		float stepSize;
		float3 step;
		float offset;

		float3 currentPos;
		float3 rayStart;
		float3 rayEnd;
		float3 rayDir;
		float rayLength;
		float rayLengthToWaterZ;
		float rayLengthToSceneZ;
		bool surfaceMask;
		float waterHeight;
		float3 turbidityColor;
		float transparent;
		float causticStrength;
		float2 waterVolumeDepth;
	};

	struct vertexInput
	{
		uint vertexID : SV_VertexID;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};


	struct vertexOutput
	{
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
		UNITY_VERTEX_OUTPUT_STEREO
	};


	vertexOutput vert(vertexInput v)
	{
		vertexOutput o;
		UNITY_SETUP_INSTANCE_ID(v);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
		o.vertex = GetTriangleVertexPosition(v.vertexID);
		o.uv = GetTriangleUVScaled(v.vertexID);
		return o;
	}

	struct RaymarchResult
	{
		float4 DirLightScattering;
		float DirLightSurfaceShadow;
		float DirLightSceneShadow;

		float3 SurfaceLight;
		
		float4 AdditionalLightsScattering;
		float AdditionalLightsSceneAttenuation;
	};

	inline half MieScattering(float cosAngle)
	{
		return KWS_LightAnisotropy.w * (KWS_LightAnisotropy.x / (KWS_LightAnisotropy.y - KWS_LightAnisotropy.z * cosAngle));
	}




	half RaymarchCaustic(RaymarchData raymarchData, float3 currentPos, float3 lightForward)
	{
		const uint causticSlice = 0;

		float angle = dot(float3(0, -0.999, 0), lightForward);
		float cameraHeight = GetCameraAbsolutePosition().y;
		float vericalOffset = cameraHeight * 0.5 * (1 - raymarchData.surfaceMask);
		float offsetLength = (raymarchData.waterHeight - currentPos.y) / angle;
		float2 uv = (currentPos.xz - offsetLength * lightForward.xz) / GetDomainSize(causticSlice, 1);
		half caustic = GetCausticLod(uv, causticSlice, CAUSTIC_LOD) - KWS_CAUSTIC_MULTIPLIER;
		
		float causticOverScaleFade = 1 - saturate((cameraHeight - raymarchData.waterHeight) * 0.025);
		float causticOverScale = saturate((raymarchData.transparent - 1) * 0.15) * causticOverScaleFade * 0.35;
		caustic *= lerp(1, causticOverScale, raymarchData.surfaceMask);
		caustic *= clamp(raymarchData.causticStrength, 1, 5);

		float distanceToCamera = GetWorldToCameraDistance(currentPos);
		caustic = lerp(caustic, 0, saturate(distanceToCamera * 0.005));
		return caustic;
	}

	void IntegrateLightSlice(inout float3 finalScattering, inout float transmittance, float atten, float rayLength)
	{
		float sliceDensity = KWS_VOLUME_LIGHT_SLICE_DENSITY / rayLength;
		float sliceTransmittance = exp(-sliceDensity / (float)KWS_RayMarchSteps);
		float3 sliceLightIntegral = atten * (1.0 - sliceTransmittance);
		finalScattering += max(0, sliceLightIntegral * transmittance);
		transmittance *= sliceTransmittance;
	}


	inline void UpdateLocalZones(inout RaymarchData raymarchData)
	{
		#if defined(KWS_USE_LOCAL_WATER_ZONES)
			
			float noise = InterleavedGradientNoise(raymarchData.uv * _ScreenParams.xy, KWS_Time) * 2 - 1;
		
			LocalZoneData zone = (LocalZoneData)0;
			zone.transparent = raymarchData.transparent;
			zone.turbidityColor.xyz = raymarchData.turbidityColor;
		
			EvaluateBlendedZoneData(zone, raymarchData.rayStart, raymarchData.rayDir, raymarchData.rayLengthToWaterZ, raymarchData.waterHeight + 20, noise);
			if (zone.transparent > 0.01)
			{
				raymarchData.transparent = zone.transparent;
				raymarchData.turbidityColor.xyz = zone.turbidityColor.xyz;
			
			}
			
		#endif
		
	}

	RaymarchData InitRaymarchData(vertexOutput i, float waterMask)
	{
		RaymarchData data = (RaymarchData)0;

		float sceneZ = GetSceneDepth(i.uv);
		data.waterVolumeDepth = GetWaterVolumeDepth(i.uv, sceneZ, waterMask);

		float3 startPos = GetWorldSpacePositionFromDepth(i.uv, data.waterVolumeDepth.x);
		float3 endPos = GetWorldSpacePositionFromDepth(i.uv, data.waterVolumeDepth.y);
		
		data.rayStart = startPos;
		data.rayEnd = endPos;
		data.rayDir = normalize(endPos - startPos);

		data.surfaceMask = GetSurfaceMask(waterMask);
		
		//WaterZonesData waterZonesData = GetWaterZonesData(data);

		data.waterHeight = KWS_WaterPosition.y;
		data.turbidityColor = KWS_TurbidityColor;
		data.transparent = KWS_Transparent;
		
		data.transparent = clamp(data.transparent + KWS_UnderwaterTransparentOffset * (1 - data.surfaceMask), 1, KWS_MAX_TRANSPARENT * 2);
		data.causticStrength = KWS_CausticStrength;

		data.rayLength = GetMaxRayDistanceRelativeToTransparent(data.transparent);
		data.rayLengthToWaterZ = length(startPos - endPos);
		data.rayLengthToSceneZ = length(startPos - GetWorldSpacePositionFromDepth(i.uv, sceneZ));

		data.stepSize = data.rayLength / (float)KWS_RayMarchSteps;
		data.step = data.rayDir * data.stepSize;
		data.offset = InterleavedGradientNoise(i.vertex.xy, KWS_Time);

		data.currentPos = data.rayStart + data.step * data.offset;
		data.uv = i.uv;

		UpdateLocalZones(data);

		return data;
	}

	float3 ComputeTemporalAccumulation(float3 worldPos, float3 color)
	{
		if (KWS_Frame > 5)
		{
			float2 reprojectedUV = WorldPosToScreenPosReprojectedPrevFrame(worldPos, 0).xy;
			float3 lastColor = GetVolumetricLightLastFrame(reprojectedUV).xyz;
			return lerp(color, lastColor, KWS_VolumetricLightTemporalAccumulationFactor);
		}
		else return color;
	}

	inline void IntegrateAdditionalLight(RaymarchData raymarchData, inout float3 scattering, inout float transmittance, float atten, float3 lightPos, float3 step, inout float3 currentPos)
	{
		float3 posToLight = normalize(GetCameraRelativePosition(currentPos) - lightPos.xyz);
		
		#if defined(KWS_USE_ADDITIONAL_CAUSTIC)
			if (GetCameraAbsolutePosition(lightPos).y > raymarchData.waterHeight)
			{
				atten += atten * RaymarchCaustic(raymarchData, currentPos, posToLight);
			}
		#endif
		
		half cosAngle = dot(-raymarchData.rayDir, posToLight);
		atten += atten * MieScattering(cosAngle) * 5;

		IntegrateLightSlice(scattering, transmittance, atten, raymarchData.rayLength);
		currentPos += step;
	}


	
	
	#pragma multi_compile_fragment _ KWS_USE_CAUSTIC KWS_USE_ADDITIONAL_CAUSTIC
	#pragma multi_compile_fragment _ KWS_USE_LOCAL_WATER_ZONES
	#define KWS_USE_DIR_SHADOW
	#define KWS_USE_ADDITIONAL_SHADOW

	
	#ifdef KWS_BUILTIN

		#pragma multi_compile_fragment _ KWS_USE_DIR_LIGHT
		#pragma multi_compile_fragment _ KWS_USE_POINT_LIGHTS
		#pragma multi_compile_fragment _ KWS_USE_SPOT_LIGHTS
		#pragma multi_compile_fragment _ KWS_USE_SHADOW_POINT_LIGHTS
		#pragma multi_compile_fragment _ KWS_USE_SHADOW_SPOT_LIGHTS
		#ifdef SHADER_API_VULKAN
			#define KWS_DISABLE_POINT_SPOT_SHADOWS
		#endif

		#include "../../PlatformSpecific/KWS_LightingHelpers.cginc"
		#include "../../PlatformSpecific/KWS_VolumetricLighting_Builtin.cginc"
	#endif

	
	#ifdef KWS_URP

		#pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
		#pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
		#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
		
		#pragma multi_compile_fragment _ _FORWARD_PLUS
		#pragma multi_compile_fragment _ _LIGHT_LAYERS

		#include "../../PlatformSpecific/KWS_LightingHelpers.cginc"
		#include "../../PlatformSpecific/KWS_VolumetricLighting_URP.cginc"
	#endif


	#ifdef KWS_HDRP
		
		#pragma multi_compile_fragment _ SUPPORT_LOCAL_LIGHTS
		#define MAX_VOLUMETRIC_LIGHT_ITERATIONS 8

		#include "../../PlatformSpecific/KWS_LightingHelpers.cginc"
		#include "../../PlatformSpecific/KWS_VolumetricLighting_HDRP.cginc"
		
	#endif
	



	ENDHLSL




	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always

		//Volume Light Pass : 0
		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.6

			

			half4 frag(vertexOutput i) : SV_Target0
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				float waterMask = GetWaterMask(i.uv);
				if (waterMask == 0) discard;
				
				RaymarchData raymarchData = InitRaymarchData(i, waterMask);
				RaymarchResult raymarchResult = (RaymarchResult)0;

				//if (raymarchData.transparent < 0.001) discard;

				RayMarchDirLight(raymarchData, raymarchResult);
				RayMarchAdditionalLights(raymarchData, raymarchResult);
				
				float4 volumeLight = 0;
				volumeLight.rgb = raymarchResult.DirLightScattering.rgb + raymarchResult.AdditionalLightsScattering.rgb + MIN_THRESHOLD;
				volumeLight.a = saturate((raymarchResult.DirLightScattering.a + raymarchResult.AdditionalLightsScattering.a) * 2);

				if (!KWS_IsEditorCamera) volumeLight.rgb = ComputeTemporalAccumulation(raymarchData.rayEnd, volumeLight.rgb);

				
				return volumeLight;
			}
			
			ENDHLSL
		}


		//Surface light pass : 1
		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.6

			
			void frag(vertexOutput i, out half3 additionalData : SV_Target0, out half3 surfaceLight : SV_Target1)
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				float waterMask = GetWaterMask(i.uv);
				if (waterMask == 0) discard;
				
				float sceneZ = GetSceneDepth(i.uv);
				float2 waterVolumeDepth = GetWaterVolumeDepth(i.uv, sceneZ, waterMask);

				if(waterVolumeDepth.x < 0.00001) discard;

				float3 startPos = GetWorldSpacePositionFromDepth(i.uv, waterVolumeDepth.x);
				float3 endPos = GetWorldSpacePositionFromDepth(i.uv, waterVolumeDepth.y);

				float4 fullSurfaceLight = KWS_ComputeLighting(startPos, 0, false, i.uv);
				surfaceLight = fullSurfaceLight.rgb + 0.001;

				additionalData.r = fullSurfaceLight.a;
				additionalData.g = KWS_ComputeDirLightShadow(endPos, 0, false, i.uv);
				additionalData.b = KWS_ComputeUnderwaterLightingAttenuation(endPos);
			}
			
			ENDHLSL
		}
	}
}