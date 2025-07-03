using UnityEngine;
using System.Collections.Generic;

namespace KWS
{
    internal static class KWS_Settings
    {
        

        public static readonly int MaskStencilValue = 32; //builtin 32, urp 8, hdrp 128

        public static class Water
        {
            public static readonly int DefaultWaterQueue     = 3000;
            public static readonly int UnderwaterQueueOffset = +1;
            public static readonly int WaterLayer            = 4; //water layer bit mask
            public static readonly int LightLayer            = 0; //light layer bit mask used in urp/hdrp

            public static readonly int   MaxRefractionDispersion   = 5;

           
            public static readonly Dictionary<WaterQualityLevelSettings.WaterMeshQualityEnum, int[]> QuadTreeChunkQuailityLevelsInfinite = new Dictionary<WaterQualityLevelSettings.WaterMeshQualityEnum, int[]>()
            {
                {WaterQualityLevelSettings.WaterMeshQualityEnum.Ultra, new[] {4, 6, 8, 12, 16, 20}},
                {WaterQualityLevelSettings.WaterMeshQualityEnum.High, new[] {2, 4, 6, 8, 12, 16}},
                {WaterQualityLevelSettings.WaterMeshQualityEnum.Medium, new[] {1, 2, 4, 6, 8, 12}},
                {WaterQualityLevelSettings.WaterMeshQualityEnum.Low, new[] {1, 2, 3, 4, 6, 8}},
                {WaterQualityLevelSettings.WaterMeshQualityEnum.VeryLow, new[] {1, 2, 3, 4, 5, 6}}
            };

            public static readonly Dictionary<WaterQualityLevelSettings.WaterMeshQualityEnum, int[]> QuadTreeChunkQuailityLevelsFinite = new Dictionary<WaterQualityLevelSettings.WaterMeshQualityEnum, int[]>()
            {
                {WaterQualityLevelSettings.WaterMeshQualityEnum.Ultra, new[] {2, 4, 6, 8, 10, 12}},
                {WaterQualityLevelSettings.WaterMeshQualityEnum.High, new[] {2, 4, 5, 6, 8, 10}},
                {WaterQualityLevelSettings.WaterMeshQualityEnum.Medium, new[] {1, 2, 3, 4, 6, 8}},
                {WaterQualityLevelSettings.WaterMeshQualityEnum.Low, new[] {1, 2, 3, 4, 5, 6}},
                {WaterQualityLevelSettings.WaterMeshQualityEnum.VeryLow, new[] {1, 1, 2, 3, 4, 5}}
            };

            public static readonly float[] QuadTreeChunkLodRelativeToWind = {0.5f, 0.75f, 1f, 1.5f, 2f, 2.5f};
            public static readonly int QuadTreeChunkLodOffsetForDynamicWaves = 5;

            public static readonly bool IsPostfxRequireDepthWriting = true;
        }

        public static class ResourcesPaths
        {
            public const string WaterSettingsProfileAssetName = "WaterSettings";
           
            public static readonly string KWS_FluidsFoamTex                  = "Textures/FluidsFoamTex";
            public static readonly string KWS_IntersectionFoamTex            = "Textures/IntersectionFoamTex";
            public static readonly string KWS_OceanFoamTex                   = "Textures/OceanFoamTex";
            public static readonly string KWS_SplashTex0                     = "Textures/WaterSplash_0";
            public static readonly string KWS_WaterDrops                     = "Textures/WaterDrops";
            public static readonly string KWS_WaterDropsMask                 = "Textures/WaterDropsMask";
            public static readonly string KWS_WaterDynamicWavesFlowMapNormal = "Textures/WaterDynamicWavesFlowMapNormal";

            public static readonly string KWS_DefaultVideoLoading = "Textures/KWS_DefaultVideoLoading";
        }

        public static class ShaderPaths
        {
            public static readonly string KWS_WaterDefines         = @"Includes/KWS_WaterDefines.cginc";
            
            #if !KWS_HDRP && !KWS_URP
                public static readonly string KWS_PlatformSpecificHelpers = @"Resources/PlatformSpecific/KWS_PlatformSpecificHelpers_Builtin.cginc";
            #endif
            
            #if KWS_URP
                public static readonly string KWS_PlatformSpecificHelpers = @"Resources/PlatformSpecific/KWS_PlatformSpecificHelpers_URP.cginc";
            #endif
                        
            #if KWS_HDRP
                public static readonly string KWS_PlatformSpecificHelpers = @"Resources/PlatformSpecific/KWS_PlatformSpecificHelpers_HDRP.cginc";
            #endif
        }

        public static class FFT
        {
            public static readonly float   MaxWindSpeed          = 50;
            public static readonly float MaxWavesAreaScale = 4;

            public static readonly float[] FftDomainSizes = { 5, 20, 100, 600 };
            public static readonly float[] FftDomainVisiableArea = { 40, 160, 800, 4800};
            public static readonly Vector4[] FftDomainScales =
            {
               /* new Vector4(1.0f,  0.5f,  1.0f,  0),
                new Vector4(0.95f, 0.4f,  0.95f, 0),
                new Vector4(0.95f, 0.45f, 0.95f, 0),
                new Vector4(0.9f,  0.5f,  0.9f,  0)*/

                new Vector4(1.0f, 0.5f, 1.0f, 0), //first cascade, micro detail waves, x z - choppiness, Y = height 
                new Vector4(1.0f,   0.5f, 1.0f, 0), //second cascade, small waves
                new Vector4(1.0f,   0.6f, 1.0f, 0), //third cascade, middle waves 
                new Vector4(1.0f,   0.9f, 1.0f, 0) //big waves 
            };

            public static readonly int     MaxLods               = 4;
        }

        public static class Caustic
        {
            public static readonly float CausticDecalHeight = 5000;
            public static readonly float MaxCausticDepth = 10;
        }

        public static class SurfaceDepth
        {
            public static readonly float MaxSurfaceDepthMeters = 50;
        }

      
        public static class VolumetricLighting
        {
            public static readonly float AbsorbtionOverrideMultiplier = 1;
            public static readonly int   MaxIterations                = 8;
        }

        public static class Reflection
        {
            public static readonly float MaxSunStrength               = 3;
            public static readonly float MaxSkyLodAtFarDistance       = 1.5f;
            
            #if KWS_HDRP
                public static readonly bool IsCloudRenderingAvailable    = true;
                public static readonly bool IsVolumetricsAndFogAvailable = true;
            #else 
                public static readonly bool IsCloudRenderingAvailable    = false;
                public static readonly bool IsVolumetricsAndFogAvailable = false;
            #endif
        }

        public static class Refraction
        {
            public static readonly bool IsRefractionDownsampleAvailable = true;
        }

        public static class DynamicWaves
        {
            public static readonly int MaxDynamicWavesTexSize = 2048;
        }

        public static class Mesh
        {
            public static readonly int SplineRiverMinVertexCount = 5;
            public static readonly int SplineRiverMaxVertexCount = 25;

            public static readonly float MaxTesselationFactorInfinite = 12;
            public static readonly float MaxTesselationFactorFinite   = 5;
            public static readonly float MaxTesselationFactorRiver    = 5;
            public static readonly float MaxTesselationFactorCustom    = 15;

            public static readonly int TesselationInfiniteMeshChunksSize = 2;
            public static readonly int TesselationFiniteMeshChunksSize   = 2;

            public static readonly float MaxInfiniteOceanDepth = 5000;


            public static readonly float QuadtreeInfiniteOceanMinDistance = 10.0f;
            public static readonly float QuadtreeFiniteOceanMinDistance = 20.0f;
            public static readonly float UpdateQuadtreeEveryMetersForward = 5f;
            public static readonly float UpdateQuadtreeEveryMetersBackward = 1.0f;
            public static readonly float QuadtreeRotationThresholdForUpdate = 0.005f;
            public static readonly float QuadTreeAmplitudeDisplacementMultiplier = 1.25f;

            public static readonly Vector3 MinFiniteSize = new Vector3(0.25f, 0.25f, 0.25f);
        }
    }
}
