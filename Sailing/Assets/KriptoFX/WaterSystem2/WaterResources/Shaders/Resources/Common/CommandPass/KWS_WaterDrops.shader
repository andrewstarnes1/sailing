Shader "Hidden/KriptoFX/KWS/WaterDrops"
{
	SubShader
	{
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Off
		ZTest Always

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vertWaterDrops
			#pragma fragment fragWaterDrops
			#pragma target 4.6
			
			#pragma multi_compile _ KWS_DYNAMIC_WAVES_USE_COLOR

			#include "../../Common/KWS_WaterHelpers.cginc"
			
			DECLARE_TEXTURE(_SourceRT);
			float4 KWS_Underwater_RTHandleScale;
			float4 _SourceRTHandleScale;
			float4 _SourceRT_TexelSize;

			float3 KWS_WaterDropsTimer;

			float hash(float2 p)
			{
				return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
			}

			float noise(float2 p)
			{
				float2 i = floor(p);
				float2 f = frac(p);
				float a = hash(i);
				float b = hash(i + float2(1, 0));
				float c = hash(i + float2(0, 1));
				float d = hash(i + float2(1, 1));
				f = f * f * (3.0 - 2.0 * f);
				return lerp(a, b, f.x) + (c - a) * f.y * (1.0 - f.x) + (d - b) * f.x * f.y;
			}

			float2 dropletMovement(float2 uv, float time)
			{
				float2 movement = float2(0.0, -time * 0.2);
				movement += 0.2 * float2(noise(uv * 10.0 + time), noise(uv * 10.0 - time));
				return uv + movement;
			}

			struct vertexInput
			{
				uint vertexID : SV_VertexID;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct vertexOutput
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 vertexColor : COLOR0;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			vertexOutput vertWaterDrops(vertexInput v)
			{
				vertexOutput o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.vertex = GetTriangleVertexPosition(v.vertexID);
				o.uv = GetTriangleUVScaled(v.vertexID);
				o.vertexColor = float4(1, 1, 1, 1);
			
				#ifdef KWS_DYNAMIC_WAVES_USE_COLOR

					uint zoneIndexOffset = 0;
					uint zoneIndexCount = 0;
					float3 pos = GetCameraAbsolutePosition();
					if (GetTileRange(pos, zoneIndexOffset, zoneIndexCount))
					{
						for (uint zoneIndex = zoneIndexOffset; zoneIndex < zoneIndexCount; zoneIndex++)
						{
							ZoneData zone = (ZoneData)0;
							if (GetWaterZone(pos, zoneIndex, zone))
							{
								float4 colorData = GetDynamicWavesZoneColorData(zone.id, zone.uv);
								o.vertexColor = colorData;
							}
						}
					}

				#endif

				return o;
			}

			half4 fragWaterDrops(vertexOutput i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				
				
				float lensTime = KWS_WaterDropsTimer.x;
				float stretchTime = KWS_WaterDropsTimer.y;
				float waterDropsFadeTimer = KWS_WaterDropsTimer.z;


				float fadeIn = saturate(lensTime * 5);
				if (fadeIn < 0.01) return 0;

				bool isUnderwater = GetUnderwaterMask(GetWaterMask(i.uv));
				if (isUnderwater) return 0;
				
				float2 time = float2(0, frac(KWS_ScaledTime * 0.001) * 1000);
				float2 noiseDrops = float2(SimpleNoise1(i.uv * 104 + time * 1.0), SimpleNoise1(i.uv * 100 + (time * 0.5 + 40)));
				float noiseDropsMask = noiseDrops.x * noiseDrops.y;

				float2 noiseDropsStretch = float2(SimpleNoise1(i.uv * float2(40, 1.5) + time * 0.07), SimpleNoise1(i.uv * float2(20, 1) + (time * 0.12 + 40)));
				float noiseDropsMaskStretch = noiseDropsStretch.x * noiseDropsStretch.y;

				float2 stretchUV = float2(0, -saturate(noiseDropsStretch.x * 10) * stretchTime * 0.02 - saturate(noiseDropsStretch.y * 20) * stretchTime * 0.04 - stretchTime * 0.0125);
				
				
				float3 waterDropsMask = SAMPLE_TEXTURE_LOD(KWS_WaterDropsMaskTexture, sampler_linear_repeat, i.uv * float2(2, 1.0) - stretchUV * 0, 0).xyz;
				float4 waterDropsNormal = SAMPLE_TEXTURE_LOD(KWS_WaterDropsTexture, sampler_linear_repeat, i.uv * float2(5, 1.5) - stretchUV * 1 - float2(0, noiseDropsMask * 0.1), 0);


				float2 normal = waterDropsNormal.xy * 2 - 1;
				float dropsShadow = waterDropsNormal.z;
				float alpha = waterDropsNormal.w;
				normal *= saturate(waterDropsMask.x - waterDropsFadeTimer);
				
				dropsShadow *= saturate(waterDropsMask.x - waterDropsFadeTimer);
				dropsShadow = lerp(1, saturate(KWS_Pow2(dropsShadow * 2) + 0.85), alpha);
				if (waterDropsNormal.z > 0.7) dropsShadow *= 1.25;

				
				float2 lensNormal = waterDropsMask.xy * 2 - 1;

				float fadeMask = saturate(waterDropsMask.z - frac(lensTime * 0.5) * 2);
				float fadeMask2 = saturate(waterDropsMask.z - frac((lensTime * 2 + 0.2) * 0.25) * 2);
				
				//return float4(noiseDropsMaskStretch.xxx,   1);

				if (fadeMask < 0.01 && alpha < 0.01) return 0;
				

				float maskLensFringe = fadeMask > 0.01 && (fadeMask - fadeMask2) < 0.015;
				float maskLens = fadeMask > 0.01;

				normal = lerp(normal, 0, maskLens);
				dropsShadow = lerp(dropsShadow, 1, maskLens);
				dropsShadow = lerp(dropsShadow, 1, waterDropsFadeTimer);
				
				
				float2 lensDistortion = (lensNormal * 0.02) * maskLens + lensNormal * (maskLensFringe) * 0.5;
				normal *= fadeIn;
				lensDistortion *= fadeIn;
				dropsShadow = lerp(1, dropsShadow, fadeIn);
				float3 waterColor = lerp(float3(0.85, 0.92, 0.95), float3(1, 1, 1), fadeIn);
				float3 sceneColorAfterWater = GetSceneColorAfterWaterPass(i.uv - normal * 0.5 * KWS_Pow2(1 - waterDropsFadeTimer) - lensDistortion * saturate(1 - lensTime));

				#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
					float3 finalColor = sceneColorAfterWater * waterColor * dropsShadow * 0.75 + sceneColorAfterWater * i.vertexColor.rgb * 2.0;
					finalColor = lerp(finalColor, sceneColorAfterWater, saturate(waterDropsFadeTimer));
				#else
					float3 finalColor = sceneColorAfterWater * waterColor * dropsShadow;
				#endif
				
				
				return float4(finalColor, 1);
			}

			ENDHLSL
		}
	}
}