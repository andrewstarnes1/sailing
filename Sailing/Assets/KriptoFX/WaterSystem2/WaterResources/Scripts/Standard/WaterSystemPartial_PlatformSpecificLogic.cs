using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace KWS
{
    public partial class WaterSystem
    {
        ///////////////////////////// platform specific components /////////////////////////////////////////////////

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////

#if !KWS_URP && !KWS_HDRP

        internal static List<WaterSystemQualitySettings.ThirdPartyAssetDescription> ThirdPartyFogAssetsDescriptions = new List<WaterSystemQualitySettings.ThirdPartyAssetDescription>()
        {
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "Native Unity Fog" },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "Enviro", AssetNameSearchPattern = "Enviro - Sky and Weather", ShaderDefine = "ENVIRO_FOG", ShaderInclude = "EnviroFogCore.cginc" },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription()
                { EditorName = "Enviro 3", AssetNameSearchPattern = "Enviro 3 - Sky and Weather", ShaderDefine = "ENVIRO_3_FOG", ShaderInclude = "FogInclude.cginc", OverrideNativeCubemap = true },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "Azure", AssetNameSearchPattern         = "Azure[Sky]", ShaderDefine   = "AZURE_FOG", ShaderInclude     = "AzureFogCore.cginc" },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "Weather maker", AssetNameSearchPattern = "WeatherMaker", ShaderDefine = "WEATHER_MAKER", ShaderInclude = "WeatherMakerFogExternalShaderInclude.cginc" },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription()
                { EditorName = "Atmospheric height fog", AssetNameSearchPattern = "Atmospheric Height Fog", ShaderDefine = "ATMOSPHERIC_HEIGHT_FOG", ShaderInclude = "AtmosphericHeightFog.cginc", CustomQueueOffset = 2 },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription()
                { EditorName = "Volumetric fog and mist 2", AssetNameSearchPattern = "VolumetricFog", ShaderDefine = "VOLUMETRIC_FOG_AND_MIST", ShaderInclude = "VolumetricFogOverlayVF.cginc", DrawToDepth = true },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription()
                { EditorName = "COZY Weather 1 and 2", AssetNameSearchPattern = "Cozy Weather", ShaderDefine = "COZY_FOG", ShaderInclude = "StylizedFogIncludes.cginc", CustomQueueOffset = 2 },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription()
                { EditorName = "COZY Weather 3", AssetNameSearchPattern = "", IgnoreInclude = true, ShaderDefine = "COZY_FOG_3", ShaderInclude = "StylizedFogIncludes.cginc", CustomQueueOffset = 2 },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "AURA 2", AssetNameSearchPattern = "Aura 2", ShaderDefine = "AURA2", ShaderInclude = "AuraUsage.cginc" },
        };

#endif

#if KWS_URP
         internal static List<WaterSystemQualitySettings.ThirdPartyAssetDescription> ThirdPartyFogAssetsDescriptions = new List<WaterSystemQualitySettings.ThirdPartyAssetDescription>()
        {
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "Native Unity Fog", ShaderDefine   = "" },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "Enviro", AssetNameSearchPattern   = "Enviro - Sky and Weather", ShaderDefine   = "ENVIRO_FOG", ShaderInclude   = "EnviroFogCore.hlsl" },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "Enviro 3", AssetNameSearchPattern = "Enviro 3 - Sky and Weather", ShaderDefine = "ENVIRO_3_FOG", ShaderInclude = "FogIncludeHLSL.hlsl" },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "Azure", AssetNameSearchPattern    = "Azure[Sky]", ShaderDefine                 = "AZURE_FOG", ShaderInclude    = "AzureFogCore.cginc" },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "Atmospheric height fog", AssetNameSearchPattern = "Atmospheric Height Fog", ShaderDefine = "ATMOSPHERIC_HEIGHT_FOG", ShaderInclude =
 "AtmosphericHeightFog.cginc", CustomQueueOffset = 2 },
            // new ThirdPartyAssetDescription(){EditorName = "Volumetric fog and mist 2", ShaderDefine = "VOLUMETRIC_FOG_AND_MIST", ShaderInclude = "VolumetricFogOverlayVF.cginc", DrawToDepth = true},
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "COZY Weather 1 and 2", AssetNameSearchPattern = "Cozy Weather", ShaderDefine = "COZY_FOG", ShaderInclude = "StylizedFogIncludes.cginc", CustomQueueOffset = 2 },

            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "COZY Weather 3", AssetNameSearchPattern = "", IgnoreInclude = true, ShaderDefine = "COZY_FOG_3", ShaderInclude = "StylizedFogIncludes.cginc", CustomQueueOffset = 2 },
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "BUTO", AssetNameSearchPattern = "", IgnoreInclude = true, ShaderDefine = "BUTO", ShaderInclude = "Buto.hlsl" },
        };
#endif

#if KWS_HDRP
         internal static List<WaterSystemQualitySettings.ThirdPartyAssetDescription> ThirdPartyFogAssetsDescriptions = new List<WaterSystemQualitySettings.ThirdPartyAssetDescription>()
        {
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() {EditorName = "Native Unity Fog", ShaderDefine      = ""},
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() {EditorName = "Expanse", AssetNameSearchPattern     = "Expanse", ShaderDefine                    = "EXPANSE", ShaderInclude      = "transparency.hlsl"},
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() {EditorName = "Time Of Day", AssetNameSearchPattern = "Time Of Day", ShaderDefine                = "TIME_OF_DAY", ShaderInclude  = ""},
            new WaterSystemQualitySettings.ThirdPartyAssetDescription(){EditorName  = "Enviro", AssetNameSearchPattern      = "Enviro - Sky and Weather", ShaderDefine   = "ENVIRO_FOG", ShaderInclude   = "EnviroFogCore.hlsl"},
            new WaterSystemQualitySettings.ThirdPartyAssetDescription(){EditorName  = "Enviro 3", AssetNameSearchPattern    = "Enviro 3 - Sky and Weather", ShaderDefine = "ENVIRO_3_FOG", ShaderInclude = "FogIncludeHLSL.hlsl"},
            new WaterSystemQualitySettings.ThirdPartyAssetDescription(){EditorName = "Atmospheric height fog", AssetNameSearchPattern = "Atmospheric Height Fog", ShaderDefine = "ATMOSPHERIC_HEIGHT_FOG", ShaderInclude = "AtmosphericHeightFog.cginc", CustomQueueOffset
 = 2},
            //new ThirdPartyAssetDescription(){EditorName = "Volumetric fog and mist 2", ShaderDefine = "VOLUMETRIC_FOG_AND_MIST", ShaderInclude = "VolumetricFogOverlayVF.cginc", DrawToDepth = true},
            new WaterSystemQualitySettings.ThirdPartyAssetDescription() { EditorName = "COZY Weather 3", AssetNameSearchPattern = "", IgnoreInclude = true, ShaderDefine = "COZY_FOG_3", ShaderInclude = "StylizedFogIncludes.cginc", CustomQueueOffset = 2 },
        };

#endif

        static Light     _lastSun;
        static Transform _lastSunTransform;

        static void SetGlobalPlatformSpecificShaderParams(Camera cam)
        {
#if !KWS_HDRP && !KWS_URP

            var fogState = 0;
            if (RenderSettings.fog)
            {
                if (RenderSettings.fogMode      == FogMode.Linear) fogState             = 1;
                else if (RenderSettings.fogMode == FogMode.Exponential) fogState        = 2;
                else if (RenderSettings.fogMode == FogMode.ExponentialSquared) fogState = 3;
            }

            Shader.SetGlobalInt(KWS_ShaderConstants.DynamicWaterParams.KWS_FogState, fogState);
            Shader.SetGlobalTexture(KWS_ShaderConstants.ReflectionsID.KWS_SkyTexture, ReflectionProbe.defaultTexture);
            Shader.SetGlobalVector(KWS_ShaderConstants.ReflectionsID.KWS_SkyTexture_HDRDecodeValues, ReflectionProbe.defaultTextureHDRDecodeValues);

            var currentSun = RenderSettings.sun;
            if (currentSun != null)
            {
                if (_lastSun == null || _lastSun != currentSun)
                {
                    _lastSun          = currentSun;
                    _lastSunTransform = currentSun.transform;
                }

                Shader.SetGlobalVector(KWS_ShaderConstants.DynamicWaterParams.KWS_DirLightDireciton, -_lastSunTransform.forward);
                Shader.SetGlobalVector(KWS_ShaderConstants.DynamicWaterParams.KWS_DirLightColor,     _lastSun.color * _lastSun.intensity);
            }

            SphericalHarmonicsL2 sh;
            LightProbes.GetInterpolatedProbe(cam.transform.position, null, out sh);
            var ambient = new Vector3(sh[0, 0] - sh[0, 6], sh[1, 0] - sh[1, 6], sh[2, 0] - sh[2, 6]);
            ambient = Vector3.Max(ambient, Vector3.zero);
            Shader.SetGlobalVector(KWS_ShaderConstants.DynamicWaterParams.KWS_AmbientColor, ambient);

#endif

#if KWS_URP
                var fogState = 0;
                if (RenderSettings.fog)
                {
                    if (RenderSettings.fogMode      == FogMode.Linear) fogState = 1;
                    else if (RenderSettings.fogMode == FogMode.Exponential) fogState = 2;
                    else if (RenderSettings.fogMode == FogMode.ExponentialSquared) fogState = 3;
                }
                Shader.SetGlobalInt(KWS_ShaderConstants.DynamicWaterParams.KWS_FogState, fogState);
                //Shader.SetGlobalTexture(KWS_ShaderConstants.ReflectionsID.KWS_SkyTexture, ReflectionProbe.defaultTexture);

                SphericalHarmonicsL2 sh;
                LightProbes.GetInterpolatedProbe(cam.transform.position, null, out sh);
                var ambient = new Vector3(sh[0, 0] - sh[0, 6], sh[1, 0] - sh[1, 6], sh[2, 0] - sh[2, 6]);
                ambient = Vector3.Max(ambient, Vector3.zero);
                Shader.SetGlobalVector(KWS_ShaderConstants.DynamicWaterParams.KWS_AmbientColor, ambient);

#endif

#if KWS_HDRP
                Shader.SetGlobalTexture(KWS_ShaderConstants.ReflectionsID.KWS_SkyTexture, ReflectionProbe.defaultTexture);
                SphericalHarmonicsL2 sh;
                LightProbes.GetInterpolatedProbe(cam.transform.position, null, out sh);
                var ambient = new Vector3(sh[0, 0] - sh[0, 6], sh[1, 0] - sh[1, 6], sh[2, 0] - sh[2, 6]);
                ambient = Vector3.Max(ambient, Vector3.zero);
                Shader.SetGlobalVector(KWS_ShaderConstants.DynamicWaterParams.KWS_AmbientColor, ambient);

#endif
        }
    }
}