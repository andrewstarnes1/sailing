Shader "Hidden/KriptoFX/KWS/KWS_DynamicWavesSplashParticles"
{
	HLSLINCLUDE

	#define KWS_LIGHTING_RECEIVE_DIR_SHADOWS
	//#define	KWS_USE_SOFT_SHADOWS


	#include "../../Common/KWS_WaterHelpers.cginc"

	Texture2D KWS_DynamicWavesFoamShadowMap;
	Texture3D _DitherMaskLOD;
	float4 KWS_DynamicWavesFoamShadowMap_TexelSize;
	float KWS_ParticlesSplashInterpolationTime;

	float KWS_SplashParticlesScale;
	float KWS_SplashParticlesAlphaMultiplier;

	static const float2 quadOffsets[3] =
	{
		float2(1, -1),
		float2(-1, -1),
		float2(0, 1)
	};


	#define SPLASH_SIZE_MIN 1.5
	#define SPLASH_SIZE_MIN_SHORELINE 0.75
	#define SPLASH_SIZE_MAX 5.0

	float2 RotateBillboardVertexOptimized(float2 offset, float angle, float2 center)
	{
		float2 localOffset = offset -center;

		float sinA, cosA;
		sincos(angle, sinA, cosA);

		float2 rotatedOffset;
		rotatedOffset.x = localOffset.x * cosA - localOffset.y * sinA;
		rotatedOffset.y = localOffset.x * sinA + localOffset.y * cosA;

		return rotatedOffset +center;
	}

	void GetDynamicWavesFoamParticlesVertexPosition(SplashParticle particle, uint vertexID, float farDistance, out float3 vertex, out float2 uv, out float particleSpeed, out float normalizedLifeTime, out float particleSize)
	{
		
		float3 center = lerp(particle.prevPosition, particle.position, KWS_ParticlesSplashInterpolationTime);
		float currentLifeTime = lerp(particle.prevLifetime, particle.currentLifetime, KWS_ParticlesSplashInterpolationTime);

		float oceanLevel = KWS_WaterPosition.y;
		float maxWaveDisplacement = lerp(2, 20, saturate(KWS_WindSpeed * 0.02));
		if (center.y < oceanLevel + maxWaveDisplacement)
		{
			float2 dynamicWavesUV = GetDynamicWavesUV(center);
			float4 dynamicWavesAdditionalData = GetDynamicWavesZoneAdditionalData(dynamicWavesUV);
			float3 disp = GetFftWavesDisplacement(center);
			float shorelineMask = dynamicWavesAdditionalData.y;
			
			disp *= shorelineMask;
			center += disp;
		}

		float3 velocity = particle.velocity;

		particleSpeed = length(velocity);
		normalizedLifeTime = 1;
		
		float verticalVelocityStretch = 0;
		float gravityFactor = saturate(dot(float3(0, -1, 0), velocity / particleSpeed));
		verticalVelocityStretch = saturate(KWS_Pow5(gravityFactor) * particleSpeed * 0.1) * 20;
		verticalVelocityStretch *= saturate(particle.distanceToSurface);

		particleSize = 0.1;
		float particleHeightOffset = 0;
		
		float minSize = lerp(SPLASH_SIZE_MIN, SPLASH_SIZE_MIN_SHORELINE, particle.shorelineMask);
		particleSize = lerp(minSize * KWS_SplashParticlesScale, SPLASH_SIZE_MAX * saturate(KWS_SplashParticlesScale + 0.25), KWS_Pow5(particle.initialRandom01) * saturate(particle.initialSpeed * 0.25));

		normalizedLifeTime = 1 - saturate(currentLifeTime / lerp(0.75, 1.25, particle.initialRandom01));
		float fadeFactor = 0.5 + 0.5 * (sin(normalizedLifeTime * 3.1415));
		fadeFactor *= saturate(normalizedLifeTime * 20);
		
		particleSize *= fadeFactor;
		verticalVelocityStretch *= fadeFactor * (1 - normalizedLifeTime);
		particleHeightOffset += lerp(particleSize * 0.75, particleSize * 0.4, particle.shorelineMask);

		
		float2 offset = quadOffsets[vertexID];
		uv = offset * 0.5 + 0.5;
		offset = RotateBillboardVertexOptimized(offset, (particle.initialRandom01 + particle.initialSpeed) * 3.1415 * 2, float2(0, -0.5));

		vertex = center + (KWS_CameraRight * offset.x) * particleSize + (KWS_CameraUp * offset.y) * (particleSize + verticalVelocityStretch);
		vertex.y += particleHeightOffset;
	}

	
	float4 GetSplashData(float2 uv, float uvOffset)
	{
		uv.x *= 0.25;
		uv.x += uvOffset;
		float4 data = KWS_SplashTex0.SampleBias(sampler_linear_repeat, uv, -1.5).xyzw;

		return data;
	}

	float GetParticleAlpha(float lifetime)
	{
		float fadeIn = saturate(lifetime * 20);
		float fadeOut = saturate((1.0 - lifetime) * 1.25);
		return fadeIn * fadeOut;
	}

	struct appdata
	{
		uint instanceID : SV_InstanceID;
		uint vertexID : SV_VertexID;
		//UNITY_VERTEX_INPUT_INSTANCE_ID
	};

	ENDHLSL


	SubShader
	{
		
		//Tags { "RenderType" = "Opaque" "Queue" = "AlphaTest+1" }
		//Zwrite On
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" }

		Pass
		{
			
			Blend SrcAlpha OneMinusSrcAlpha
			//Blend SrcAlpha One
			Zwrite Off
			Cull Off
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma editor_sync_compilation

			#pragma target 4.6
			#pragma multi_compile _ KWS_USE_SPLASH_PER_PIXEL_SHADOWS
			#pragma multi_compile _ KWS_DYNAMIC_WAVES_USE_COLOR
			#pragma multi_compile _ KWS_USE_DIR_SHADOW KWS_USE_ALL_SHADOWS
			#pragma multi_compile _ KWS_USE_PER_VERTEX_SHADOWS

			#ifdef KWS_USE_ALL_SHADOWS
					#define KWS_USE_DIR_SHADOW
					#define KWS_USE_ADDITIONAL_SHADOW
			#endif
		
			#ifdef KWS_BUILTIN

				#pragma multi_compile _ KWS_USE_DIR_LIGHT
				#pragma multi_compile _ KWS_USE_POINT_LIGHTS
				#pragma multi_compile _ KWS_USE_SPOT_LIGHTS

			#ifndef KWS_USE_ADDITIONAL_SHADOW
				#define KWS_DISABLE_POINT_SPOT_SHADOWS
			#endif
				
			#endif

			
			#ifdef KWS_URP

				#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
				#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
				
				#pragma multi_compile _ _FORWARD_PLUS
				#pragma multi_compile _ _LIGHT_LAYERS
				
			#endif


			#ifdef KWS_HDRP
				
				#pragma multi_compile _ SUPPORT_LOCAL_LIGHTS
				
			#endif
			
		

			#include "../../PlatformSpecific/KWS_LightingHelpers.cginc"

			
			struct v2f
			{
				float4 pos : SV_POSITION;
				float4 color : COLOR0;
				float2 uv : TEXCOORD0;
				float4 screenPos : TEXCOORD1;
				float waterDepth : TEXCOORD2;
				float3 worldPos : TEXCOORD3;
				nointerpolation float4 particleData : TEXCOORD4;
				float4 fogData : TEXCOORD5;
				#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
					float4 overrideColor : TEXCOORD6;
				#endif
				UNITY_VERTEX_OUTPUT_STEREO
			};


			v2f vert(appdata v)
			{
				v2f o = (v2f)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				float3 vertex;
				float2 uv;
				float particleSpeed;
				float normalizedLifeTime;
				float particleSize;
				
				SplashParticle particle = KWS_SplashParticlesBuffer[v.instanceID];
				
				float cameraDistance = GetWorldToCameraDistance(particle.position);
				float farDistance = saturate(cameraDistance * 0.005);
				
				GetDynamicWavesFoamParticlesVertexPosition(particle, v.vertexID, farDistance, vertex, uv, particleSpeed, normalizedLifeTime, particleSize);
				vertex.y += farDistance * 0.5;

				o.particleData.x = particle.initialRandom01;
				o.particleData.y = particle.uvOffset;
				o.particleData.z = normalizedLifeTime;
				o.particleData.w = particle.initialSpeed;

				o.pos = ObjectToClipPos(float4(vertex, 1.0));
				o.uv = uv;
				o.screenPos = ComputeScreenPos(o.pos);

				float2 screenUV = o.screenPos.xy / o.screenPos.w;
				bool isUnderwater = GetUnderwaterMask(GetWaterMaskFast(screenUV));
				o.waterDepth = LinearEyeDepthUniversal(GetWaterDepth(screenUV));
				if (isUnderwater)
				{
					o.pos.w = NAN_VALUE;
					return o;
				}
				
				o.worldPos = LocalToWorldPos(vertex);
				
				#ifdef KWS_USE_PER_VERTEX_SHADOWS
					o.color.rgb = KWS_ComputeLighting(o.worldPos, 0.1, true, screenUV);
				#endif
				
				float2 dynamicWavesUV = GetDynamicWavesUV(o.worldPos);
				float borderFade = GetDynamicWavesBorderFading(dynamicWavesUV);
				o.color.a = 1;

				#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
					
					float4 zoneColorData = GetDynamicWavesZoneColorData(dynamicWavesUV);
					zoneColorData.rgb = lerp(zoneColorData.rgb, zoneColorData.rgb * 0.35, saturate(zoneColorData.a * zoneColorData.a + zoneColorData.a * 2));
					o.overrideColor = max(o.overrideColor, zoneColorData);
					
				#endif
				o.color.a *= saturate(1.25 - farDistance);

				float3 viewDir = GetWorldSpaceViewDirNorm(o.worldPos);
				float deviceDepth = o.screenPos.z / o.screenPos.w;
				float surfaceDepthZEye = LinearEyeDepthUniversal(deviceDepth);

				//I can approximate fog by calculating the color difference between black and the fog color, and use that difference as "fog opacity".
				//It's faster than per-pixel fog, which is overkill in HDRP and third-party fogs
				half3 fogColor;
				half3 fogOpacity;
				
				GetInternalFogVariables(o.pos, viewDir, deviceDepth, surfaceDepthZEye, o.worldPos, fogColor, fogOpacity);
				o.fogData.xyz = ComputeInternalFog(0, fogColor, fogOpacity);
				o.fogData.xyz = ComputeThirdPartyFog(o.fogData.xyz, o.worldPos, screenUV, o.screenPos.z);
				o.fogData.w = max(saturate(dot(o.fogData.xyz, 0.33)), fogOpacity.x);
				
				return o;
			}


			float4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				
				float2 screenUV = i.screenPos.xy / i.screenPos.w;
				
				float particleRandom01 = i.particleData.x;
				float uvOffset = i.particleData.y;
				float normalizedLifeTime = i.particleData.z;
				float initialSpeed = i.particleData.w;
				
				float4 splashData = GetSplashData(i.uv, uvOffset);
				float splashMain = splashData.x;
				float splashShine = splashData.y;
				float noise = splashData.z;
				float splashDepth = splashData.w;
				
				float3 waterColor = float3(0.7, 0.85, 1);
				
				if (splashMain + splashShine < 0.05) return 0;

				float lifeTime = 1 - GetParticleAlpha(normalizedLifeTime);
				
				noise = saturate(noise - lifeTime * 2 + 1);
				splashShine = splashShine * noise;
				splashMain = splashMain * noise * KWS_SplashParticlesAlphaMultiplier;
				splashShine = splashShine * splashShine * splashShine;

				float normalizedSpeed = saturate(initialSpeed * initialSpeed * 0.05);
				waterColor *= lerp(0.5, 1, normalizedSpeed);
				
				float splashAlpha = saturate(splashMain * 0.5 + splashMain * splashMain * 2 + splashShine * 1);
				
				float waterDepth = LinearEyeDepthUniversal(GetWaterDepth(screenUV));
				float sceneDepth = LinearEyeDepthUniversal(GetSceneDepth(screenUV));

				float surfaceDepth = LinearEyeDepthUniversal(i.screenPos.z / i.screenPos.w);
				float softParticlesFade = saturate(1 * abs(min(waterDepth, sceneDepth) - surfaceDepth));
				splashAlpha *= lerp(softParticlesFade * saturate(KWS_Pow10(splashDepth) * 5), 1, softParticlesFade);

				splashAlpha *= saturate(normalizedLifeTime * normalizedLifeTime * 30);

				//return float4(lerp(pow(splashDepth, Test4.x),  1, softParticlesFade * softParticlesFade), 0, 0, 1);
				#ifndef KWS_USE_PER_VERTEX_SHADOWS
					i.color.rgb = KWS_ComputeLighting(i.worldPos, 0.1, true, screenUV);
				#endif
				
				#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
					waterColor = lerp(waterColor, i.overrideColor.rgb, saturate(i.overrideColor.a * 2) * (0.8 + particleRandom01 * 0.4));
				#endif
				
				float3 splashColor = (waterColor * 1 + splashShine * 3) * clamp(i.color.rgb, 0, 1.25);
				
				float4 finalColor =  float4(splashColor * KWS_SplashParticlesAlphaMultiplier, saturate(splashAlpha * i.color.a));
				finalColor.a = lerp(finalColor.a, 0, i.fogData.w);
				
				return finalColor;
				
			}
			ENDHLSL
		}

		
		
		Pass
		{
			Tags { "LightMode" = "ShadowCaster" "Queue" = "AlphaTest+1" }

			Cull Off
			ZWrite On

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_shadowcaster
			#pragma dynamic_branch _ KWS_USE_SPLASH_SHADOW_CAST_FAST
			
			struct v2f
			{
				float4 pos : SV_POSITION;
				float4 color : COLOR0;
				float2 uv : TEXCOORD0;
				float4 screenPos : TEXCOORD1;
				float waterDepth : TEXCOORD2;
				float3 worldPos : TEXCOORD3;
				nointerpolation float particleRandom01 : TEXCOORD4;
				nointerpolation float uvOffset : TEXCOORD5;
				nointerpolation float normalizedLifeTime : TEXCOORD6;
				nointerpolation float particleSize : TEXCOORD7;
				#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
					float4 overrideColor : TEXCOORd8;
				#endif

				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			v2f vert(appdata v)
			{
				v2f o = (v2f)0;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				
				SplashParticle particle = KWS_SplashParticlesBuffer[v.instanceID];

				if (KWS_USE_SPLASH_SHADOW_CAST_FAST)
				{
					if (particle.initialRandom01 > 0.3)
					{
						o.pos.w = NAN_VALUE;
						return o;
					}
				}

				float cameraDistance = GetWorldToCameraDistance(particle.position);
				float farDistance = saturate(cameraDistance * 0.005);
				
				float3 vertex;
				float2 uv;
				float particleSpeed;
				float normalizedLifeTime;
				float particleSize;

				GetDynamicWavesFoamParticlesVertexPosition(particle, v.vertexID, farDistance, vertex, uv, particleSpeed, normalizedLifeTime, particleSize);
				vertex.y += farDistance * 0.5;

				//shadow offset
				if (KWS_USE_SPLASH_SHADOW_CAST_FAST) vertex.y -= 0.35;
				else vertex.y -= 0.5;
				

				o.particleRandom01 = particle.initialRandom01;
				o.uvOffset = particle.uvOffset;
				o.normalizedLifeTime = normalizedLifeTime;
				o.particleSize = particleSize;
				
				o.pos = ObjectToClipPos(float4(vertex, 1.0));
				o.uv = uv;
				o.screenPos = ComputeScreenPos(o.pos);
				
				float2 screenUV = o.screenPos.xy / o.screenPos.w;
				bool isUnderwater = GetUnderwaterMask(GetWaterMaskFast(screenUV));
				o.waterDepth = LinearEyeDepthUniversal(GetWaterDepth(screenUV));
				//if (isUnderwater) o.pos.w = NAN_VALUE;
				
				
				o.worldPos = LocalToWorldPos(vertex);
				
				float2 dynamicWavesUV = GetDynamicWavesUV(o.worldPos);
				float borderFade = GetDynamicWavesBorderFading(dynamicWavesUV);
				o.color.a = borderFade;


				return o;
			}

			static const float4x4 bayerMatrix =
			{
				1.0 / 17.0, 9.0 / 17.0, 3.0 / 17.0, 11.0 / 17.0,
				13.0 / 17.0, 5.0 / 17.0, 15.0 / 17.0, 7.0 / 17.0,
				4.0 / 17.0, 12.0 / 17.0, 2.0 / 17.0, 10.0 / 17.0,
				16.0 / 17.0, 8.0 / 17.0, 14.0 / 17.0, 6.0 / 17.0
			};


			//float BayerDither(float2 uv, float alpha)
			//{
			//	float threshold = (bayerMatrix[(int)fmod(uv.y, 4.0)][(int)fmod(uv.x, 4.0)] + 0.5) / 16.0;
			//	return alpha > threshold ?  1.0 : 0.0;
			//}


			float4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				//float ignNoise = InterleavedGradientNoise(i.pos.xy + i.particleRandom01 * 1000, KWS_ScaledTime * 1);
				//if (ignNoise > 0.75) discard;
				
				float3 splashData = GetSplashData(i.uv, i.uvOffset).xyz;
				float splashMain = splashData.x;
				float splashShine = splashData.y;
				float noise = splashData.z;
				
				//if (splashMain + splashShine < 0.05) discard;

				float lifeTime = 1 - GetParticleAlpha(i.normalizedLifeTime);
				
				noise = saturate(noise - lifeTime * 2 + 1);
				splashShine = splashShine * noise;
				splashMain = splashMain * noise;
				splashShine = splashShine * splashShine * splashShine;
				
				float splashAlpha = saturate(splashMain * 0.5 + splashMain * splashMain * 2 + splashShine * 1);
				splashAlpha *= saturate(i.normalizedLifeTime * i.normalizedLifeTime * 30);
				
				float blueNoise = KWS_BlueNoise3D.SampleLevel(sampler_linear_repeat, i.pos.xy / 128.0, 0).x;
				float transparencyFactor = lerp(0.2, 1.5, saturate(i.particleSize / SPLASH_SIZE_MAX)) * KWS_SplashParticlesAlphaMultiplier;

				if (KWS_USE_SPLASH_SHADOW_CAST_FAST)
				{
					if (saturate(splashAlpha * 10 * transparencyFactor) < blueNoise.x) discard;
				}
				else
				{
					if (saturate(splashAlpha * 1.5 * transparencyFactor) < blueNoise.x) discard;
				}
				
				
				return 0;
			}
			ENDHLSL
		}
	}
}