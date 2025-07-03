Shader "Hidden/KriptoFX/KWS/KWS_DynamicWavesFoamParticles"
{
	HLSLINCLUDE

	#define KWS_USE_SOFT_SHADOWS
	#include "../../Common/KWS_WaterHelpers.cginc"
	//#include "../../PlatformSpecific/KWS_Lighting.cginc"

	Texture2D KWS_DynamicWavesFoamShadowMap;
	float4 KWS_DynamicWavesFoamShadowMap_TexelSize;

	float KWS_ParticlesFoamInterpolationTime;
	float KWS_FoamParticlesScale;
	float KWS_FoamParticlesAlphaMultiplier;


	static const float2 quadOffsets[3] =
	{
		float2(1, -1),
		float2(-1, -1),
		float2(0, 1)
	};

	#define FOAM_SIZE_MIN 0.07
	#define FOAM_SIZE_MAX 0.03


	void GetDynamicWavesFoamParticlesVertexPosition(FoamParticle particle, uint vertexID, float farDistance, out float3 vertex, out float2 uv, out float4 color)
	{
		//KWS_ParticlesFoamInterpolationTime = 1;
		float3 center = lerp(particle.prevPosition, particle.position, KWS_ParticlesFoamInterpolationTime);
		float currentLifeTime = lerp(particle.prevLifetime, particle.currentLifetime, KWS_ParticlesFoamInterpolationTime);
		color = lerp(particle.prevColor, particle.color, KWS_ParticlesFoamInterpolationTime);


		float oceanLevel = KWS_WaterPosition.y;
		float maxWaveDisplacement = lerp(2, 20, saturate(KWS_WindSpeed * 0.02));
		if(center.y < oceanLevel + maxWaveDisplacement) 
		{
			float2 dynamicWavesUV = GetDynamicWavesUV(center);
			float4 dynamicWavesAdditionalData = GetDynamicWavesZoneAdditionalData(dynamicWavesUV);
			float3 disp = GetFftWavesDisplacement(center);
			float shorelineMask = dynamicWavesAdditionalData.y;
		
			disp *= shorelineMask;
			center += disp;
		}

		float normalizedLifeTime = 1 - saturate(particle.currentLifetime / particle.maxLifeTime);
		
		float particleSize = lerp(FOAM_SIZE_MIN, FOAM_SIZE_MAX, particle.initialRandom01) * KWS_FoamParticlesScale;
		particleSize *= saturate(sin(normalizedLifeTime * 3.1415) * 2);
		particleSize *= 1 + farDistance * 10;
		
		//float particleHeightOffset = particleSize * 0.75;
		//float particleHeightStretch = lerp(1, 3, particle.isFreeMoving);
		//particleSize *= lerp(1, 0.75, particle.isFreeMoving);

		float2 offset = quadOffsets[vertexID];
		uv = offset * 0.5 + 0.5;

		vertex = center + (KWS_CameraRight * offset.x) * particleSize + (KWS_CameraUp * offset.y) * particleSize * 1;
		vertex.y += lerp(0.1, 0.3, length(particle.velocity.xz * 0.2));
	}


	struct appdata
	{
		uint instanceID : SV_InstanceID;
		uint vertexID : SV_VertexID;
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

			#pragma multi_compile _ KWS_USE_DIR_LIGHT
			#pragma multi_compile _ KWS_USE_PHYTOPLANKTON_EMISSION

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR0;
				nointerpolation float speed : TEXCOORD1;
				float4 fogData : TECXOORD2;
			};


			v2f vert(appdata v)
			{
				v2f o = (v2f)0;
				float3 vertex;
				float2 uv;
				float4 color;
				FoamParticle particle = KWS_FoamParticlesBuffer[v.instanceID];
				
				float cameraDistance = GetWorldToCameraDistance(particle.position);
				float farDistance = saturate(cameraDistance * 0.005);
				
				GetDynamicWavesFoamParticlesVertexPosition(particle, v.vertexID, farDistance, vertex, uv, color);
				
				vertex.y += farDistance * 0.5;

				o.pos = ObjectToClipPos(float4(vertex, 1.0));
				o.uv = uv;
				
				float4 screenPos = ComputeScreenPos(o.pos);
				float2 screenUV = screenPos.xy / screenPos.w;
				bool isUnderwater = GetUnderwaterMask(GetWaterMaskFast(screenUV));
				if (isUnderwater) o.pos.w = NAN_VALUE;
				
				o.color = color;

				#ifndef KWS_USE_PHYTOPLANKTON_EMISSION
					o.color.xyz *= GetVolumetricSurfaceLight(screenUV).xyz;
				#endif

				float3 worldPos = LocalToWorldPos(vertex);
				float3 viewDir = GetWorldSpaceViewDirNorm(worldPos);
				float deviceDepth = screenPos.z / screenPos.w;
				float surfaceDepthZEye = LinearEyeDepthUniversal(deviceDepth);

				half3 fogColor;
				half3 fogOpacity;
				
				GetInternalFogVariables(o.pos, viewDir, deviceDepth, surfaceDepthZEye, worldPos, fogColor, fogOpacity);
				o.fogData.xyz = ComputeInternalFog(0, fogColor, fogOpacity);
				o.fogData.xyz = ComputeThirdPartyFog(o.fogData.xyz, worldPos, screenUV, screenPos.z);
				o.fogData.w = max(saturate(dot(o.fogData.xyz, 0.33)), fogOpacity.x);
				
				return o;
			}


			float4 frag(v2f i) : SV_Target
			{
				float bubbleAlpha = 0.1 * KWS_FoamParticlesAlphaMultiplier;
				float alpha = saturate(1 - length(i.uv - float2(0.5, 0.33)) * 2.5);
				
				float3 bubblesColor = float3(0.75, 0.85, 1);

				#ifndef KWS_USE_PHYTOPLANKTON_EMISSION
					 i.color.rgb = clamp(i.color.rgb, 0, 1.25);
				#endif
				
				bubblesColor *= i.color.rgb;
				alpha *= i.color.a;
				
				alpha = lerp(alpha, 0, i.fogData.w);
				return float4(bubblesColor, saturate(bubbleAlpha * alpha));
			}
			ENDHLSL
		}


		
		//Pass
		//{
		//	Tags { "LightMode" = "ShadowCaster" }

		//	Cull Off
		//	ZWrite On

		//	HLSLPROGRAM
		//	#pragma vertex vert
		//	#pragma fragment frag
		//	#pragma multi_compile_shadowcaster


		//	struct v2f
		//	{
		//		float4 pos : SV_POSITION;
		//		UNITY_VERTEX_INPUT_INSTANCE_ID
		//		UNITY_VERTEX_OUTPUT_STEREO
		//		float2 uv : TEXCOORD0;
		//		uint particleType : TEXCOORD1;
		//		nointerpolation float uvOffset : TEXCOORD2;
		//		nointerpolation float normalizedLifeTime : TEXCOORD3;
		//	};

		//	v2f vert(appdata v)
		//	{
		//		v2f o;

		//		UNITY_INITIALIZE_OUTPUT(v2f, o);

		//		UNITY_SETUP_INSTANCE_ID(v);
		//		UNITY_TRANSFER_INSTANCE_ID(v, o);
		//		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
		
		//		float3 vertex;
		//		float2 uv;
		//		float particleSpeed;
		//		float normalizedLifeTime;
		//		FoamParticle particle = KWS_ParticlesBuffer[v.instanceID];
		//		float cameraDistance = GetWorldToCameraDistance(particle.position);
		//		float farDistance = saturate(cameraDistance * 0.005);

		//		GetDynamicWavesFoamParticlesVertexPosition(particle, v.vertexID, false, farDistance, vertex, uv, particleSpeed, normalizedLifeTime);

		//		o.pos = UnityObjectToClipPos(float4(vertex - float3(0, 0.1, 0), 1.0));
		//		o.uv = uv;
		//		o.uvOffset = particle.uvOffset;
		//		o.normalizedLifeTime = normalizedLifeTime;
		//		//o.screenPos = ComputeScreenPos(o.pos);
		//		o.particleType = particle.particleType;
		//		return o;
		//	}

		//	float4 frag(v2f i) : SV_Target
		//	{
		//		if(i.particleType == KWS_DYNAMIC_WAVE_PARTICLE_TYPE_FOAM) discard;

		//		float noise = InterleavedGradientNoise(i.pos.xy, KWS_Time * 0.25);
		//		if (noise > 0.5) discard;

		//		if(i.particleType == KWS_DYNAMIC_WAVE_PARTICLE_TYPE_SPLASH)
		//		{
		//			float3 splashData = GetSplashData(i.uv, i.uvOffset);
		//			float splashMain = splashData.x;
		//			float splashShine = splashData.y;
		//			float noise = splashData.z;

		//			float lifeTime = 1-GetParticleAlpha(i.normalizedLifeTime);
		
		//			noise = saturate(noise - lifeTime * 2 + 1);
		//			splashShine = splashShine * noise;
		//			splashMain = splashMain * noise;
		
		
		//			splashShine = splashShine * splashShine * splashShine;

		//			float splashAlpha = saturate(splashMain *  splashMain * 1.5 + splashShine * 1);
		
		
		//			//return float4(i.uv, 0, 1);
		//			if(splashAlpha < 0.01) discard;
		//		}
		
		//		/*float bubbleAlpha = ComputeBubblesAlpha(i.screenPos.xy / i.screenPos.w, i.uv, i.particleData.x, i.particleData.y);
		
		//		alpha = saturate(bubbleAlpha * bubbleAlpha * bubbleAlpha * alpha * 20);
		//		*/
		//		//float alpha = saturate(1 - length(i.uv - 0.5) * 2);
		//		//if (alpha < 0.01) discard;

		//		UNITY_SETUP_INSTANCE_ID(i);
		//		SHADOW_CASTER_FRAGMENT(i)
		//	}
		//	ENDHLSL
		//}

	}
}