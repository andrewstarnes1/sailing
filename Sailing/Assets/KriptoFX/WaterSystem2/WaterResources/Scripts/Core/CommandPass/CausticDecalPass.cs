using System;
using System.Collections.Generic;
using UnityEngine;
using static KWS.KWS_CoreUtils;
using static KWS.KWS_ShaderConstants;
using UnityEngine.Rendering;

namespace KWS
{
    internal class CausticDecalPass : WaterPass
    {
        internal override string PassName => "Water.CausticDecalPass";

        private           Mesh   _decalMesh;
        Material                 _decalMaterial;

        Dictionary<WaterQualityLevelSettings.CausticTextureResolutionQualityEnum, float> _causticQualityToDispersionStrength = new Dictionary<WaterQualityLevelSettings.CausticTextureResolutionQualityEnum, float>()
        {
            {WaterQualityLevelSettings.CausticTextureResolutionQualityEnum.Ultra, 1.5f},
            {WaterQualityLevelSettings.CausticTextureResolutionQualityEnum.High, 1.25f},
            {WaterQualityLevelSettings.CausticTextureResolutionQualityEnum.Medium, 1.0f},
            {WaterQualityLevelSettings.CausticTextureResolutionQualityEnum.Low, 0.75f},
        };


        public CausticDecalPass()
        {
            _decalMaterial = KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.CausticDecalShaderName, useWaterStencilMask: true);
            _decalMesh = MeshUtils.CreateCubeMesh();
        }


        void ReleaseTextures()
        {
            WaterSharedResources.CausticRTArray?.Release();
            WaterSharedResources.CausticRTArray = null;
            this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.ReleaseRT);
        }

        public override void Release()
        {
            KW_Extensions.SafeDestroy(_decalMesh, _decalMaterial);

            this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.Release);
        }


        public override void ExecuteCommandBuffer(WaterPass.WaterPassContext waterContext)
        {
            if (WaterSystem.QualitySettings.UseCausticEffect == false) return;
            if (!IsWaterVisibleAndActive()) return;

            DrawCausticDecal(waterContext);
            ResetKeywords(waterContext.cmd);
        }

        void DrawCausticDecal(WaterPass.WaterPassContext waterContext)
        {
            var cmd      = waterContext.cmd;
            var settings = WaterSystem.QualitySettings;
           
            if(!_causticQualityToDispersionStrength.TryGetValue(settings.CausticTextureResolutionQuality, out var dispersionStrength)) dispersionStrength = 1;

            cmd.SetGlobalFloat(CausticID.KWS_CaustisDispersionStrength, dispersionStrength);

            SetKeywords(cmd);

            var waterInstance = WaterSystem.Instance;
            var decalSize     = waterInstance.WorldSpaceBounds.size;
            var decalPos      = waterContext.cam.transform.position;

            var farDistance = waterContext.cam.farClipPlane * 0.5f;
            decalSize.x = Mathf.Min(decalSize.x, farDistance);
            decalSize.y = Mathf.Min(decalSize.y, farDistance);
            decalSize.z = Mathf.Min(decalSize.z, farDistance);
            
            decalPos.y -= decalSize.y * 0.5f;
            decalPos.y += 2;
          
            var decalTRS = Matrix4x4.TRS(decalPos, Quaternion.identity, decalSize); //todo precompute trs matrix

            UnityEngine.Rendering.CoreUtils.SetRenderTarget(waterContext.cmd, waterContext.cameraColor);
            cmd.DrawMesh(_decalMesh, decalTRS, _decalMaterial);
            
        }

        void SetKeywords(CommandBuffer cmd)
        {
            var isTwoCascades   = WaterSystem.Instance.FftWavesCascades == 2;
            var isThreeCascades = WaterSystem.Instance.FftWavesCascades == 3 || WaterSystem.Instance.FftWavesCascades == 4;

            cmd.SetKeyword("KWS_CAUSTIC_CASCADES_2",                                  isTwoCascades);
            cmd.SetKeyword("KWS_CAUSTIC_CASCADES_3",                                  isThreeCascades);
        }

        //by some reason unity can't reset these keywords correctly after scene reloading and material keywords always broken
        void ResetKeywords(CommandBuffer cmd)
        {
            cmd.DisableShaderKeyword(CausticKeywords.USE_DISPERSION);
        }
    }
}