#if !KWS_HDRP && !KWS_URP

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KWS
{

    internal class KWS_WaterPassHandler
    {
        List<WaterPass> _waterPasses;

        VolumetricLightingPrePass            _volumetricLightingPrePass    = new();
        FftWavesPass                         _fftWavesPass                 = new();
        BuoyancyPass                         _buoyancyPass                 = new();
        DynamicWavesPass                     _dynamicWavesPass             = new();
        
        WaterPrePass           _waterPrePass           = new();
        MotionVectorsPass      _motionVectorsPass      = new();
        CausticPrePass         _causticPrePass         = new();
        VolumetricLightingPass _volumetricLightingPass = new();
        CausticDecalPass       _causticDecalPass       = new();
        CopyColorPass          _copyColorPass          = new();
        CopyWaterColorPass     _copyWaterColorPass     = new();

        ScreenSpaceReflectionPass    _ssrPass              = new();
        private PlanarReflectionPass _planarReflectionPass = new();
        ReflectionFinalPass          _reflectionFinalPass  = new();
        DrawMeshPass                 _drawMeshPass         = new();
        UnderwaterPass               _underwaterPass       = new();
        DrawToPosteffectsDepthPass   _drawToDepthPass      = new();



        internal KWS_WaterPassHandler()
        {
            _waterPasses = new List<WaterPass>
            {
                _volumetricLightingPrePass, _fftWavesPass, _buoyancyPass, _dynamicWavesPass,
                _waterPrePass, _motionVectorsPass, _causticPrePass, _volumetricLightingPass, _causticDecalPass, _copyColorPass, _copyWaterColorPass,
                _ssrPass, _planarReflectionPass, _reflectionFinalPass, _drawMeshPass, _underwaterPass, _drawToDepthPass
            };

            _dynamicWavesPass.cameraEvent   = CameraEvent.BeforeReflections;
            _drawToDepthPass.cameraEvent    = CameraEvent.AfterForwardAlpha;
            _copyWaterColorPass.cameraEvent = CameraEvent.AfterForwardAlpha;
            _underwaterPass.cameraEvent     = CameraEvent.AfterForwardAlpha;
        }


        public void OnBeforeFrameRendering(HashSet<Camera> cameras, CustomFixedUpdates fixedUpdates)
        {
            foreach (var waterPass in _waterPasses) waterPass.ExecutePerFrame(cameras, fixedUpdates);
        }

        public void OnBeforeCameraRendering(Camera cam, ScriptableRenderContext context)
        {
            try
            {

                OverrideCameraRequiredSettings(cam);
                
                var cameraSize = KWS_CoreUtils.GetScreenSizeLimited(KWS_CoreUtils.SinglePassStereoEnabled);
                KWS_CoreUtils.RTHandles.SetReferenceSize(cameraSize.x, cameraSize.y);

                WaterPass.WaterPassContext waterContext = default;
                waterContext.cam         = cam;
                waterContext.cameraDepth = cam.actualRenderingPath == RenderingPath.Forward ? BuiltinRenderTextureType.Depth : BuiltinRenderTextureType.ResolvedDepth;
                waterContext.cameraColor = BuiltinRenderTextureType.CameraTarget;

                foreach (var waterPass in _waterPasses)
                {
                    waterPass.ExecuteBeforeCameraRendering(cam, context);
                    //waterPass.ExecuteInjectionPointPass(waterContext); //cause bug with command buffer and editor camera
                }

                _dynamicWavesPass.ExecuteInjectionPointPass(waterContext);
                _waterPrePass.ExecuteInjectionPointPass(waterContext);
                _motionVectorsPass.ExecuteInjectionPointPass(waterContext);
                _causticPrePass.ExecuteInjectionPointPass(waterContext);
                _volumetricLightingPass.ExecuteInjectionPointPass(waterContext);
                _causticDecalPass.ExecuteInjectionPointPass(waterContext);
                _copyColorPass.ExecuteInjectionPointPass(waterContext);
                _ssrPass.ExecuteInjectionPointPass(waterContext);
                _planarReflectionPass.ExecuteInjectionPointPass(waterContext);
                _reflectionFinalPass.ExecuteInjectionPointPass(waterContext);
                _copyWaterColorPass.ExecuteInjectionPointPass(waterContext);
                _underwaterPass.ExecuteInjectionPointPass(waterContext);
                _drawToDepthPass.ExecuteInjectionPointPass(waterContext);

            }
            catch (Exception e)
            {
                OnAfterCameraRendering(cam);
                Release();
                Debug.LogError("Water rendering error: " + e.Message + "    \r\n " + e.StackTrace);
            }
        }


        internal static void OverrideCameraRequiredSettings(Camera cam)
        {
            if (cam.actualRenderingPath == RenderingPath.Forward && cam.depthTextureMode == DepthTextureMode.None) cam.depthTextureMode = DepthTextureMode.Depth;
        }


        public void OnAfterCameraRendering(Camera cam)
        {
            foreach (var waterPass in _waterPasses) waterPass?.ReleaseCameraBuffer(cam);
        }

        public void Release()
        {
            if (_waterPasses != null)
            {
                foreach (var waterPass in _waterPasses)
                {
                    waterPass?.Release();
                }
            }

        }

    }
}
#endif