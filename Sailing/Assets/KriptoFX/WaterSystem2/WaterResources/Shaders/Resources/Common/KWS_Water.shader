Shader "Hidden/KriptoFX/KWS/Water"
{
	Properties
	{
		srpBatcherFix ("srpBatcherFix", Float) = 0
		[HideInInspector]KWS_StencilMaskValue("KWS_StencilMaskValue", Int) = 32
	}

	SubShader
	{
		Tags { "Queue" = "Transparent-1" "IgnoreProjector" = "True" "RenderType" = "Transparent" "DisableBatching" = "true" }
		
		
		Stencil
		{
			Ref [KWS_StencilMaskValue]
            ReadMask [KWS_StencilMaskValue]
			Comp Greater
			Pass keep
		}

		
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite On

			Cull Back
			HLSLPROGRAM

			#pragma multi_compile _ KWS_USE_LOCAL_WATER_ZONES

			#pragma multi_compile _ KWS_DYNAMIC_WAVES_VISIBLE_ZONES_1 KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2 KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4 KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8
			#pragma multi_compile _ KWS_DYNAMIC_WAVES_USE_COLOR
			#pragma multi_compile _ KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE


 			#pragma multi_compile_fragment _ KWS_REFLECT_SUN
			#pragma multi_compile_fragment _ KWS_USE_VOLUMETRIC_LIGHT
			#pragma multi_compile_fragment _ KWS_SSR_REFLECTION
			#pragma multi_compile_fragment _ KWS_USE_PLANAR_REFLECTION
			#pragma multi_compile_fragment _ KWS_USE_REFRACTION_IOR
			#pragma multi_compile_fragment _ KWS_USE_REFRACTION_DISPERSION


			#define KWS_USE_WATER_INSTANCING
		
			#include "../PlatformSpecific/Includes/KWS_VertFragIncludes.cginc"

			#pragma vertex vertWater
			#pragma fragment fragWater
			#pragma target 4.6
			#pragma editor_sync_compilation

			ENDHLSL
		}
	}
}