#if KWS_URP

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class KWS_WaterPassHandler
    {

        List<WaterPass> _waterPasses;

        FftWavesPass              _fftWavesPass              = new();
        BuoyancyPass              _buoyancyPass              = new();
        DynamicWavesPass          _dynamicWavesPass          = new();

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

        private Dictionary<WaterQualityLevelSettings.RefractionResolutionEnum, Downsampling> _downsamplingQuality = new ()
        {
            { WaterQualityLevelSettings.RefractionResolutionEnum.Full, Downsampling.None },
            { WaterQualityLevelSettings.RefractionResolutionEnum.Half, Downsampling._2xBilinear },
            { WaterQualityLevelSettings.RefractionResolutionEnum.Quarter, Downsampling._4xBilinear },
        };
        private Downsampling _defaultDownsampling = Downsampling._2xBilinear;
        private FieldInfo    _downsampleProp;

        internal KWS_WaterPassHandler()
        {
            _waterPrePass.renderPassEvent            = RenderPassEvent.BeforeRenderingSkybox;
            _motionVectorsPass.renderPassEvent      = RenderPassEvent.BeforeRenderingSkybox;
            _causticPrePass.renderPassEvent         = RenderPassEvent.BeforeRenderingSkybox;
            _volumetricLightingPass.renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
            _causticDecalPass.renderPassEvent       = RenderPassEvent.BeforeRenderingSkybox;

            _ssrPass.renderPassEvent             = RenderPassEvent.BeforeRenderingTransparents;
            _reflectionFinalPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            
            _copyWaterColorPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            _drawToDepthPass.renderPassEvent    = RenderPassEvent.AfterRenderingTransparents;
            _underwaterPass.renderPassEvent     = RenderPassEvent.AfterRenderingTransparents;

            _waterPasses = new List<WaterPass>
            {
                _fftWavesPass, _buoyancyPass, _dynamicWavesPass,
                _waterPrePass, _motionVectorsPass, _causticPrePass, _volumetricLightingPass, _causticDecalPass, _copyColorPass, _copyWaterColorPass,
                _ssrPass, _planarReflectionPass, _reflectionFinalPass, _drawMeshPass, _underwaterPass, _drawToDepthPass
            };




            //#if UNITY_EDITOR
            var urpAsset = UniversalRenderPipeline.asset;
            if (urpAsset != null)
            {
                urpAsset.supportsCameraOpaqueTexture = true;
                urpAsset.supportsCameraDepthTexture  = true;
                _defaultDownsampling                 = urpAsset.opaqueDownsampling;
            }
//#endif
            
        }

        void SetAssetSettings(UniversalAdditionalCameraData data, UniversalRenderPipelineAsset urpAsset)
        {
            data.requiresColorOption = CameraOverrideOption.On;
            data.requiresDepthOption = CameraOverrideOption.On;
            if (urpAsset.opaqueDownsampling != _defaultDownsampling)
            {
                _defaultDownsampling = urpAsset.opaqueDownsampling;
                Debug.Log("downsample changed " + _defaultDownsampling);
            }

            _downsampleProp = urpAsset.GetType().GetField("m_OpaqueDownsampling", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var downsample = KWS_CoreUtils.CanRenderUnderwater() ? Downsampling.None : _downsamplingQuality[WaterSystem.QualitySettings.RefractionResolution];
            if (_downsampleProp != null) _downsampleProp.SetValue(urpAsset, downsample);

        }

        void RestoreAssetSettings()
        {
            var urpAsset = UniversalRenderPipeline.asset;
            if(urpAsset == null) return;
            var prop     = urpAsset.GetType().GetField("m_OpaqueDownsampling", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            prop.SetValue(urpAsset, _defaultDownsampling);
        }


        internal void OnBeforeFrameRendering(HashSet<Camera> cameras, CustomFixedUpdates fixedUpdates)
        {
            foreach (var waterPass in _waterPasses) waterPass.ExecutePerFrame(cameras, fixedUpdates);

        }


        internal void OnBeforeCameraRendering(Camera cam, ScriptableRenderContext ctx)
        {
            try
            {
                var urpAsset = UniversalRenderPipeline.asset;
                if (urpAsset == null) return;

                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) return;
                SetAssetSettings(data, urpAsset);

                var cameraSize = KWS_CoreUtils.GetScreenSizeLimited(KWS_CoreUtils.SinglePassStereoEnabled);
                KWS_CoreUtils.RTHandles.SetReferenceSize(cameraSize.x, cameraSize.y);

                WaterPass.WaterPassContext waterContext = default;
                waterContext.cam = cam;

                waterContext.RenderContext        = ctx;
                waterContext.AdditionalCameraData = data;

                foreach (var waterPass in _waterPasses)
                {
                    waterPass.SetWaterContext(waterContext);
                    waterPass.ExecuteBeforeCameraRendering(cam, ctx);
                }

                var srpRenderer = data.scriptableRenderer;
                _waterPrePass.ConfigureInput(ScriptableRenderPassInput.Depth); //we need depth texture before "copy color" pass and caustic rendering

                _causticDecalPass.ColorPassWriteAccess = true;
                _drawToDepthPass.DepthPassWriteAccess  = true;
                _underwaterPass.ColorPassWriteAccess   = true;

                srpRenderer.EnqueuePass(_waterPrePass);
                //srpRenderer.EnqueuePass(_motionVectorsPass);

                if (KWS_TileZoneManager.VisibleDynamicWavesZones.Count > 0) srpRenderer.EnqueuePass(_dynamicWavesPass);

               
                if (WaterSystem.QualitySettings.UseCausticEffect)srpRenderer.EnqueuePass(_causticPrePass);
                if (WaterSystem.QualitySettings.UseVolumetricLight) srpRenderer.EnqueuePass(_volumetricLightingPass);
                if (WaterSystem.QualitySettings.UseCausticEffect)  srpRenderer.EnqueuePass(_causticDecalPass);
               
                if (WaterSystem.QualitySettings.UseScreenSpaceReflection) srpRenderer.EnqueuePass(_ssrPass);
                if (WaterSystem.QualitySettings.UseScreenSpaceReflection || WaterSystem.QualitySettings.UsePlanarReflection) srpRenderer.EnqueuePass(_reflectionFinalPass);

                if (WaterSystem.QualitySettings.UseUnderwaterEffect && WaterSystem.QualitySettings.UseWaterDropsEffect) srpRenderer.EnqueuePass(_copyWaterColorPass);
                if (WaterSystem.QualitySettings.UseUnderwaterEffect) srpRenderer.EnqueuePass(_underwaterPass);
                if (WaterSystem.QualitySettings.DrawToPosteffectsDepth) srpRenderer.EnqueuePass(_drawToDepthPass);

            }
            catch (Exception e)
            {
                Debug.LogError("Water rendering error: " + e.InnerException);
            }
        }

        internal void OnAfterCameraRendering(Camera cam, ScriptableRenderContext ctx)
        {
            if (UniversalRenderPipeline.asset == null) return;

            if (_downsampleProp               != null) _downsampleProp.SetValue(UniversalRenderPipeline.asset, _defaultDownsampling);
        }


        public void Release()
        {
            if (_waterPasses != null)
            {
                foreach (var waterPass in _waterPasses) waterPass?.Release();
            }

            RestoreAssetSettings();
        }
    }
}
#endif