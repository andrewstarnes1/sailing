using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace KWS
{
    internal class ReflectionFinalPass : WaterPass
    {
        internal override string PassName => "Water.ReflectionFinalPass";

        RTHandle _planarFinalRT;
        RTHandle                                            _ssrFinalRT;
        private Material                                    _anisoMaterial;


        public ReflectionFinalPass()
        {
            WaterSystem.OnAnyWaterSettingsChanged += OnWaterSettingsChanged;
            _anisoMaterial                                 =  KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.ReflectionFiltering);
        }
        public override void Release()
        {
            WaterSystem.OnAnyWaterSettingsChanged -= OnWaterSettingsChanged;

            ReleaseTextures();

            KW_Extensions.SafeDestroy(_anisoMaterial);
        }

        void ReleaseTextures()
        {
            _planarFinalRT?.Release();
            _ssrFinalRT?.Release();

            _planarFinalRT = _ssrFinalRT = null;
        }

        private void OnWaterSettingsChanged(WaterSystem.WaterSettingsCategory changedTab)
        {
            if (changedTab.HasTab(WaterSystem.WaterSettingsCategory.Reflection))
            {
                if (!WaterSystem.QualitySettings.UseAnisotropicReflections) return;

                if (WaterSystem.QualitySettings.UseScreenSpaceReflection) ReinitializeSsrTexture();
                if (WaterSystem.QualitySettings.UsePlanarReflection) ReinitializePlanarTexture();
            }
        }


        void ReinitializeSsrTexture()
        {
            _ssrFinalRT?.Release();

            var source = WaterSharedResources.SsrReflection;
            if (source == null || source.rt == null) return;
            _ssrFinalRT = KWS_CoreUtils.RTHandleAllocVR(ScreenSpaceReflectionPass.ScaleFunc, name: "_ssrFinalRT", colorFormat: source.rt.graphicsFormat, useMipMap: true, autoGenerateMips: false);
          
            KW_Extensions.WaterLog(this, _ssrFinalRT);
        }

        void ReinitializePlanarTexture()
        {
            _planarFinalRT?.Release();

            var source = WaterSharedResources.PlanarReflection;
            if (source == null || source.width <= 1) return;
            _planarFinalRT = KWS_CoreUtils.RTHandleAllocVR(source.width, source.height, colorFormat: source.graphicsFormat, name: "_planarFilteredRT", useMipMap: true, autoGenerateMips: false);

           KW_Extensions.WaterLog(this, _planarFinalRT);
        }


        public override void ExecuteCommandBuffer(WaterPass.WaterPassContext waterContext)
        {
            if (WaterSystem.QualitySettings.UseScreenSpaceReflection)
            {
                ExecuteSsrPass(waterContext);
            }

            if (WaterSystem.QualitySettings.UsePlanarReflection)
            {
                ExecutePlanarPass(waterContext);
            }
        }

      
        void ExecuteSsrPass(WaterPass.WaterPassContext waterContext)
        {
            var cmd           = waterContext.cmd;
            var sourceRT      = WaterSharedResources.SsrReflection;
            if (sourceRT == null || sourceRT.rt == null) return;


            if (WaterSystem.QualitySettings.UseAnisotropicReflections)
            {
                if (_ssrFinalRT == null || _ssrFinalRT.rt == null) ReinitializeSsrTexture();
                CoreUtils.SetRenderTarget(waterContext.cmd, _ssrFinalRT, ClearFlag.Color, Color.black);

               
                cmd.BlitTriangleRTHandle(sourceRT, _ssrFinalRT, _anisoMaterial, ClearFlag.None, Color.clear, WaterSystem.QualitySettings.AnisotropicReflectionsHighQuality ? 1 : 0);
              
                cmd.GenerateMips(_ssrFinalRT);
                WaterSharedResources.SsrReflection = _ssrFinalRT;

            }
            else
            {
                cmd.GenerateMips(sourceRT);
                WaterSharedResources.SsrReflection = sourceRT;
            }

            cmd.SetGlobalTexture(KWS_ShaderConstants.SSR_ID.KWS_ScreenSpaceReflectionRT, WaterSharedResources.SsrReflection);
            cmd.SetGlobalVector(KWS_ShaderConstants.SSR_ID.KWS_ScreenSpaceReflection_RTHandleScale, WaterSharedResources.SsrReflection.rtHandleProperties.rtHandleScale);
        }

        void ExecutePlanarPass(WaterPass.WaterPassContext waterContext)
        {
            var cmd      = waterContext.cmd;
            var sourceRT = WaterSharedResources.PlanarReflection;
            if (sourceRT == null || sourceRT.width <= 1) return;


            if (WaterSystem.QualitySettings.UseAnisotropicReflections)
            {
                if (_planarFinalRT == null || _planarFinalRT.rt == null) ReinitializePlanarTexture();
                CoreUtils.SetRenderTarget(waterContext.cmd, _planarFinalRT, ClearFlag.Color, Color.black);

                
                cmd.BlitTriangleRTHandle(sourceRT, Vector4.one, _planarFinalRT, _anisoMaterial, ClearFlag.None, Color.clear, WaterSystem.QualitySettings.AnisotropicReflectionsHighQuality ? 1 : 0);
                cmd.GenerateMips(_planarFinalRT);
                WaterSharedResources.PlanarReflection = _planarFinalRT.rt;
                Shader.SetGlobalTexture(KWS_ShaderConstants.ReflectionsID.KWS_PlanarReflectionRT, WaterSharedResources.PlanarReflection);
            }
           
        }
    }
}