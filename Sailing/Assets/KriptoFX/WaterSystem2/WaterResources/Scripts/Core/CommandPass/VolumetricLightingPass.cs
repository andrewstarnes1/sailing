using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static KWS.KWS_CoreUtils;
using static KWS.KWS_ShaderConstants;


namespace KWS
{
    internal class VolumetricLightingPass : WaterPass
    {
        internal override string PassName => "Water.VolumetricLightingPass";

        private static Vector2Int _surfaceLightResolution = new Vector2Int(512, 256);
        Material _volumeMaterial;

        internal Dictionary<Camera, VolumetricData> _volumetricDatas = new Dictionary<Camera, VolumetricData>();

        public VolumetricLightingPass()
        {
            _volumeMaterial = CreateMaterial(ShaderNames.VolumetricLightingShaderName);
            WaterSystem.OnAnyWaterSettingsChanged += OnAnyWaterSettingsChanged;
        }


        internal class VolumetricData : KW_Extensions.ICacheCamera
        {
            public RTHandle[] VolumetricLightRT = new RTHandle[2];
            public RTHandle VolumetricLightAdditionalDataRT;
            public RTHandle VolumetricLightSurfaceRT;

            public RTHandle VolumetricLightRTBlured;
            public RTHandle VolumetricLightAdditionalBlured;
            public RTHandle VolumetricLightSurfaceRTBlured;

            public int      Frame;

            public Matrix4x4 PrevVPMatrix;
            public Matrix4x4[] PrevVPMatrixStereo = new Matrix4x4[2];

            public KW_PyramidBlur PyramidBlur_VolumetricLight = new KW_PyramidBlur();
            public KW_PyramidBlur PyramidBlur_AdditionalLight = new KW_PyramidBlur();
            public KW_PyramidBlur PyramidBlur_SurfaceLight = new KW_PyramidBlur();

            internal void InitializeVolumeLightTextures(Camera cam)
            {
                var resolutionDownsample                                 = (int)WaterSystem.QualitySettings.VolumetricLightResolutionQuality / 100f;
                var maxSize                                              = KWS_CoreUtils.GetScreenSizeLimited(false);
                var height                                               = (int)(maxSize.y * resolutionDownsample);
                var width                                                = (int)(height    * 2); // typical resolution ratio is 16x9 (or 2x1), for better pixel filling we use [2 * width] x [height], instead of square [width] * [height].
                var hdrFormat                                            = GetGraphicsFormatHDR();
                for (int idx = 0; idx < 2; idx++) VolumetricLightRT[idx] = KWS_CoreUtils.RTHandleAllocVR(width, height, name: "_volumeLightRT_" + idx + "_" + cam, colorFormat: hdrFormat);

                if (WaterSystem.QualitySettings.VolumetricLightUseBlur)
                {
                    VolumetricLightRTBlured = KWS_CoreUtils.RTHandleAllocVR(width, height, name: "_volumeLightRTBlured", colorFormat: VolumetricLightRT[0].rt.graphicsFormat);
                }
                this.WaterLog(VolumetricLightRT[0]);
                Frame = 0;
            }

            internal void InitializeSurfaceLightTextures()
            {
                var hdrFormat                                            = GetGraphicsFormatHDR();

                VolumetricLightAdditionalDataRT = KWS_CoreUtils.RTHandleAllocVR(_surfaceLightResolution.x, _surfaceLightResolution.y, name: "_volumeLightAdditionalDataRT", colorFormat: GraphicsFormat.R8G8B8A8_UNorm);
                VolumetricLightSurfaceRT        = KWS_CoreUtils.RTHandleAllocVR(_surfaceLightResolution.x, _surfaceLightResolution.y, name: "_volumetricLightSurfaceRT",    colorFormat: hdrFormat);

                VolumetricLightAdditionalBlured = KWS_CoreUtils.RTHandleAllocVR(_surfaceLightResolution.x,   _surfaceLightResolution.y,                    name: "_volumeLightAdditionalDataRTBlured", colorFormat: GraphicsFormat.R8G8B8A8_UNorm);
                VolumetricLightSurfaceRTBlured  = KWS_CoreUtils.RTHandleAllocVR(_surfaceLightResolution.x, _surfaceLightResolution.y, name: "_volumetricLightSurfaceRTBlured",    colorFormat: hdrFormat);

                this.WaterLog(VolumetricLightAdditionalDataRT);
                Frame = 0;
            }

            
            internal void Update()
            {
                Frame++;
                if (Frame > int.MaxValue - 10) Frame = 0;

                if (KWS_CoreUtils.SinglePassStereoEnabled) PrevVPMatrixStereo = WaterSystem.KWS_MATRIX_VP;
                else PrevVPMatrix = WaterSystem.KWS_MATRIX_VP[0];
            }

            internal void ReleaseTextures()
            {
                VolumetricLightRT[0]?.Release();
                VolumetricLightRT[1]?.Release();
                VolumetricLightAdditionalDataRT?.Release();
                VolumetricLightSurfaceRT?.Release();


                VolumetricLightRTBlured?.Release();
                VolumetricLightAdditionalBlured?.Release();
                VolumetricLightSurfaceRTBlured?.Release();

                VolumetricLightRT[0] = VolumetricLightRT[1] = VolumetricLightAdditionalDataRT = VolumetricLightSurfaceRT = VolumetricLightRTBlured = VolumetricLightSurfaceRTBlured = null;

                PyramidBlur_VolumetricLight?.Release();
                PyramidBlur_AdditionalLight?.Release();
                PyramidBlur_SurfaceLight?.Release();

                Frame = 0;
            }

            public void Release()
            {
                ReleaseTextures();
                this.WaterLog("", KW_Extensions.WaterLogMessageType.Release);
            }
        }

        private void OnAnyWaterSettingsChanged(WaterSystem.WaterSettingsCategory changedTabs)
        {
            if (changedTabs.HasTab(WaterSystem.WaterSettingsCategory.VolumetricLighting))
            {
                _volumetricDatas.ReleaseCameraCache();
            }
        }




        public override void Release()
        {
            WaterSystem.OnAnyWaterSettingsChanged -= OnAnyWaterSettingsChanged;
            _volumetricDatas.ReleaseCameraCache();
            KW_Extensions.SafeDestroy(_volumeMaterial);


            this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.Release);
        }
        public override void ExecuteCommandBuffer(WaterPass.WaterPassContext waterContext)
        {
            
            if (WaterSystem.QualitySettings.UseVolumetricLight) ComputeVolumetricLighting(waterContext);

            if (WaterSystem.QualitySettings.UseVolumetricLight || KWS_TileZoneManager.VisibleDynamicWavesZones.Count > 0) ComputeSurfaceLighting(waterContext);
        }

        private void ComputeVolumetricLighting(WaterPassContext waterContext)
        {
            var cmd = waterContext.cmd;
            var cam = waterContext.cam;

            var data = _volumetricDatas.GetCameraCache(cam);
            
            if (data.VolumetricLightRT[0] == null) data.InitializeVolumeLightTextures(cam);


            var targetRT     = data.Frame % 2 == 0 ? data.VolumetricLightRT[0] : data.VolumetricLightRT[1];
            var lastTargetRT = data.Frame % 2 == 0 ? data.VolumetricLightRT[1] : data.VolumetricLightRT[0];
            
            UpdateShaderParams(cmd, data, lastTargetRT);

            if (data.Frame == 0)
            {
                CoreUtils.SetRenderTarget(waterContext.cmd, lastTargetRT, ClearFlag.Color, Color.black);
            }
            CoreUtils.SetRenderTarget(waterContext.cmd, targetRT, ClearFlag.Color, Color.black);
            cmd.BlitTriangle(_volumeMaterial, pass: 0);
            data.Update();
            
            if (WaterSystem.QualitySettings.VolumetricLightUseBlur)
            {
                data.PyramidBlur_VolumetricLight.ComputeSeparableBlur(WaterSystem.QualitySettings.VolumetricLightBlurRadius, targetRT, data.VolumetricLightRTBlured, waterContext.cmd, targetRT.rt.width, targetRT.rt.height);
                targetRT = data.VolumetricLightRTBlured;
            }
            

            cmd.SetGlobalTexture(VolumetricLightConstantsID.KWS_VolumetricLightRT, targetRT);
            cmd.SetGlobalVector(VolumetricLightConstantsID.KWS_VolumetricLight_RTHandleScale, targetRT.rtHandleProperties.rtHandleScale);
            
            WaterSharedResources.VolumetricLightingRT = targetRT;
        }
        
        private void ComputeSurfaceLighting(WaterPassContext waterContext)
        {
            var cmd = waterContext.cmd;
            var cam = waterContext.cam;

            var data = _volumetricDatas.GetCameraCache(cam);
            if (data.VolumetricLightSurfaceRT == null) data.InitializeSurfaceLightTextures();

            var additionalDataRT = data.VolumetricLightAdditionalDataRT;
            var surfaceRT = data.VolumetricLightSurfaceRT;

            CoreUtils.SetRenderTarget(waterContext.cmd, GetMrt(additionalDataRT, surfaceRT), additionalDataRT, ClearFlag.Color, Color.black);
            cmd.BlitTriangle(_volumeMaterial, pass: 1);

            data.PyramidBlur_AdditionalLight.ComputeSeparableBlur(2, additionalDataRT, data.VolumetricLightAdditionalBlured, waterContext.cmd, additionalDataRT.rt.width, additionalDataRT.rt.height);
            data.PyramidBlur_SurfaceLight.ComputeSeparableBlur(2, surfaceRT, data.VolumetricLightSurfaceRTBlured, waterContext.cmd, additionalDataRT.rt.width, additionalDataRT.rt.height);

            cmd.SetGlobalTexture(VolumetricLightConstantsID.KWS_VolumetricLightAdditionalDataRT, data.VolumetricLightAdditionalBlured);
            cmd.SetGlobalTexture(VolumetricLightConstantsID.KWS_VolumetricLightSurfaceRT, data.VolumetricLightSurfaceRTBlured);
            
            WaterSharedResources.VolumetricLightingAdditionalDataRT = data.VolumetricLightAdditionalBlured;
        }


        Vector4 ComputeMieVector(float mieG)
        {
            return new Vector4(1 - (mieG * mieG), 1 + (mieG * mieG), 2 * mieG, 1.0f / (4.0f * Mathf.PI));
        }

        private void UpdateShaderParams(CommandBuffer cmd, VolumetricData data, RTHandle lastTargetRT)
        {
            var anisoMie = ComputeMieVector(0.8f);
            cmd.SetGlobalVector(VolumetricLightConstantsID.KWS_LightAnisotropy, anisoMie);

            if (KWS_CoreUtils.SinglePassStereoEnabled) cmd.SetGlobalMatrixArray(KWS_ShaderConstants.CameraMatrix.KWS_PREV_MATRIX_VP_STEREO, data.PrevVPMatrixStereo);
            else cmd.SetGlobalMatrix(KWS_ShaderConstants.CameraMatrix.KWS_PREV_MATRIX_VP, data.PrevVPMatrix);

            cmd.SetGlobalTexture(KWS_ShaderConstants.VolumetricLightConstantsID.KWS_VolumetricLightRT_Last, lastTargetRT);
            cmd.SetGlobalVector(VolumetricLightConstantsID.KWS_VolumetricLightRT_Last_RTHandleScale, lastTargetRT.rtHandleProperties.rtHandleScale);
            cmd.SetGlobalInt(KWS_ShaderConstants.VolumetricLightConstantsID.KWS_Frame, data.Frame);

        }



    }
}