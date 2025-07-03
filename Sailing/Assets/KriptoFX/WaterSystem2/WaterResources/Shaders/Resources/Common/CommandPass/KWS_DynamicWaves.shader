Shader "Hidden/KriptoFX/KWS/DynamicWaves"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "" { }
	}

	HLSLINCLUDE

	#include "../../Common/KWS_WaterHelpers.cginc"

	struct vertexInputMaskMesh
	{
		float4 vertex : POSITION;
		float3 uv : TEXCOORD0;
	};

	struct v2fMaskMesh
	{
		float4 pos : SV_POSITION;
		float3 worldPos : TEXCOORD0;
	};

	struct v2fMaskProcedural
	{
		float4 pos : SV_POSITION;
		uint instanceID : TEXCOORD1;
		float3 worldPos : TEXCOORD2;
		nointerpolation float useWaterIntersection : TEXCOORD3;
	};

	struct MaskFragmentOutput
	{
		float4 data : SV_Target0;
		#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
			float4 color : SV_Target1;
		#endif
	};


	Texture2D KWS_PreviousTarget;
	Texture2D KWS_CurrentTarget;
	Texture2D KWS_CurrentAdditionalTarget;
	Texture2D KWS_PreviousAdditionalTarget;
	Texture2D KWS_CurrentNormalTarget;
	Texture2D KWS_CurrentColorTarget;

	float4 KWS_CurrentTarget_TexelSize;

	float3 KW_AreaOffset;
	float3 KW_LastAreaOffset;
	float KW_InteractiveWavesPixelSpeed;

	float KWS_DynamicWavesRainStrength;
	float KWS_DynamicWavesWaterSurfaceHeight;
	float KWS_DynamicWavesForce;
	float3 KWS_DynamicWavesForceDirection;
	float KWS_MeshIntersectionThreshold;
	float KWS_DynamicWavesGlobalForceScale;
	uint KWS_DynamicWavesUseWaterIntersection;
	uint KWS_DynamicWavesZoneInteractionType;

	uint KWS_CurrentFrame;

	float KWS_FoamStrengthRiver;
	float KWS_FoamStrengthShoreline;

	static const float2 QuadIndex[6] =
	{
		float2(-0.5, -0.5),
		float2(-0.5, 0.5),
		float2(0.5, 0.5),
		float2(0.5, 0.5),
		float2(0.5, -0.5),
		float2(-0.5, -0.5)
	};
	
	float sphIntersect(float3 ro, float3 rd, float4 sph)
	{
		float3 oc = ro - sph.xyz;
		float b = dot(oc, rd);
		float c = dot(oc, oc) - sph.w * sph.w;
		float h = b * b - c;
		if (h < 0.0) return -1.0;
		h = sqrt(h);
		return -b - h;
	}

	#define DYNAMIC_WAVES_MASK_OBSTACLE 1

	v2fMaskMesh vertMaskMesh(vertexInputMaskMesh v)
	{
		v2fMaskMesh o;
		o.pos = LocalPosToToClipPosOrtho(v.vertex.xyz);
		o.worldPos = LocalToWorldPos(v.vertex.xyz);
		return o;
	}

	v2fMaskProcedural vertMaskProcedural(uint instanceID : SV_InstanceID, float4 vertex : POSITION)
	{
		v2fMaskProcedural o;
		KWS_DynamicWavesMask data = KWS_DynamicWavesMaskBuffer[instanceID];
		float3 worldPos = mul(data.matrixTRS, float4(vertex.xyz, 1)).xyz;

		
		o.pos = WorldPosToToClipPosOrtho(worldPos);
		o.worldPos = worldPos;
		o.instanceID = instanceID;
		o.useWaterIntersection = data.useWaterIntersection;

		return o;
	}

	MaskFragmentOutput fragDrawMesh(v2fMaskMesh i) : SV_Target
	{
		MaskFragmentOutput o = (MaskFragmentOutput)0;
		
		float intersectionAlpha = 1;
		float intersectionMask = 0;

		// if (KWS_DynamicWavesUseWaterIntersection)
		// {
		// 	float currentWaterLevel = 0;
		// 	UNITY_BRANCH
		// 	if (KWS_WindSpeed > 7) currentWaterLevel = GetDynamicWavesHeightOffset(i.worldPos);
		// 	else currentWaterLevel = KWS_WaterPosition.y;
		//
		// 	intersectionMask = saturate(abs(currentWaterLevel - i.worldPos.y));
		// 	intersectionAlpha *= 1 - intersectionMask;
		// }
		//
		if (intersectionMask < 1)
		{
			/*float force = clamp(KWS_DynamicWavesForce, -1, 1) * 0.5 + 0.5;
			float2 forceDirection = clamp(KWS_DynamicWavesForceDirection.xz, -1, 1) * 0.5 + 0.5;
			o.data = float4(force * saturate(intersectionAlpha * 5), forceDirection, intersectionAlpha);*/
			o.data = float4(0.5, 0.5, 0.5, intersectionAlpha);
			
			//#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
			//	if(data.useColor == 1) o.color = data.color;
			//#endif

			return o;
		}
		else discard;

		o.data = float4(0.5, 0.5, 0.5, 0);
		return o;
	}


	MaskFragmentOutput fragDrawProcedural(v2fMaskProcedural i) : SV_Target
	{
		MaskFragmentOutput o = (MaskFragmentOutput)0;

		KWS_DynamicWavesMask data = KWS_DynamicWavesMaskBuffer[i.instanceID];
		float halfSize = KWS_MAX(data.size) * 0.5;
		float intersectionAlpha = 1;

		float2 zoneUV = GetDynamicWavesUV(i.worldPos);
		float4 dynamicWaves = GetDynamicWavesZone(zoneUV);

		//float currentHeight = dynamicWaves.z + dynamicWaves.w + KWS_WaterPosition.y;

		//if (i.worldPos.y < currentHeight)
		//{
		//	o.data = float4(0.5, 0.5, 0.5, 0);
		//	return o;
		//};
		
		if (i.useWaterIntersection)
		{
			float currentWaterLevel = 0;
			UNITY_BRANCH
			if (KWS_WindSpeed > 7) currentWaterLevel = GetDynamicWavesHeightOffset(i.worldPos, zoneUV, dynamicWaves);
			else currentWaterLevel = KWS_WaterPosition.y;
				
			float intersectionMask = (abs(currentWaterLevel - data.position.y)) / halfSize;
			intersectionAlpha *= 1 - intersectionMask;
		}
		
		
		data.force = clamp(data.force, -1, 1) * 0.5 + 0.5;
		data.forceDirection.xz = clamp(data.forceDirection.xz, -1, 1) * 0.5 + 0.5;
		
		o.data = float4(data.force * saturate(intersectionAlpha * 5), data.forceDirection.xz, intersectionAlpha);
		
		#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
			if(data.useColor == 1) o.color = data.color;
		#endif

		return o;
	}

	
	struct vertexInput
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
		uint vertexID : SV_VertexID;
	};
	

	struct v2f
	{
		float4 vertex : SV_POSITION;
		float3 worldPos : TEXCOORD0;
		float2 uv : TEXCOORD1;
	};
	
	v2f vert(vertexInput v)
	{
		v2f o;
		
		o.vertex = GetTriangleVertexPosition(v.vertexID);
		o.uv = GetTriangleUVScaled(v.vertexID);
		float2 worldUV = o.uv * KWS_DynamicWavesZoneSize.xz - KWS_DynamicWavesZoneSize.xz * 0.5;
		worldUV = RotateDynamicWavesCoord(worldUV);
		o.worldPos = float3(worldUV.x, 0, worldUV.y) + KWS_DynamicWavesZonePosition.xyz;
		

		o.uv += KW_AreaOffset.xz;
		return o;
	}

	struct SimulationFragmentOutput
	{
		float4 data : SV_Target0;
		float3 normal : SV_Target1;
		float4 additionalData : SV_Target2;

		#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
			float4 colorData : SV_Target3;
		#endif
	};

	float RainNoise(float2 p, float threshold)
	{
		p = p * 9757.0 + frac(KWS_Time);
		float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
		p3 += dot(p3, p3.yxz + 33.33);
		float3 noise = frac((p3.xxy + p3.yzz) * p3.zyx);
		return (noise.x * noise.y * noise.z) > threshold;
	}


	#define MINIMUM_WATER_HEIGHT  0.001
	#define GRID_CELL_SIZE 1
	#define TIME_STEP 0.07
	#define GRAVITY 10.0
	#define ADVECT_SPEED 1.25
	#define ADVECT_NOISE_SCALE 1.5


	SimulationFragmentOutput frag1(v2f i) : SV_Target
	{
		SimulationFragmentOutput o = (SimulationFragmentOutput)0;
		float2 border = KWS_CurrentTarget_TexelSize.xy * 3;
		UNITY_BRANCH
		if (KWS_CurrentFrame < 3)
		//|| i.uv.x <= border.x || i.uv.x >= 1 - border.x
		//|| i.uv.y <= border.y || i.uv.y >= 1 - border.y)

		{
			o.data = PackSimulation(float4(0, 0, 0, 0));
			o.additionalData = float4(0, 1, 0, 0);
			o.normal = float4(0.5, 0.5, 0.5, 0.5);
			#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
				o.colorData = float4(0, 0, 0, 0);
			#endif
			return o;
		}
		
		float4 center = UnpackSimulation(KWS_CurrentTarget.SampleLevel(sampler_linear_clamp, i.uv, 0));
		float4 right = UnpackSimulation(KWS_CurrentTarget.SampleLevel(sampler_linear_clamp, i.uv + float2(KWS_CurrentTarget_TexelSize.x, 0), 0));
		float4 left = UnpackSimulation(KWS_CurrentTarget.SampleLevel(sampler_linear_clamp, i.uv - float2(KWS_CurrentTarget_TexelSize.x, 0), 0));
		float4 top = UnpackSimulation(KWS_CurrentTarget.SampleLevel(sampler_linear_clamp, i.uv + float2(0, KWS_CurrentTarget_TexelSize.y), 0));
		float4 down = UnpackSimulation(KWS_CurrentTarget.SampleLevel(sampler_linear_clamp, i.uv - float2(0, KWS_CurrentTarget_TexelSize.y), 0));
		
		#ifdef KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE
			float rawOrthoDepth = -1000;
		#else
			float rawOrthoDepth = GetWaterOrthoDepth(i.uv);
		#endif
		float dynamicWaveMaskDepth = GetDynamicWavesZoneDepthMaskBicubic(i.uv);
		
		float orthoDepth = max(rawOrthoDepth, dynamicWaveMaskDepth);
		float dynamicDepthDiff = orthoDepth - rawOrthoDepth;
		float wetMapDepth = EncodeDynamicWavesHeight(orthoDepth);
		orthoDepth -= KWS_WaterPosition.y;
		
		float4 additionalData = KWS_CurrentAdditionalTarget.SampleLevel(sampler_linear_clamp, i.uv, 0);
		//float4 lastCenter = KWS_PreviousAdditionalTarget.SampleLevel(sampler_linear_clamp, i.uv, 0);
		
		float heightLeft = (left.x >= 0.0) ?                 left.z : center.z;
		float heightRight = (center.x <= 0.0) ?                 right.z : center.z;
		float heightDown = (down.y >= 0.0) ?                 down.z : center.z;
		float heightTop = (center.y <= 0.0) ?                 top.z : center.z;

		float MAX_DEPTH_LIMIT = 2.0;
		float maxAverageHeight = MAX_DEPTH_LIMIT * GRID_CELL_SIZE / (GRAVITY * TIME_STEP);
		float heightAdjustment = max(orthoDepth > 0 ?          0 : -2, (left.z + right.z + down.z + top.z) / 4.0 - maxAverageHeight);

		heightLeft -= heightAdjustment;
		heightRight -= heightAdjustment;
		heightDown -= heightAdjustment;
		heightTop -= heightAdjustment;

		float heightChange = - ((heightRight * center.x - heightLeft * left.x) / GRID_CELL_SIZE
		+ (heightTop * center.y - heightDown * down.y) / GRID_CELL_SIZE);

		center.z += heightChange * TIME_STEP;

		////advection
		float dt = -ADVECT_SPEED * TIME_STEP / GRID_CELL_SIZE;
		float2 time = frac(KWS_ScaledTime * 0.001) * 1000;
		float2 noise = float2(SimpleNoise1(i.uv * 100 + center.xy * 0.1 + time * 2.5), SimpleNoise1(i.uv * 100 + center.xy * 0.1 - (time * 2.5 + 40)));
		noise = lerp(noise, noise * length(center.xy), 0.65) * ADVECT_NOISE_SCALE;
		center.xy = UnpackSimulation(KWS_CurrentTarget.SampleLevel(sampler_linear_clamp, i.uv + ((center.xy + noise) * dt) * KWS_CurrentTarget_TexelSize.xy, 0)).xy;

		// Dynamic wave adjustments
		
		#ifdef KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE
			float sdfDepth = 1000;
		#else
			float sdfDepth = GetWaterOrthoDepthSDF(i.uv);
		#endif

		float4 dynamicWaveMask = GetDynamicWavesMask(i.uv);
		
		float borderFade = GetDynamicWavesBorderFading(i.uv);
		//borderFade = 1;

		float orthoDepthWaterMask = orthoDepth - center.z < 0;
		float shorelineMaskFade = saturate(smoothstep(2, 20, sdfDepth)) * orthoDepthWaterMask;
		float shorelineMask = saturate(smoothstep(0, 5, sdfDepth)) * orthoDepthWaterMask;

		
		float3 fftWaveDisplacement = GetFftWavesDisplacementDynamicWaves(i.worldPos);

		float normalizedWind = saturate(KWS_WindSpeed / 30.0);
		float diplacementRelativeToWind = lerp(0.025, 0.01, normalizedWind);
		float diplacementRelativeToDistance = lerp(diplacementRelativeToWind, 0.01, smoothstep(2, 100, sdfDepth));
		fftWaveDisplacement *= diplacementRelativeToDistance;

		noise = float2(SimpleNoise1(i.uv * 30 + center.xy + time * 2.5), SimpleNoise1(i.uv * 30 + center.xy + (time * 2.5 + 40)));
		fftWaveDisplacement.xz *= lerp(1, 2, noise.xy);
		fftWaveDisplacement.y += fftWaveDisplacement.y * noise.y * 0.125;
		fftWaveDisplacement *= shorelineMaskFade;
		
		if (center.z < 7)
		{
			center.z += (fftWaveDisplacement.y);
			center.xy += fftWaveDisplacement.xz * 0.5;
		}
		
		//wet mask
		float wetMaskFrameFading = (KWS_CurrentFrame % 15) == 0 ?          1.0 / 128.0 : 0;
		additionalData.x *= lerp(1, 0.992, additionalData.x > 0.9);
		additionalData.x -= wetMaskFrameFading;

		float wetMap = saturate((saturate(center.z) - 0.05) * 10);
		wetMap *= lerp(0.0001, 1, saturate(center.z * 4));
		wetMap = saturate(additionalData.x + wetMap);
		
		
		//foam mask
		additionalData.z = KWS_CurrentAdditionalTarget.SampleLevel(sampler_linear_clamp, i.uv + ((center.xy + noise) * dt) * KWS_CurrentTarget_TexelSize.xy, 0).z;
		additionalData.z = saturate(additionalData.z - 0.004);
		//additionalData.z *= 0.995;

		float divergence = length(right.xy - left.xy) + length(top.xy - down.xy);
		float foamHeightThreshold = 1 + noise * 0.25;
		float divergenceStrength = lerp(KWS_FoamStrengthRiver, KWS_FoamStrengthShoreline, saturate(shorelineMask * 10));

		float newFoamValue = saturate(saturate((heightChange - foamHeightThreshold) * 0.25) * divergenceStrength + saturate(divergence * divergence * 0.05) * divergenceStrength);
		float foamMask = saturate(additionalData.z + newFoamValue);
		


		if (orthoDepth < 0.00001)
		{
			if (orthoDepth < KWS_WaterPosition.y)
			{
				center.z = max(center.z, MINIMUM_WATER_HEIGHT);
			}
			
			orthoDepth = 0;
			wetMap = 1;
		}

		center.xy -= dynamicWaveMask.yz * 10;
		if (center.z < 10) center.z += dynamicWaveMask.x;
		center.w = orthoDepth + dynamicWaveMask.x * 0;
		center.xy *= lerp(0.1f, 1, wetMap);

		float currentWorldHeight = center.z + center.w + KWS_WaterPosition.y;
		if (currentWorldHeight - 0.01 < dynamicWaveMaskDepth && center.z > MINIMUM_WATER_HEIGHT) center.z -= MINIMUM_WATER_HEIGHT;
		if (dynamicWaveMaskDepth > rawOrthoDepth) center.xy = clamp(center.xy, -1, 1);
		
		center.xyz *= saturate(borderFade * 10);
		foamMask *= borderFade;
		
		float dHeightX = ((right.z + right.w) - (left.z + left.w)) / (2.0 * KWS_CurrentTarget_TexelSize.x);
		float dHeightZ = ((top.z + top.w) - (down.z + down.w)) / (2.0 * KWS_CurrentTarget_TexelSize.y);
		float3 normal = normalize(float3(dHeightX, max(KWS_DynamicWavesZoneSize.x, KWS_DynamicWavesZoneSize.z), dHeightZ));
		
		o.data = PackSimulation(center);
		o.normal = float3(-normal.x, normal.y, -normal.z);
		o.additionalData = float4(wetMap, shorelineMaskFade, foamMask, wetMapDepth);

		
		//color mask
		#ifdef KWS_DYNAMIC_WAVES_USE_COLOR
			float4 dynamicWaveMaskColor = GetDynamicWavesMaskColor(i.uv);
			
			float4 colorData = KWS_CurrentColorTarget.SampleLevel(sampler_linear_clamp, i.uv, 0);
			float4 colorDataAdvected = KWS_CurrentColorTarget.SampleLevel(sampler_linear_clamp, i.uv + (center.xy * dt * 1.5) * KWS_CurrentTarget_TexelSize.xy, 0);
			float4 colorDataAdvected2 = KWS_CurrentColorTarget.SampleLevel(sampler_linear_clamp, i.uv - (center.xy * dt * 0.5) * KWS_CurrentTarget_TexelSize.xy, 0);
			colorDataAdvected = max(colorDataAdvected, colorDataAdvected2);

			float shorelineColorFading = (shorelineMaskFade > 0.01) ?      0.95 : 1;
			colorData.a *= shorelineColorFading;
			
			float4 finalColor = 0;
			finalColor.rgb = max(max(colorData.rgb, colorDataAdvected.rgb * (1 + 1.0 / 128.0)), dynamicWaveMaskColor.rgb);
			finalColor.a = max(max(colorData.a, colorDataAdvected.a), dynamicWaveMaskColor.a);
			
			o.colorData = finalColor * saturate(borderFade * 10);
		#endif


		return o;
	}

	float4 frag2(v2f i) : SV_Target
	{
		
		
		float4 center = UnpackSimulation(KWS_CurrentTarget.SampleLevel(sampler_linear_clamp, i.uv, 0));
		float4 right = UnpackSimulation(KWS_CurrentTarget.SampleLevel(sampler_linear_clamp, i.uv + float2(KWS_CurrentTarget_TexelSize.x, 0), 0));
		float4 left = UnpackSimulation(KWS_CurrentTarget.SampleLevel(sampler_linear_clamp, i.uv - float2(KWS_CurrentTarget_TexelSize.x, 0), 0));
		float4 top = UnpackSimulation(KWS_CurrentTarget.SampleLevel(sampler_linear_clamp, i.uv + float2(0, KWS_CurrentTarget_TexelSize.y), 0));
		float4 down = UnpackSimulation(KWS_CurrentTarget.SampleLevel(sampler_linear_clamp, i.uv - float2(0, KWS_CurrentTarget_TexelSize.y), 0));
		
		float4 additionalData = KWS_CurrentAdditionalTarget.SampleLevel(sampler_linear_clamp, i.uv, 0);
	
		float shorelineMaskFade = additionalData.y;

		float currentHeight = center.z + center.w;
		float rightHeight = right.z + right.w;
		float topHeight = top.z + top.w;
		
		float2 velocityChange;
		velocityChange.x = -GRAVITY / GRID_CELL_SIZE * (rightHeight - currentHeight);
		velocityChange.y = -GRAVITY / GRID_CELL_SIZE * (topHeight - currentHeight);
		center.xy += velocityChange * TIME_STEP;
		
		#if !defined(KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE)
			
			if (((center.z <= MINIMUM_WATER_HEIGHT * GRID_CELL_SIZE) && (center.w > rightHeight)) ||
			((right.z <= MINIMUM_WATER_HEIGHT * GRID_CELL_SIZE) && (right.w > currentHeight)))
			{
				center.x *= 0.0;
			}

			if (((center.z <= MINIMUM_WATER_HEIGHT * GRID_CELL_SIZE) && (center.w > topHeight)) ||
			((top.z <= MINIMUM_WATER_HEIGHT * GRID_CELL_SIZE) && (top.w > currentHeight)))
			{
				center.y *= 0.0;
			}

		//	if (center.z <= MINIMUM_WATER_HEIGHT * 5) center.xy *= 0.75;

		#endif

		
		
		float velocityLength = length(center.xy);
		if (velocityLength > 0.0)
		{
			float VELOCITY_LIMIT_FACTOR = 0.5;
			center.xy /= velocityLength;
			velocityLength = min(velocityLength, GRID_CELL_SIZE / TIME_STEP * VELOCITY_LIMIT_FACTOR);
			center.xy *= velocityLength;
		}
		

		if (center.z < MINIMUM_WATER_HEIGHT * 5)
		{
			center.w = 0;
			center.z *= 0.75;
			if (right.z > MINIMUM_WATER_HEIGHT * 5 || top.z > MINIMUM_WATER_HEIGHT * 5
			|| left.z > MINIMUM_WATER_HEIGHT * 5 || down.z > MINIMUM_WATER_HEIGHT * 5) center.w = min(top.w, min(down.w, min(left.w, right.w)));
		}
		
		if (shorelineMaskFade > 0.01) center.xy *= 0.995;
		

		return PackSimulation(center);
	}


	ENDHLSL

	Subshader
	{

		//0 draw mesh mask
		Pass
		{
			ZTest LEqual
			Cull Back
			ZWrite On
			//Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex vertMaskMesh
			#pragma fragment fragDrawMesh
			#pragma target 4.6
			#pragma editor_sync_compilation

			#pragma multi_compile_fragment _ KWS_DYNAMIC_WAVES_USE_COLOR

			ENDHLSL
		}

		//1 draw procedural mask
		Pass
		{
			ZTest Always
			Cull Off
			ZWrite Off

			Blend 0 SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex vertMaskProcedural
			#pragma fragment fragDrawProcedural

			#pragma multi_compile_fragment _ KWS_DYNAMIC_WAVES_USE_COLOR
			#pragma editor_sync_compilation

			#pragma target 4.6
			ENDHLSL
		}
		
		//2
		Pass
		{
			ZTest Always
			Cull Off
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag1

			#pragma shader_feature _ KWS_DYNAMIC_WAVES_BAKE_MODE
			#pragma multi_compile_fragment _ KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE
			#pragma multi_compile_fragment _ KW_USE_RAIN_EFFECT
			#pragma multi_compile_fragment _ KWS_DYNAMIC_WAVES_USE_COLOR
			#pragma editor_sync_compilation

			#pragma target 4.6
			ENDHLSL
		}
		
		//3
		Pass
		{
			ZTest Always
			Cull Off
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag2

			#pragma shader_feature _ KWS_DYNAMIC_WAVES_BAKE_MODE
			#pragma multi_compile_fragment _ KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE
			#pragma multi_compile_fragment _ KW_USE_RAIN_EFFECT
			#pragma multi_compile_fragment _ KWS_DYNAMIC_WAVES_USE_COLOR
			#pragma editor_sync_compilation

			#pragma target 4.6
			ENDHLSL
		}

		////3
		//Pass
		//{
		//	ZTest Always
		//	Cull Off
		//	ZWrite Off

		//	HLSLPROGRAM
		//	#pragma vertex vert
		//	#pragma fragment fragLinearWaves
		//	#pragma multi_compile _ KW_USE_RAIN_EFFECT
		//	#pragma target 4.6
		//	ENDHLSL
		//}

	}
}