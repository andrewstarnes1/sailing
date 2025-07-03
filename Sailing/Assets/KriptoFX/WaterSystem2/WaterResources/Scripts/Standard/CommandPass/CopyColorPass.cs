using UnityEngine;
using UnityEngine.Rendering;

namespace KWS
{
    internal class CopyColorPass : WaterPass
    {
        internal override string PassName => "Water.CopyColorPass";
        private Material _copyColorMaterial;
        RTHandleSystem _RTHandleSystem;


        public CopyColorPass()
        {
            _copyColorMaterial                             =  KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.CopyColorShaderName);
        }


        float GetTextureScale()
        {
            return KWS_CoreUtils.CanRenderUnderwater() ? 1 : (int)WaterSystem.QualitySettings.RefractionResolution / 100f;
        }


        private void InitializeTextures()
        {
            if (_RTHandleSystem == null)
            {
                _RTHandleSystem = new RTHandleSystem();
                var screenSize = KWS_CoreUtils.GetScreenSize(KWS_CoreUtils.SinglePassStereoEnabled);
                _RTHandleSystem.Initialize(screenSize.x, screenSize.y);
            }

            var dimension = KWS_CoreUtils.SinglePassStereoEnabled ? TextureDimension.Tex2DArray : TextureDimension.Tex2D;
            var slices = KWS_CoreUtils.SinglePassStereoEnabled ? 2 : 1;

            WaterSharedResources.CameraOpaqueTexture = _RTHandleSystem.Alloc(Vector2.one, name: "KWS_CameraOpaqueTexture", colorFormat: KWS_CoreUtils.GetGraphicsFormatHDR(), slices: slices, dimension: dimension);
        }


        public override void ExecuteCommandBuffer(WaterPassContext waterContext)
        {
            if (WaterSharedResources.CameraOpaqueTexture == null) InitializeTextures();
            var target     = WaterSharedResources.CameraOpaqueTexture;

            var screenSize = KWS_CoreUtils.GetScreenSize(KWS_CoreUtils.SinglePassStereoEnabled);
            var scale      = GetTextureScale();
            _RTHandleSystem.SetReferenceSize((int)(screenSize.x * scale), (int)(screenSize.y * scale));

            waterContext.cmd.BlitTriangleRTHandle(BuiltinRenderTextureType.CurrentActive, Vector4.one, target, _copyColorMaterial, ClearFlag.None, Color.clear, 0);
            waterContext.cmd.SetGlobalTexture(KWS_ShaderConstants_PlatformSpecific.CopyColorID.KWS_CameraOpaqueTexture, target);
            waterContext.cmd.SetGlobalVector(KWS_ShaderConstants_PlatformSpecific.CopyColorID.KWS_CameraOpaqueTexture_RTHandleScale, target.rtHandleProperties.rtHandleScale);
        }

        public override void Release()
        {
            WaterSharedResources.CameraOpaqueTexture?.Release();
            WaterSharedResources.CameraOpaqueTexture = null;
            KW_Extensions.SafeDestroy(_copyColorMaterial);

            _RTHandleSystem?.Dispose();
        }


    }
}