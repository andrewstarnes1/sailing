Shader "Hidden/KriptoFX/KWS/CausticDecal"
{
	Properties
	{
		[HideInInspector]KWS_StencilMaskValue ("KWS_StencilMaskValue", Int) = 32
	}

	Subshader
	{
		ZWrite Off
		Cull Front

		ZTest Always
		Blend DstColor SrcColor
		//Blend SrcAlpha OneMinusSrcAlpha

		Stencil
		{
			Ref [KWS_StencilMaskValue]
			ReadMask [KWS_StencilMaskValue]
			//WriteMask [KWS_StencilMaskValue]
			Comp Greater
			Pass keep
		}

		Pass
		{
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.6

			#define SHORELINE_CAUSTIC_STRENGTH 0.1
			#define DYNAMIC_WAVES_CAUSTIC_STRENGTH_VELOCITY 0.5
			#define DYNAMIC_WAVES_CAUSTIC_STRENGTH_HEIGHT 1
			#define CURVEDWORLD_DISABLED_ON

			#pragma multi_compile_fragment _ USE_DISPERSION
			#pragma multi_compile_fragment _ KWS_USE_VOLUMETRIC_LIGHT
			#pragma multi_compile_fragment _ KWS_CAUSTIC_CASCADES_2 KWS_CAUSTIC_CASCADES_3
			
			#pragma multi_compile_fragment _ KWS_USE_LOCAL_WATER_ZONES
			#pragma multi_compile_fragment _ KWS_DYNAMIC_WAVES_VISIBLE_ZONES_1 KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2 KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4 KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8
			#pragma multi_compile_fragment _ KWS_DYNAMIC_WAVES_USE_COLOR
			#pragma multi_compile_fragment _ KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE

			#include "../../Common/KWS_WaterHelpers.cginc"


			bool GetClipFade(float3 worldPos)
			{
				float3 localPos = WorldToLocalPos(worldPos);
				float3 fadeByAxis = saturate(0.5 - abs(localPos));
				float clipFade = fadeByAxis.x * fadeByAxis.y * fadeByAxis.z;
				return clipFade;
			}

			float GetDepthFade(float depth, float2 screenUV, float underwaterMask)
			{
				float terrainFade = saturate((LinearEyeDepthUniversal(depth) - LinearEyeDepthUniversal(GetWaterDepth(screenUV))) * 0.25);
				return lerp(terrainFade, 1 - terrainFade, underwaterMask);
			}


			float GetFadeCascade0(float distanceToCamera)
			{
				return 1 - (saturate(distanceToCamera / 25));
			}

			float GetFadeCascade1(float distanceToCamera)
			{
				return 1 - abs(saturate((distanceToCamera + 50) / 200) * 2 - 1);
			}

			float GetFadeCascade2(float distanceToCamera)
			{
				return saturate(distanceToCamera / 200);
			}




			half3 ComputeCausticCascades(float depth, float2 screenUV, float3 worldPos, float underwaterMask)
			{
				float distanceToCamera = GetWorldToCameraDistance(worldPos);
				uint currentCausticCascade = floor(saturate(distanceToCamera * 0.005) * 2);
				
				float domainSize = GetDomainSize(currentCausticCascade);
				float2 causticUV = (worldPos.xz / domainSize);
				float3 caustic = 0;

				float causticCascade0 = 0;
				float causticCascade1 = 0;
				float causticCascade2 = 0;

				float causticCascade0_flow = 0;
				float causticCascade1_flow = 0;
				float causticCascade2_flow = 0;

				float2 flowMapOffset = 0;
				float flowMapLerp = 0;
				float normalMask = 0;

				#if defined(KWS_USE_DYNAMIC_WAVES) 
					float2 zoneUV = 0;
					float borderFade = 0;
					float2 flowDirection = 0;

					uint zoneIndexOffset = 0; uint zoneIndexCount = 0;
					if (GetTileRange(worldPos, zoneIndexOffset, zoneIndexCount) == true)
					{
						for (uint zoneIndex = zoneIndexOffset; zoneIndex < zoneIndexCount; zoneIndex++)
						{
							ZoneData zone = (ZoneData)0;
							if (GetWaterZone(worldPos, zoneIndex, zone))
							{
								float4 dynamicWaves = GetDynamicWavesZone(zone.id, zone.uv);
								float3 zoneNormal = GetDynamicWavesZoneNormals(zone.id, zone.uv).xyz;
								float zoneFade = GetDynamicWavesBorderFading(zone.uv);

								flowDirection = flowDirection + dynamicWaves.xy * zoneFade;
								normalMask += dot(zoneNormal, float3(0, 0, 0.25)) * zoneFade;
								borderFade = saturate(borderFade + zoneFade);
							}
						}
					}
					
					flowDirection = NormalizeDynamicWavesVelocity(flowDirection);
					flowDirection = flowDirection * flowDirection * sign(flowDirection);
					FlowMapData data = GetFlowmapData(worldPos.xz * 0.075, flowDirection * 0.75, 2);
					
					float flowThreshold = 0.01;
					//if (abs(flowDirection.x) > flowThreshold || abs(flowDirection.y) > flowThreshold)
					{	
						
						flowMapOffset = data.uvOffset1;
						flowMapLerp = data.lerpValue;

						causticCascade0_flow = GetCausticSlice(data.uvOffset2 + worldPos.xz / GetDomainSize(0), 0) * GetFadeCascade0(distanceToCamera);

						#if defined(KWS_CAUSTIC_CASCADES_2) || defined(KWS_CAUSTIC_CASCADES_3)
							causticCascade1_flow = GetCausticSlice(data.uvOffset2 * 0.5 + worldPos.xz / GetDomainSize(1), 1) * GetFadeCascade1(distanceToCamera);
						#endif

						#if defined(KWS_CAUSTIC_CASCADES_3)
							causticCascade2_flow = GetCausticSlice(data.uvOffset2 * 0.125 + worldPos.xz / GetDomainSize(2), 2) * GetFadeCascade2(distanceToCamera);
						#endif
					}
					
				#endif
			
				causticCascade0 = GetCausticSlice(flowMapOffset + worldPos.xz / GetDomainSize(0), 0) * GetFadeCascade0(distanceToCamera);
		
				#if defined(KWS_CAUSTIC_CASCADES_2) || defined(KWS_CAUSTIC_CASCADES_3)
					causticCascade1 = GetCausticSlice(flowMapOffset * 0.5 + worldPos.xz / GetDomainSize(1), 1) * GetFadeCascade1(distanceToCamera);
				#endif

				#if defined(KWS_CAUSTIC_CASCADES_3)
					causticCascade2 = GetCausticSlice(flowMapOffset * 0.125 + worldPos.xz / GetDomainSize(2), 2) * GetFadeCascade2(distanceToCamera);
				#endif

				#if defined(KWS_USE_DYNAMIC_WAVES) 
					caustic += lerp(causticCascade0, causticCascade0_flow, flowMapLerp);
					caustic += lerp(causticCascade1, causticCascade1_flow, flowMapLerp);
					caustic += lerp(causticCascade2, causticCascade2_flow, flowMapLerp);
					caustic += saturate(abs(normalMask) * 0.75 + normalMask * 0.25);
				#else
					caustic += causticCascade0;
					caustic += causticCascade1;
					caustic += causticCascade2;
				#endif
				
				float depthFade = GetDepthFade(depth, screenUV, underwaterMask);
				caustic = lerp(float3(0, 0, 0), caustic, depthFade);
				
				float waterHeight = KWS_WaterPosition.y;
				float transparent = KWS_Transparent;
				half verticalDepth = GetVolumeLightInDepthTransmitance(waterHeight, worldPos.y, transparent * 0.5);
				caustic = lerp(float3(0, 0, 0), caustic, verticalDepth);

				float causticStrength = KWS_CausticStrength * saturate(KWS_Pow2(transparent * 0.1));
				float minCausticBlackOffset = lerp(0, -KWS_CAUSTIC_MULTIPLIER * 2, underwaterMask);
				float causticBlackOffset = lerp(KWS_CAUSTIC_MULTIPLIER, KWS_CAUSTIC_MULTIPLIER * 2, underwaterMask);

		
				return max(minCausticBlackOffset, pow(caustic * causticStrength, causticStrength * 0.5) - causticBlackOffset);
			}

			struct vertexInput
			{
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct vertexOutput
			{
				float4 vertex : SV_POSITION;
				float4 screenUV : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			};


			vertexOutput vert(vertexInput v)
			{
				vertexOutput o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.vertex = ObjectToClipPos(v.vertex);
				o.screenUV = ComputeScreenPos(o.vertex);
				return o;
			}

			

			half4 frag(vertexOutput i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				float2 screenUV = i.screenUV.xy / i.screenUV.w;

				//todo check id
				//uint waterID = GetWaterID(screenUV);
				//if (waterID != KWS_WaterInstanceID) discard;

				float depth = GetSceneDepth(screenUV);
				float underwaterMask = GetUnderwaterMask(GetWaterMaskFast(screenUV));
				//if(underwaterMask < 0.5) discard;
				//if (depth > GetWaterDepth(screenUV)) discard;

				float3 worldPos = GetWorldSpacePositionFromDepth(screenUV, depth);
				float3 camPos = GetCameraAbsolutePosition();

				#if defined(KWS_USE_LOCAL_WATER_ZONES)
						
					UNITY_LOOP
					for (uint zoneIdx = 0; zoneIdx < KWS_WaterLocalZonesCount; zoneIdx++)
					{
						LocalZoneData zone = KWS_ZoneData_LocalZone[zoneIdx];
						if (zone.overrideHeight > 0.5 && zone.clipWaterBelowZone)
						{
							float2 distanceToBox = abs(mul(camPos.xz - zone.center.xz, zone.rotationMatrix)) / zone.halfSize.xz;
							float distanceToBorder = max(distanceToBox.x, distanceToBox.y);
							float zoneMinHeight = zone.center.y - zone.halfSize.y;
							if (distanceToBorder < 1.1 && camPos.y < zoneMinHeight) discard;
						}
					}
						
				#endif


				
				float surfaceAtten = 1;
				#if KWS_USE_VOLUMETRIC_LIGHT
					VolumetricLightAdditionalData volumeLightData = GetVolumetricLightAdditionalData(screenUV);
					surfaceAtten = volumeLightData.SceneDirShadow;
				#endif

				//float clipFade = GetClipFade(worldPos);
				//if (clipFade < 0.01 || surfaceAtten < 0.01) return float4(0.5, 0.5, 0.5, 0.5);
				if (surfaceAtten < 0.01) return float4(0.5, 0.5, 0.5, 0.5);

				half3 caustic = ComputeCausticCascades(depth, screenUV, worldPos, underwaterMask);
				

				half3 fogColor;
				half3 fogOpacity;
				float linearDepth = LinearEyeDepthUniversal(depth);
				GetInternalFogVariables(i.vertex, 0, depth, linearDepth, worldPos, fogColor, fogOpacity);
				
				float3 worldNormal = KWS_GetDerivativeNormal(worldPos, _ProjectionParams.x);
				float causticAlphaRelativeToWorldUp = 1 - saturate(dot(worldNormal, float3(0, -1, 0)));
				caustic *= causticAlphaRelativeToWorldUp;
				caustic *= surfaceAtten;
				//float causticAlphaRelativeToNormal = saturate(dot(worldNormal, GetMainLightDir()));
				//caustic *= saturate(causticAlphaRelativeToNormal * 2);
				
				caustic = lerp(float3(0.5, 0.5, 0.5) + caustic, float3(0.5, 0.5, 0.5), fogOpacity);
				return float4(caustic, 1);
			}

			ENDHLSL
		}
	}
}