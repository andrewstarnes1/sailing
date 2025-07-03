
using UnityEngine;
using UnityEngine.Rendering;
#if KWS_URP
using UnityEngine.Rendering.Universal;
#endif
#if KWS_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif
using UnityEngine.XR;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KWS
{
    internal static partial class KWS_CoreUtils
    {
        private static Light _lastDirLight;
        private static float _sunIntensityBeforeCameraEnterUnderwater;
        private static float _environmentLightingBeforeCameraEnterUnderwater;


        static bool CanRenderWaterForCurrentCamera_PlatformSpecific(Camera cam)
        {

            
            #if KWS_URP
                var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (camData != null && camData.renderType == UnityEngine.Rendering.Universal.CameraRenderType.Overlay) return false;
                return true;
            #else
                 return true;
            #endif
        }

        public static Vector2Int GetCameraRTHandleViewPortSize(Camera cam)
        {
#if ENABLE_VR_MODULE && ENABLE_VR && ENABLE_XR_MODULE
            if (XRSettings.enabled)
            {
                return new Vector2Int(XRSettings.eyeTextureWidth, XRSettings.eyeTextureHeight);
            }
            else
#endif
            {
                var viewPortSize = RTHandles.rtHandleProperties.currentViewportSize;
                if (viewPortSize.x == 0 || viewPortSize.y == 0) return new Vector2Int(cam.pixelWidth, cam.pixelHeight);
                else return viewPortSize;
            }

        }

        public static bool CanRenderSinglePassStereo(Camera cam)
        {
#if ENABLE_VR_MODULE && ENABLE_VR && ENABLE_XR_MODULE
            return XRSettings.enabled &&
                   (XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced && cam.cameraType != CameraType.SceneView);
#else
            return false;
#endif
        }

        public static bool IsSinglePassStereoActive()
        {
#if ENABLE_VR_MODULE && ENABLE_VR && ENABLE_XR_MODULE
            return XRSettings.enabled && XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced;
#else
            return false;
#endif
        }

        public static void UniversalCameraRendering(Camera camera, ScriptableRenderContext context)
        {
            #if !KWS_HDRP && !KWS_URP
                camera.Render();
            #endif  
            
            #if KWS_URP
                UniversalRenderPipeline.RenderSingleCamera(context, camera);
            #endif
        }

        
        public static void SetComputeShadersDefaultPlatformSpecificValues(this CommandBuffer cmd, ComputeShader cs, int kernel)
        {
            #if KWS_HDRP
                cmd.SetComputeTextureParam(cs, kernel, "_AirSingleScatteringTexture",     KWS_CoreUtils.DefaultBlack3DTexture);
                cmd.SetComputeTextureParam(cs, kernel, "_AerosolSingleScatteringTexture", KWS_CoreUtils.DefaultBlack3DTexture);
                cmd.SetComputeTextureParam(cs, kernel, "_MultipleScatteringTexture",      KWS_CoreUtils.DefaultBlack3DTexture);
            #endif
        }

        public static void RenderDepth(Camera depthCamera, RenderTexture depthRT, bool isBackface = false)
        {
            
            //drawInstanced doesnt work in hdrp and cause bugs
            
            var currentShadowDistance = QualitySettings.shadowDistance;
            var lodBias               = QualitySettings.lodBias;

            var terrains                                            = Terrain.activeTerrains;
            var pixelError                                          = new float[terrains.Length];
            //var drawInstanced = new bool[terrains.Length];
            var drawTrees     = new bool[terrains.Length];

            for (var i = 0; i < terrains.Length; i++)
            {
                pixelError[i] = terrains[i].heightmapPixelError;
                drawTrees[i]     = terrains[i].drawTreesAndFoliage;
                //drawInstanced[i] = terrains[i].drawInstanced;
            }

            try
            {
                QualitySettings.shadowDistance = 0;
                QualitySettings.lodBias        = 10;

                if(isBackface) GL.invertCulling = true;

                foreach (var terrain in terrains)
                {
                    terrain.heightmapPixelError = 1;
                    terrain.drawTreesAndFoliage = false;
                    //terrain.drawInstanced       = true;
                }

                depthCamera.targetTexture = depthRT;
                depthCamera.Render();
                
            }
            finally
            {
                for (var i = 0; i < terrains.Length; i++)
                {
                    terrains[i].heightmapPixelError = pixelError[i];
                    terrains[i].drawTreesAndFoliage = drawTrees[i];
                   // terrains[i].drawInstanced       = drawInstanced[i];
                }

                QualitySettings.shadowDistance = currentShadowDistance;
                QualitySettings.lodBias        = lodBias;
                if (isBackface) GL.invertCulling = false;
            }
        }

        
        #if KWS_HDRP
     
        public static void SetCameraFrameSetting(this Camera cam, FrameSettingsField setting, bool enabled)
        {
            var cameraData = cam.GetComponent<HDAdditionalCameraData>();
            if (cameraData == null) cameraData = cam.gameObject.AddComponent<HDAdditionalCameraData>();
            SetCameraFrameSetting(cameraData, setting, enabled);
        }

        public static void SetCameraFrameSetting(this HDAdditionalCameraData cameraData, FrameSettingsField setting, bool enabled)
        {
            var frameSettings = cameraData.renderingPathCustomFrameSettings;
            var frameSettingsOverrideMask = cameraData.renderingPathCustomFrameSettingsOverrideMask;

            frameSettingsOverrideMask.mask[(uint)setting] = true;
            frameSettings.SetEnabled(setting, enabled);

            cameraData.renderingPathCustomFrameSettings = frameSettings;
            cameraData.renderingPathCustomFrameSettingsOverrideMask = frameSettingsOverrideMask;
        }

        public static void SetFrameSetting(this HDAdditionalReflectionData reflData, FrameSettingsField setting, bool enabled)
        {
            var frameSettings             = reflData.frameSettings;
            var frameSettingsOverrideMask = reflData.frameSettingsOverrideMask;

            frameSettingsOverrideMask.mask[(uint)setting] = true;
            frameSettings.SetEnabled(setting, enabled);

            reflData.frameSettings = frameSettings;
            reflData.frameSettingsOverrideMask = frameSettingsOverrideMask;
        }

        public static void SetFrameSetting(this PlanarReflectionProbe reflData, FrameSettingsField setting, bool enabled)
        {
            var frameSettings             = reflData.frameSettings;
            var frameSettingsOverrideMask = reflData.frameSettingsOverrideMask;

            frameSettingsOverrideMask.mask[(uint)setting] = true;
            frameSettings.SetEnabled(setting, enabled);

            reflData.frameSettings             = frameSettings;
            reflData.frameSettingsOverrideMask = frameSettingsOverrideMask;
        }


        public static void SetFrameSetting(this CameraSettings settings, FrameSettingsField setting, bool enabled)
        {
            var frameSettings             = settings.renderingPathCustomFrameSettings;
            var frameSettingsOverrideMask = settings.renderingPathCustomFrameSettingsOverrideMask;

            frameSettingsOverrideMask.mask[(uint)setting] = true;
            frameSettings.SetEnabled(setting, enabled);

            settings.renderingPathCustomFrameSettings = frameSettings;
            settings.renderingPathCustomFrameSettingsOverrideMask = frameSettingsOverrideMask;
        }

        public static void DisableAllCameraFrameSettings(this PlanarReflectionProbe reflData)
        {
            var frameSettings             = reflData.frameSettings;
            var frameSettingsOverrideMask = reflData.frameSettingsOverrideMask;


            frameSettingsOverrideMask.mask = new BitArray128(ulong.MaxValue, ulong.MaxValue);

            for (uint i = 0; i < frameSettingsOverrideMask.mask.capacity; i++)
            {
                frameSettings.SetEnabled((FrameSettingsField)i, false);
            }


            reflData.frameSettings = frameSettings;
            reflData.frameSettingsOverrideMask = frameSettingsOverrideMask;
        }

        public static void DisableAllCameraFrameSettings(this HDAdditionalCameraData cameraData)
        {
            var frameSettings             = cameraData.renderingPathCustomFrameSettings;
            var frameSettingsOverrideMask = cameraData.renderingPathCustomFrameSettingsOverrideMask;

           
            frameSettingsOverrideMask.mask = new BitArray128(ulong.MaxValue, ulong.MaxValue);

            for (uint i = 0; i < frameSettingsOverrideMask.mask.capacity; i++)
            {
                frameSettings.SetEnabled((FrameSettingsField) i, false);
            }


            cameraData.renderingPathCustomFrameSettings             = frameSettings;
            cameraData.renderingPathCustomFrameSettingsOverrideMask = frameSettingsOverrideMask;
        }

        
        #endif
        
        
        
    }

}