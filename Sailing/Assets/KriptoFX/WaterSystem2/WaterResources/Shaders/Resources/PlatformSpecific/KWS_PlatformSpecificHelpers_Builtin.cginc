#ifndef KWS_PLATFORM_SPECIFIC_HELPERS
#define KWS_PLATFORM_SPECIFIC_HELPERS

//#define ENVIRO_FOG
//#define ENVIRO_3_FOG
//#define AZURE_FOG
//#define WEATHER_MAKER
//#define ATMOSPHERIC_HEIGHT_FOG
//#define VOLUMETRIC_FOG_AND_MIST
//#define COZY_FOG
//#define COZY_FOG_3
//#define CURVED_WORLDS
//#define AURA2

//ATMOSPHERIC_HEIGHT_FOG also need to change the "Queue" = "Transparent-1"      -> "Queue" = "Transparent+2"
//VOLUMETRIC_FOG_AND_MIST also need to enable "Water->Rendering->DrawToDepth"

#define _FrustumCameraPlanes unity_CameraWorldClipPlanes
#define KWS_INITIALIZE_DEFAULT_MATRIXES float4x4 KWS_MATRIX_M = UNITY_MATRIX_M; float4x4 KWS_MATRIX_I_M = unity_WorldToObject;

#if defined(KWS_COMPUTE) || defined(KWS_SHARED_API_INCLUDED)
	#undef ENVIRO_3_FOG
	#undef WEATHER_MAKER
	#undef AURA2
	#undef COZY_FOG_3
#endif

#ifdef COZY_FOG_3
	#define _TimeParameters float4(_Time.y, _SinTime.w, _CosTime.w, 0)
	float4x4 GetObjectToWorldMatrix()
	{
		return unity_ObjectToWorld;
	}
#endif

//------------------  unity includes   ----------------------------------------------------------------

#ifndef HLSL_SUPPORT_INCLUDED
	#include "HLSLSupport.cginc"
#endif

#ifndef UNITY_CG_INCLUDED
	#include "UnityCG.cginc"
#endif
//-------------------------------------------------------------------------------------------------------




//------------------  thid party assets  ----------------------------------------------------------------

#if defined(ENVIRO_FOG)
	#include "Assets/Third-party assets/Enviro - Sky and Weather/Core/Resources/Shaders/Core/EnviroFogCore.cginc"
#endif

#if defined(ENVIRO_3_FOG)
	#include "Assets/Enviro 3 - Sky and Weather/Resources/Shader/Includes/FogInclude.cginc"
#endif

#if defined(AZURE_FOG)
	#include "Assets/Azure[Sky] Dynamic Skybox/Shaders/Transparent/AzureFogCore.cginc"
#endif

#if defined(WEATHER_MAKER)
	#include "Assets/WeatherMaker/Prefab/Shaders/WeatherMakerFogExternalShaderInclude.cginc"
#endif

#if defined(ATMOSPHERIC_HEIGHT_FOG)
	#include "Assets/BOXOPHOBIC/Atmospheric Height Fog/Core/Includes/AtmosphericHeightFog.cginc"
#endif

#if defined(VOLUMETRIC_FOG_AND_MIST)
	#include "Assets/VolumetricFog/Resources/Shaders/VolumetricFogOverlayVF.cginc"
#endif

#if defined(COZY_FOG)
	#include "Assets/Distant Lands/Cozy Weather/Contents/Materials/Shaders/Includes/StylizedFogIncludes.cginc"
#endif

#if defined(COZY_FOG_3)
	#include "Packages/com.distantlands.cozy.core/Runtime/Shaders/Includes/StylizedFogIncludes.cginc"
#endif

#if defined(AURA2)
	#include "Assets/Aura 2/Core/Code/Shaders/Includes/AuraUsage.cginc"
#endif

#if defined(CURVED_WORLDS)
	#define CURVEDWORLD_BEND_TYPE_LITTLEPLANET_Y
	#define CURVEDWORLD_BEND_ID_1
	#include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"
#endif
//-------------------------------------------------------------------------------------------------------


#ifndef KWS_WATER_VARIABLES
	#include "..\Common\KWS_WaterVariables.cginc"
#endif


DECLARE_TEXTURE(_CameraDepthTexture);

#ifndef SHADERGRAPH_PREVIEW
	DECLARE_TEXTURE(KWS_CameraOpaqueTexture);
#endif


SamplerState sampler_CameraDepthTexture;
float4 _CameraDepthTexture_TexelSize;

SamplerState samplerKWS_CameraOpaqueTexture;
float4 KWS_CameraOpaqueTexture_TexelSize;
float4 KWS_CameraOpaqueTexture_RTHandleScale;


TextureCube KWS_SkyTexture;
float4 KWS_SkyTexture_HDRDecodeValues;

TextureCube KWS_EnvCubemapTexture0;
TextureCube KWS_EnvCubemapTexture1;
TextureCube KWS_EnvCubemapTexture2;
TextureCube KWS_EnvCubemapTexture3;
TextureCube KWS_EnvCubemapTexture4;
TextureCube KWS_EnvCubemapTexture5;


inline float4x4 UpdateCameraRelativeMatrix(float4x4 matrixM)
{
	return matrixM;
}

inline float4 ObjectToClipPos(float4 vertex)
{
	#if defined(CURVEDWORLD_IS_INSTALLED) && !defined(CURVEDWORLD_DISABLED_ON)
		CURVEDWORLD_TRANSFORM_VERTEX(vertex)
	#endif

	return UnityObjectToClipPos(vertex);
}

inline float4 ObjectToClipPos(float4 vertex, float4x4 matrixM, float4x4 matrixIM)
{
	#if defined(CURVEDWORLD_IS_INSTALLED) && !defined(CURVEDWORLD_DISABLED_ON)
		#if defined(KWS_USE_WATER_INSTANCING)
			unity_ObjectToWorld = matrixM;
			unity_WorldToObject = matrixIM;
		#endif
		CURVEDWORLD_TRANSFORM_VERTEX(vertex)
	#endif

	#if defined(KWS_USE_WATER_INSTANCING)
		return mul(UNITY_MATRIX_VP, mul(matrixM, float4(vertex.xyz, 1.0)));
	#else
		return UnityObjectToClipPos(vertex);
	#endif
}


inline float2 GetTriangleUVScaled(uint vertexID)
{
	#if UNITY_UV_STARTS_AT_TOP
		return float2((vertexID << 1) & 2, 1.0 - (vertexID & 2));
	#else
		return float2((vertexID << 1) & 2, vertexID & 2);
	#endif
}


inline float4 GetTriangleVertexPosition(uint vertexID, float z = UNITY_NEAR_CLIP_VALUE)
{
	float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
	return float4(uv * 2.0 - 1.0, z, 1.0);
}

inline float2 GetNormalizedRTHandleUV(float2 screenUV)
{
	return screenUV;
}

inline float3 LocalToWorldPos(float3 localPos)
{
	return mul(UNITY_MATRIX_M, float4(localPos, 1)).xyz;
}

inline float3 LocalToWorldPos(float3 localPos, float4x4 matrixM)
{
	#if defined(KWS_USE_WATER_INSTANCING)
		return mul(matrixM, float4(localPos, 1));
	#else
		return LocalToWorldPos(localPos);
	#endif
}


inline float3 WorldToLocalPos(float3 worldPos)
{
	return mul(unity_WorldToObject, float4(worldPos, 1)).xyz;
}

inline float3 WorldToLocalPos(float3 worldPos, float4x4 matrixIM)
{
	#if defined(KWS_USE_WATER_INSTANCING)
		return mul(matrixIM, float4(worldPos, 1));
	#else
		return WorldToLocalPos(worldPos);
	#endif
}

inline float3 WorldToLocalPosWithoutTranslation(float3 worldPos)
{
	return mul((float3x3)unity_WorldToObject, worldPos);
}


inline float3 WorldToLocalPosWithoutTranslation(float3 worldPos, float4x4 matrixIM)
{
	#if defined(KWS_USE_WATER_INSTANCING)
		return mul((float3x3)matrixIM, worldPos);
	#else
		return WorldToLocalPosWithoutTranslation(worldPos);
	#endif
}


inline float3 GetCameraRelativePosition(float3 worldPos)
{
	return worldPos;
}

inline float3 GetCameraAbsolutePosition(float3 worldPos)
{
	return worldPos;
}

inline float3 GetCameraRelativePositionOrtho(float3 worldPos)
{
	return worldPos - KWS_WorldSpaceCameraPosOrtho.xyz;
}


inline float3 GetWorldSpaceViewDirNorm(float3 worldPos)
{
	return lerp(normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz), UNITY_MATRIX_IT_MV[2].xyz, unity_OrthoParams.w);
}

inline float3 GetCameraAbsolutePosition()
{
	return _WorldSpaceCameraPos.xyz;
	//return _WorldSpaceCameraPos.xyz; //cause shader error in 'Hidden/KriptoFX/KWS/VolumetricLighting': Program 'frag', error X8000: D3D11 Internal Compiler Error: Invalid Bytecode:
	//source register relative index temp register component 1 in r7 uninitialized. Opcode #61 (count is 1-based) at line 15 (on vulkan)
	//check return UNITY_MATRIX_I_V._m03_m13_m23;

}



inline float GetWorldToCameraDistance(float3 worldPos)
{
	return length(_WorldSpaceCameraPos.xyz - worldPos.xyz);
}



inline float3 GetWorldSpaceNormal(float3 normal)
{
	return (mul((float3x3)UNITY_MATRIX_M, normal)).xyz;
	//return (mul((float3x3)UNITY_MATRIX_M, normal)).xyz / UNITY_MATRIX_M._11_22_33;

}

inline float3 GetWorldSpaceNormal(float3 normal, float4x4 matrixM)
{
	#if defined(KWS_USE_WATER_INSTANCING)
		return normalize(mul((float3x3)matrixM, normal)).xyz;
	#else
		return GetWorldSpaceNormal(normal);
	#endif
}



float3 GetWorldSpacePositionFromDepth(float2 uv, float deviceDepth)
{
	float4 positionCS = float4(uv * 2.0 - 1.0, deviceDepth, 1.0);
	#if UNITY_UV_STARTS_AT_TOP
		positionCS.y = -positionCS.y;
	#endif
	
	float4 hpositionWS = mul(KWS_MATRIX_I_VP, positionCS);
	return hpositionWS.xyz / hpositionWS.w;
}

inline float GetSceneDepth(float2 uv)
{
	float rawDepth = SAMPLE_TEXTURE_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0).x;
	return rawDepth;
	float orthoLinearDepth = _ProjectionParams.x > 0 ?        rawDepth : 1 - rawDepth;
	return lerp(_ProjectionParams.y, _ProjectionParams.z, orthoLinearDepth);
}


inline float3 GetAmbientColor(float exposure)
{
	return KWS_AmbientColor;
}

inline float2 GetSceneColorNormalizedUV(float2 uv)
{
	//	return uv * Test4.xy;

	float2 maxCoord = 1.0f - KWS_CameraOpaqueTexture_TexelSize.xy * 0.5;
	return min(uv, maxCoord) * KWS_CameraOpaqueTexture_RTHandleScale.xy;

	//todo add define for mutliple cameras using mirror reflection
	//if(uv.x >= 0.995) uv.x = 1.99-uv.x;
	//if(uv.y >= 0.995) uv.y = 1.99-uv.y;
	//if(uv.y <= 0) uv.y = -uv.y;
	//if(uv.x <= 0) uv.x = -uv.x;
	//return uv * KWS_CameraOpaqueTexture_RTHandleScale.xy;

}


inline half3 GetSceneColor(float2 uv)
{
	return SAMPLE_TEXTURE_LOD(KWS_CameraOpaqueTexture, samplerKWS_CameraOpaqueTexture, GetSceneColorNormalizedUV(uv), 0).xyz;
}

inline half3 GetSceneColor(float2 uv, float2 offset)
{
	uv += offset * KWS_CameraOpaqueTexture_TexelSize.xy;
	return SAMPLE_TEXTURE_LOD(KWS_CameraOpaqueTexture, samplerKWS_CameraOpaqueTexture, GetSceneColorNormalizedUV(uv), 0).xyz;
}

inline half3 GetSceneColorPoint(float2 uv)
{
	return SAMPLE_TEXTURE_LOD(KWS_CameraOpaqueTexture, sampler_point_clamp, GetSceneColorNormalizedUV(uv), 0).xyz;
	//return LOAD_TEXTURE(KWS_CameraOpaqueTexture, GetSceneColorNormalizedUV(uv) * KWS_CameraOpaqueTexture_TexelSize.zw);

}


inline half3 GetSceneColorWithDispersion(float2 uv, float dispersionStrength)
{
	half3 refraction;
	refraction.r = GetSceneColor(uv - KWS_CameraOpaqueTexture_TexelSize.xy * dispersionStrength).r;
	refraction.g = GetSceneColor(uv).g;
	refraction.b = GetSceneColor(uv + KWS_CameraOpaqueTexture_TexelSize.xy * dispersionStrength).b;
	return refraction;
}

inline float3 ScreenPosToWorldPos(float2 uv)
{
	float depth = GetSceneDepth(uv);
	float3 posWS = GetWorldSpacePositionFromDepth(uv, depth);
	return posWS;
}

inline float4 WorldPosToScreenPos(float3 pos)
{
	float4 projected = mul(KWS_MATRIX_VP, float4(pos, 1.0f));
	projected.xy = (projected.xy / projected.w) * 0.5f + 0.5f;
	#ifdef UNITY_UV_STARTS_AT_TOP
		projected.y = 1 - projected.y;
	#endif
	return projected;
}

inline float3 KWS_WorldPosToViewPos(float3 pos)
{
	return mul(UNITY_MATRIX_V, float4(pos, 1)).xyz;
}

inline float3 KWS_WorldPosToViewDir(float3 dir)
{
	return mul((float3x3)UNITY_MATRIX_V, dir).xyz;
}



inline float2 KWS_ViewToScreenPos(float3 viewPos)
{
	float4 positionCS = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));

	#if UNITY_UV_STARTS_AT_TOP
		positionCS.y = -positionCS.y;
	#endif

	positionCS *= rcp(positionCS.w);
	positionCS.xy = positionCS.xy * 0.5 + 0.5;

	return positionCS.xy;
}

inline float3 KWS_DepthToViewPos(float2 uv, float depth)
{
	float4 positionCS = float4(uv * 2.0 - 1.0, depth, 1.0);
	#if UNITY_UV_STARTS_AT_TOP
		positionCS.y = -positionCS.y;
	#endif
	
	float4 hpositionWS = mul(KWS_MATRIX_I_P, positionCS);
	return hpositionWS.xyz / hpositionWS.w;
}

inline float2 WorldPosToScreenPosReprojectedPrevFrame(float3 pos, float2 texelSize)
{
	float4 projected = mul(KWS_PREV_MATRIX_VP, float4(pos, 1.0f));
	float2 uv = (projected.xy / projected.w) * 0.5f + 0.5f;
	#ifdef UNITY_UV_STARTS_AT_TOP
		uv.y = 1 - uv.y;
	#endif
	return uv + texelSize * 0.5;
}




inline void GetInternalFogVariables(float4 pos, float3 viewDir, float surfaceDepthZ, float surfaceDepthZEye, float3 worldPos, out half3 fogColor, out half3 fogOpacity)
{
	if (KWS_FogState > 0)
	{
		float fogFactor = 0 ;
		
		if (KWS_FogState == 1) fogFactor = surfaceDepthZEye * unity_FogParams.z + unity_FogParams.w;
		else if (KWS_FogState == 2) fogFactor = exp2(-unity_FogParams.y * surfaceDepthZEye);
		else if (KWS_FogState == 3) fogFactor = exp2(-unity_FogParams.x * surfaceDepthZEye * unity_FogParams.x * surfaceDepthZEye);

		fogOpacity = float3(1, 1, 1) - saturate(fogFactor);
		fogColor = unity_FogColor.xyz;
	}
	else
	{
		fogOpacity = half3(0.0, 0.0, 0.0);
		fogColor = half3(0.0, 0.0, 0.0);
	}
}

inline half3 ComputeInternalFog(half3 sourceColor, half3 fogColor, half3 fogOpacity)
{
	return lerp(sourceColor, lerp(sourceColor, fogColor, fogOpacity), saturate(KWS_FogState));
}

inline half3 ComputeThirdPartyFog(half3 sourceColor, float3 worldPos, float2 screenUV, float screenPosZ)
{
	#if defined(ENVIRO_FOG)
		sourceColor = TransparentFog(half4(sourceColor, 1.0), worldPos.xyz, screenUV, Linear01Depth(screenPosZ));
	#elif defined(ENVIRO_3_FOG)
		sourceColor = ApplyFogAndVolumetricLights(sourceColor, screenUV, worldPos.xyz, Linear01Depth(screenPosZ));
	#elif defined(AZURE_FOG)
		sourceColor = ApplyAzureFog(half4(sourceColor, 1.0), worldPos.xyz).xyz;
	#elif defined(WEATHER_MAKER)
		_DirectionalLightMultiplier = 1;
		_PointSpotLightMultiplier = 1;
		_AmbientLightMultiplier = 1;
		sourceColor = ComputeWeatherMakerFog(half4(sourceColor, 1.0), worldPos, true);
	#elif defined(ATMOSPHERIC_HEIGHT_FOG)
		float4 fogParams = GetAtmosphericHeightFog(worldPos);
		fogParams.a = saturate(fogParams.a * 1.35f); //by some reason max value < 0.75;
		sourceColor = ApplyAtmosphericHeightFog(half4(sourceColor, 1.0), fogParams).xyz;
	#elif defined(VOLUMETRIC_FOG_AND_MIST)
		sourceColor = overlayFog(worldPos, float4(screenUV, screenPosZ, 1), half4(sourceColor, 1.0)).xyz;
	#elif defined(COZY_FOG)
		sourceColor = BlendStylizedFog(worldPos, half4(sourceColor.xyz, 1));
	#elif defined(COZY_FOG_3)
		sourceColor = BlendStylizedFog(worldPos, half4(sourceColor.xyz, 1));
	#elif defined(AURA2)
		Aura2_ApplyFog(sourceColor, float3(screenUV, -mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).z));
	#endif

	return max(0, sourceColor);
}

inline float GetExposure()
{
	return 1;
}

inline float3 GetMainLightDir()
{
	return KWS_DirLightDireciton.xyz;
}

inline float3 GetMainLightColor(float exposure)
{
	return KWS_DirLightColor.xyz;
}


inline float3 KWS_GetSkyColor(float3 reflDir, float lod, float exposure)
{
	//reflDir.y += clamp(lod * 0.1, 0, 0.15);
	UNITY_BRANCH
	if (KWS_OverrideSkyColor == 1) return KWS_CustomSkyColor.xyz;
	else
	{
		#if defined(ENVIRO_3_FOG) || defined(WEATHER_MAKER)
			float4 skyData = unity_SpecCube0.SampleLevel(sampler_trilinear_clamp, reflDir, lod);
			return max(0, DecodeHDR(skyData, unity_SpecCube0_HDR));
		#else
			float4 skyData = KWS_SkyTexture.SampleLevel(sampler_trilinear_clamp, reflDir, lod);
			return skyData.xyz;
			//return max(0, DecodeHDR(skyData, KWS_SkyTexture_HDRDecodeValues)); //not sure why DecodeHDR doesnt work with  ReflectionProbe.defaultTextureHDRDecodeValues

		#endif
	}
}

float GetSurfaceDepth(float screenPosZ)
{
	return UNITY_Z_0_FAR_FROM_CLIPSPACE(screenPosZ);
}


inline float LinearEyeDepthUniversal(float z)
{
	//if (unity_OrthoParams.w == 1.0)
	//   {
	//       return LinearEyeDepth(GetWorldSpacePositionFromDepth(uv, z), UNITY_MATRIX_V);
	//   }
	//   else
	//   {
	//       return 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w);
	//   }
	float persp = 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w);
	float ortho = (_ProjectionParams.z - _ProjectionParams.y) * (1 - z) + _ProjectionParams.y;
	return lerp(persp, ortho, unity_OrthoParams.w);
}




#endif