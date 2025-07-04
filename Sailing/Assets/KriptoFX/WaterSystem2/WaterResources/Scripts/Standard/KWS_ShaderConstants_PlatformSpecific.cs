﻿using UnityEngine;

namespace KWS
{
    internal static class KWS_ShaderConstants_PlatformSpecific
    {

        public class LightsID
        {
            public static readonly int KWS_DirLightsBuffer = Shader.PropertyToID("KWS_DirLightsBuffer");
            public static readonly int KWS_PointLightsBuffer = Shader.PropertyToID("KWS_PointLightsBuffer");
            public static readonly int KWS_ShadowPointLightsBuffer = Shader.PropertyToID("KWS_ShadowPointLightsBuffer");
            public static readonly int KWS_SpotLightsBuffer = Shader.PropertyToID("KWS_SpotLightsBuffer");
            public static readonly int KWS_ShadowSpotLightsBuffer = Shader.PropertyToID("KWS_ShadowSpotLightsBuffer");

            public static readonly int KWS_DirLightsCount = Shader.PropertyToID("KWS_DirLightsCount");
            public static readonly int KWS_PointLightsCount = Shader.PropertyToID("KWS_PointLightsCount");
            public static readonly int KWS_ShadowPointLightsCount = Shader.PropertyToID("KWS_ShadowPointLightsCount");
            public static readonly int KWS_SpotLightsCount = Shader.PropertyToID("KWS_SpotLightsCount");
            public static readonly int KWS_ShadowSpotLightsCount = Shader.PropertyToID("KWS_ShadowSpotLightsCount");

            public static readonly int KWS_DirLightShadowParams = Shader.PropertyToID("KWS_DirLightShadowParams");

            public static readonly int KWS_UseDirLightShadow = Shader.PropertyToID("KWS_UseDirLightShadow");
            public static readonly int KWS_DirLightShadowStableFit = Shader.PropertyToID("KWS_DirLightShadowStableFit");
            public static readonly int KWS_DirLightShadowCascades = Shader.PropertyToID("KWS_DirLightShadowCascades");
        }

        public class LightKeywords
        {
            public static readonly string KWS_USE_DIR_LIGHT = "KWS_USE_DIR_LIGHT";

            public static readonly string KWS_USE_POINT_LIGHTS = "KWS_USE_POINT_LIGHTS";
            public static readonly string KWS_USE_SHADOW_POINT_LIGHTS = "KWS_USE_SHADOW_POINT_LIGHTS";
            public static readonly string KWS_USE_SPOT_LIGHTS = "KWS_USE_SPOT_LIGHTS";
            public static readonly string KWS_USE_SHADOW_SPOT_LIGHTS = "KWS_USE_SHADOW_SPOT_LIGHTS";

        }


        public static class CopyColorID
        {
            public static readonly int KWS_CameraOpaqueTexture               = Shader.PropertyToID("KWS_CameraOpaqueTexture");
            public static readonly int KWS_CameraOpaqueTexture_RTHandleScale = Shader.PropertyToID("KWS_CameraOpaqueTexture_RTHandleScale");

            public static readonly int KWS_CameraOpaqueTextureAfterWaterPass               = Shader.PropertyToID("KWS_CameraOpaqueTextureAfterWaterPass");
            public static readonly int KWS_CameraOpaqueTextureAfterWaterPass_RTHandleScale = Shader.PropertyToID("KWS_CameraOpaqueTextureAfterWaterPass_RTHandleScale");
        }
    }
}

