using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KWS
{
    public class WaterSharedResources
    {
        internal static ReflectionProbe[]            ReflectionProbesCache;
       

        internal static void UpdateReflectionProbeCache()
        {
            //wtf, why urp/hdrp version doesnt have FindObjectsByType????
            //ReflectionProbesCache = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            ReflectionProbesCache = UnityEngine.Object.FindObjectsOfType<ReflectionProbe>();
        }

        internal static Texture2D KWS_FluidsFoamTex;
        internal static Texture2D KWS_IntersectionFoamTex;
        internal static Texture2D KWS_OceanFoamTex;
        internal static Texture2D KWS_SplashTex;
        internal static Texture2D KWS_WaterDropsTexture;
        internal static Texture2D KWS_WaterDropsMaskTexture;
        internal static Texture2D KWS_WaterDynamicWavesFlowMapNormal;


        internal static RTHandle  CameraOpaqueTexture;
        internal static RTHandle  CameraOpaqueTextureAfterWaterPass;

        #region PrePass

        public static RTHandle WaterPrePassRT0                  { get; internal set; } //(X) water surface instance ID (1-255) encoded to 8bit, (Y) water face mask (1=horizon, 0.75=front face, 0.5=back face), (Z) subsurface scattering, (W) tension mask (if enabled)
        public static RTHandle WaterPrePassRT1                  { get; internal set; } //(XY) world normal xz
        public static RTHandle WaterDepthRT      { get; internal set; }
        public static RTHandle WaterIntersectionHalfLineTensionMaskRT { get; internal set; }

        public static RTHandle WaterBackfacePrePassRT0 { get; internal set; }
        public static RTHandle WaterBackfacePrePassRT1 { get; internal set; }
        public static RTHandle WaterBackfaceDepthRT    { get; internal set; }

        public static RTHandle WaterMotionVectors       { get; internal set; }

        #endregion

        #region FftWaves

        public static RTHandle FftWavesDisplacement { get; internal set; }
        public static RTHandle FftWavesNormal { get; internal set; }
        public static RTHandle FftBuoyancyHeight { get; internal set; }

        //public static AsyncTextureSynchronizer<SurfaceData> FftGpuHeightAsyncData { get; internal set; }

        public static int FftHeightDataTexSize { get; internal set; }

      
        #endregion

        #region Reflection

        //internal static RTHandle SsrReflectionRaw;
        public static RTHandle   SsrReflection                  { get; internal set; }
        public static Vector2Int SsrReflectionCurrentResolution { get; internal set; }

        public static RenderTexture PlanarReflection { get; internal set; }
        public static int           PlanarInstanceID { get; internal set; }

        #endregion

        #region VolumetricLighting

        public static RTHandle VolumetricLightingRT { get; internal set; }
        public static RTHandle VolumetricLightingAdditionalDataRT { get; internal set; } //(R) dir light shadow for water surface + scene, (G) additional lights attenuation with shadow

        #endregion

        #region Caustic

        public static RTHandle CausticRTArray { get; internal set; }

        #endregion

        #region Shoreline
        public static Vector4  ShorelineAreaPosSize       { get; internal set; }
        public static RTHandle ShorelineWavesDisplacement { get; internal set; }
        public static RTHandle ShorelineWavesNormal       { get; internal set; }

        public static RTHandle ShorelineFoamParticlesRT { get; internal set; }

        #endregion

        #region DynamicWaves

        public static RTHandle      DynamicWavesMaskRT           { get; internal set; }
        public static RenderTexture DynamicWavesRT               { get; internal set; }
        public static RenderTexture DynamicWavesNormalsRT        { get; internal set; }
        public static RenderTexture DynamicWavesAdditionalDataRT { get; internal set; }
        


        #endregion

        #region OrthoDepth

        public static RenderTexture OrthoDepth            { get; internal set; }
        public static RenderTexture OrthoDepthBackface    { get; internal set; }
        public static RenderTexture OrthoDepthSDF         { get; internal set; }
        public static RenderTexture OrthoDepthDirection   { get; internal set; }
        public static Vector4       OrthoDepthPosition    { get; internal set; }
        public static Vector4       OrthoDepthNearFarSize { get; internal set; }

        public static Matrix4x4 OrthoDepthCameraMatrix { get; internal set; }

        #endregion

        #region WriteToPostfxDepth


        #endregion

    }
}
