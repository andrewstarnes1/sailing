#if KWS_HDRP

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace KWS
{

    internal class KWS_WaterPassHandler
    {
        private List<WaterPass> _waterPasses = new List<WaterPass>();

        FftWavesPass              _fftWavesPass              = new();
        BuoyancyPass              _buoyancyPass              = new();
        DynamicWavesPass          _dynamicWavesPass          = new();

        WaterPrePass           _waterPrePass           = new();
        MotionVectorsPass      _motionVectorsPass      = new();
        CausticPrePass         _causticPrePass         = new();
        VolumetricLightingPass _volumetricLightingPass = new();
        CausticDecalPass       _causticDecalPass       = new();

        ScreenSpaceReflectionPass  _ssrPass             = new();
        ProbePlanarReflection _planarReflectionPass = new();
        ReflectionFinalPass        _reflectionFinalPass = new();
        DrawMeshPass               _drawMeshPass        = new();

        UnderwaterPass             _underwaterPass      = new();
        DrawToPosteffectsDepthPass _drawToDepthPass     = new();

        Dictionary<CustomPassInjectionPoint, CustomPassVolume> _volumes = new Dictionary<CustomPassInjectionPoint, CustomPassVolume>();

        internal KWS_WaterPassHandler()
        {
            CreateVolume(CustomPassInjectionPoint.BeforePreRefraction);
            CreateVolume(CustomPassInjectionPoint.BeforeTransparent);
            CreateVolume(CustomPassInjectionPoint.BeforePostProcess);
        }

        public void Release()
        {
            foreach (var customPassVolume in _volumes)
            {
                KW_Extensions.SafeDestroy(customPassVolume.Value.GetComponent<GameObject>());
            }

            foreach (var waterPass in _waterPasses) waterPass?.Release();
            _waterPasses.Clear();
        }

        internal void OnBeforeFrameRendering(HashSet<Camera> cameras, CustomFixedUpdates fixedUpdates)
        {
            foreach (var waterPass in _waterPasses) waterPass.ExecutePerFrame(cameras, fixedUpdates);
        }

        internal void OnBeforeCameraRendering(Camera cam, ScriptableRenderContext ctx)
        {


            //"volume.customCamera" and "volumePass.enabled" just ignored for other cameras in the current frame... Typical unity HDRiP rendering.
            try
            {
                var data = cam.GetComponent<HDAdditionalCameraData>();
                //if (data == null) return;

                var cameraSize = KWS_CoreUtils.GetScreenSizeLimited(KWS_CoreUtils.SinglePassStereoEnabled);
                KWS_CoreUtils.RTHandles.SetReferenceSize(cameraSize.x, cameraSize.y);

                WaterPass.WaterPassContext waterContext = default;

                waterContext.cam = cam;
                waterContext.RenderContext        = ctx;
                waterContext.AdditionalCameraData = data;

                if (_waterPasses.Count == 0)
                {
                   
                    
                    InitializePass(ref _fftWavesPass,     CustomPassInjectionPoint.BeforePreRefraction);
                    InitializePass(ref _buoyancyPass,     CustomPassInjectionPoint.BeforePreRefraction);
                    InitializePass(ref _dynamicWavesPass, CustomPassInjectionPoint.BeforePreRefraction);

                    InitializePass(ref _waterPrePass,           CustomPassInjectionPoint.BeforePreRefraction);
                    InitializePass(ref _motionVectorsPass,      CustomPassInjectionPoint.BeforePreRefraction);
                    
                    InitializePass(ref _causticPrePass,         CustomPassInjectionPoint.BeforePreRefraction);
                    InitializePass(ref _volumetricLightingPass, CustomPassInjectionPoint.BeforePreRefraction);
                    InitializePass(ref _causticDecalPass,       CustomPassInjectionPoint.BeforePreRefraction);
                    
                    InitializePass(ref _planarReflectionPass, CustomPassInjectionPoint.BeforeTransparent);
                    InitializePass(ref _ssrPass,              CustomPassInjectionPoint.BeforeTransparent);
                    InitializePass(ref _reflectionFinalPass,  CustomPassInjectionPoint.BeforeTransparent);
                    InitializePass(ref _drawMeshPass,         CustomPassInjectionPoint.BeforeTransparent);

                    //InitializePass(ref _copyWaterColorPass, CustomPassInjectionPoint.BeforePostProcess);
                    InitializePass(ref _underwaterPass, CustomPassInjectionPoint.BeforePostProcess);

                    InitializePass(ref _drawToDepthPass, CustomPassInjectionPoint.BeforePostProcess);
                }

                foreach (var waterPass in _waterPasses)
                {
                    waterPass.SetWaterContext(waterContext);
                    waterPass.ExecuteBeforeCameraRendering(cam, ctx);

                }

            }
            catch (Exception e)
            {
                Debug.LogError("Water rendering error: " + e.InnerException);
            }
        }
        internal void OnAfterCameraRendering(Camera cam, ScriptableRenderContext ctx)
        {
            
        }

        void CreateVolume(CustomPassInjectionPoint injectionPoint)
        {
            var tempGO = new GameObject("WaterVolume_" + injectionPoint) { hideFlags = HideFlags.DontSave };
            tempGO.hideFlags = HideFlags.DontSave;
            tempGO.transform.parent = WaterSystem.UpdateManagerObject.transform;
            var volume = tempGO.AddComponent<CustomPassVolume>();
            volume.injectionPoint = injectionPoint;
            _volumes.Add(injectionPoint, volume);
        }


        void InitializePass<T>(ref T pass, CustomPassInjectionPoint injectionPoint) where T : WaterPass
        {
            var volumePass = _volumes[injectionPoint];
            pass = (T)volumePass.AddPassOfType<T>();
            _waterPasses.Add(pass);
        }

    }
}
#endif