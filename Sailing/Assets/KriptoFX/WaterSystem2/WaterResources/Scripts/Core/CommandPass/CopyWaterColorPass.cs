using UnityEngine;
using UnityEngine.Rendering;

namespace KWS
{
    internal class CopyWaterColorPass : WaterPass
    {
        internal override string   PassName => "Water.CopyWaterColorPass";
        private           Material _copyColorMaterial;

        public CopyWaterColorPass()
        {
            _copyColorMaterial                             =  KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.CopyColorShaderName);
        }

        readonly Vector2 _rtScale = new Vector2(1.0f, 1.0f);

        private void InitializeTextures()
        {
            var dimension = KWS_CoreUtils.SinglePassStereoEnabled ? TextureDimension.Tex2DArray : TextureDimension.Tex2D;
            var slices    = KWS_CoreUtils.SinglePassStereoEnabled ? 2 : 1;

            WaterSharedResources.CameraOpaqueTextureAfterWaterPass = KWS_CoreUtils.RTHandles.Alloc(_rtScale, name: "KWS_CameraOpaqueTextureAfterWaterPass", colorFormat: KWS_CoreUtils.GetGraphicsFormatHDR(), dimension: dimension, slices: slices);
        }


        public override void ExecuteCommandBuffer(WaterPassContext waterContext)
        { if (!WaterSystem.QualitySettings.UseWaterDropsEffect) return;
            var state =UnderwaterPass.WaterDropsState.GetCameraCache(waterContext.cam);
            if (state.IsRendered == false) return;
         
            if (WaterSharedResources.CameraOpaqueTextureAfterWaterPass == null) InitializeTextures();

            var target     = WaterSharedResources.CameraOpaqueTextureAfterWaterPass;

            waterContext.cmd.BlitTriangleRTHandle(waterContext.cameraColor, Vector4.one, target, _copyColorMaterial, ClearFlag.None, Color.clear, 0);
            waterContext.cmd.SetGlobalTexture(KWS_ShaderConstants_PlatformSpecific.CopyColorID.KWS_CameraOpaqueTextureAfterWaterPass, target);
            waterContext.cmd.SetGlobalVector(KWS_ShaderConstants_PlatformSpecific.CopyColorID.KWS_CameraOpaqueTextureAfterWaterPass_RTHandleScale, target.rtHandleProperties.rtHandleScale);
        }

        public override void Release()
        {
            WaterSharedResources.CameraOpaqueTextureAfterWaterPass?.Release();
            WaterSharedResources.CameraOpaqueTextureAfterWaterPass = null;

            KW_Extensions.SafeDestroy(_copyColorMaterial); 

        }
       
    }
}