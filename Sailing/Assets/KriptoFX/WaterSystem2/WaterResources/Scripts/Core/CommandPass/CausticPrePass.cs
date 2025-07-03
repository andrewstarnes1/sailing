using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static KWS.KWS_CoreUtils;
using static KWS.KWS_ShaderConstants;

namespace KWS
{
    internal class CausticPrePass: WaterPass
    {
        internal override string PassName => "Water.CausticPrePass";

        private Dictionary<int, Mesh> _causticMeshes = new Dictionary<int, Mesh>();

        Material _causticMaterial;

        const float KWS_CAUSTIC_MULTIPLIER = 0.15f;

        Dictionary<WaterQualityLevelSettings.CausticTextureResolutionQualityEnum, int> _causticQualityToMeshQuality = new Dictionary<WaterQualityLevelSettings.CausticTextureResolutionQualityEnum, int>()
        {
            {WaterQualityLevelSettings.CausticTextureResolutionQualityEnum.Extreme, 512},
            {WaterQualityLevelSettings.CausticTextureResolutionQualityEnum.Ultra, 384},
            {WaterQualityLevelSettings.CausticTextureResolutionQualityEnum.High, 256},
            {WaterQualityLevelSettings.CausticTextureResolutionQualityEnum.Medium, 192},
            {WaterQualityLevelSettings.CausticTextureResolutionQualityEnum.Low, 128},
        };

        private float[] _causticDepthScaleRelativeToCascade = new[] { 0.1f, 0.05f, 0.01f };
     
        public CausticPrePass()
        {
            WaterSystem.OnAnyWaterSettingsChanged += OnAnyWaterSettingsChanged;
            _causticMaterial                               =  KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.CausticComputeShaderName);
        }

        void InitializeTextures()
        {
            var size               = (int)WaterSystem.QualitySettings.CausticTextureResolutionQuality;
            var slices             = WaterSystem.Instance.FftWavesCascades;
            if (slices > 3) slices = 3;

            WaterSharedResources.CausticRTArray = KWS_CoreUtils.RTHandles.Alloc(size, size, colorFormat: GraphicsFormat.R8_UNorm, name: "_CausticRTArray", useMipMap: true, autoGenerateMips: false, slices: slices, dimension:TextureDimension.Tex2DArray);
            Shader.SetGlobalTexture(CausticID.KWS_CausticRTArray, WaterSharedResources.CausticRTArray);

            KWS_CoreUtils.ClearRenderTexture(WaterSharedResources.CausticRTArray.rt, ClearFlag.Color, new Color(KWS_CAUSTIC_MULTIPLIER, KWS_CAUSTIC_MULTIPLIER, KWS_CAUSTIC_MULTIPLIER));
            this.WaterLog(WaterSharedResources.CausticRTArray);
        }

        void ReleaseTextures()
        {
            WaterSharedResources.CausticRTArray?.Release();
            WaterSharedResources.CausticRTArray = null;
            this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.ReleaseRT);
        }

        public override void Release()
        {
            WaterSystem.OnAnyWaterSettingsChanged -= OnAnyWaterSettingsChanged;
           
            ReleaseTextures();
            KW_Extensions.SafeDestroy(_causticMaterial);

            foreach (var causticMesh in _causticMeshes) KW_Extensions.SafeDestroy(causticMesh.Value);
            _causticMeshes.Clear();

            this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.Release);
        }

        private void OnAnyWaterSettingsChanged(WaterSystem.WaterSettingsCategory changedTabs)
        {
            if(!WaterSystem.QualitySettings.UseCausticEffect) return;

            if (changedTabs.HasTab(WaterSystem.WaterSettingsCategory.Caustic))
            {
                if (WaterSharedResources.CausticRTArray                == null 
                 || WaterSharedResources.CausticRTArray.rt.width       != (int)WaterSystem.QualitySettings.CausticTextureResolutionQuality
                 || WaterSharedResources.CausticRTArray.rt.volumeDepth != WaterSystem.Instance.FftWavesCascades)
                {
                    ReleaseTextures();
                    InitializeTextures();
                }
            }
        }

        public override void ExecuteCommandBuffer(WaterPass.WaterPassContext waterContext)
        {
            if (!WaterSystem.QualitySettings.UseCausticEffect) return;
            if (!IsWaterVisibleAndActive()) return;

            if (WaterSharedResources.CausticRTArray == null) InitializeTextures();
            ComputeCaustic(waterContext);
            if (WaterSharedResources.CausticRTArray != null) waterContext.cmd.GenerateMips(WaterSharedResources.CausticRTArray);
        }

        void ComputeCaustic(WaterPass.WaterPassContext waterContext)
        {
            var cmd         = waterContext.cmd;
            var maxCascades = Mathf.Min(WaterSystem.Instance.FftWavesCascades, 3);
            var mesh = GetOrCreateCausticMesh(WaterSystem.QualitySettings.CausticTextureResolutionQuality);

            for (int idx = 0; idx < maxCascades; idx++)
            {
                CoreUtils.SetRenderTarget(waterContext.cmd, WaterSharedResources.CausticRTArray, ClearFlag.Color, Color.black, depthSlice: idx);
                cmd.SetGlobalInteger("KWS_CausticCascadeIndex", idx);
                cmd.DrawMesh(mesh, Matrix4x4.identity, _causticMaterial);
            }
        }


        Mesh GetOrCreateCausticMesh(WaterQualityLevelSettings.CausticTextureResolutionQualityEnum quality)
        {
            if (!_causticQualityToMeshQuality.TryGetValue(quality, out var size)) size = 256;
            //var size = _causticQualityToMeshQuality[quality];
            if (!_causticMeshes.ContainsKey(size))
            {
                _causticMeshes.Add(size, MeshUtils.CreatePlaneMesh(size, 1.2f));
            }

            return _causticMeshes[size];
        }

    }
}