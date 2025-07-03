using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace KWS
{
    internal class UnderwaterPass : WaterPass
    {
        internal override string PassName => "Water.UnderwaterPass";
        internal static Dictionary<Camera, UnderwaterState> WaterDropsState = new Dictionary<Camera, UnderwaterState>();

        KW_PyramidBlur _pyramidBlur;

        Material _underwaterMaterial;
        private Material _waterDropsMaterial;

        RTHandle _underwaterRT;
        RTHandle _underwaterRTBlured;


        readonly Vector2 _rtScale = new Vector2(0.35f, 0.35f);
        RenderParams _renderParams;
        private bool _lastUnderwaterState = false;

        internal class UnderwaterState : KW_Extensions.ICacheCamera
        {
            public bool IsRendered = false;
            public bool LastUnderwaterState = false;

            public float WaterDropsTimer = 10;
            public float WaterDropsLensTimer = 10;
            public float WaterDropsStretchTimer = 10;

            public void Release()
            {

            }
        }

        public UnderwaterPass()
        {

        }

        public override void Release()
        {
            _underwaterRT?.Release();
            _underwaterRTBlured?.Release();
            _pyramidBlur?.Release();

            KW_Extensions.SafeDestroy(_underwaterMaterial, _waterDropsMaterial);
            _underwaterMaterial = null;
            _waterDropsMaterial = null;


            KW_Extensions.WaterLog(this, "", KW_Extensions.WaterLogMessageType.Release);
        }


        public override void ExecuteCommandBuffer(WaterPass.WaterPassContext waterContext)
        {
            if (!WaterSystem.QualitySettings.UseWaterDropsEffect) return;
            //if (WaterSystem.IsCameraFullUnderwater) return;

            if (_waterDropsMaterial == null) _waterDropsMaterial = KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.WaterDropsShaderName, useWaterStencilMask: true);

            var state = WaterDropsState.GetCameraCache(waterContext.cam);

            waterContext.cmd.SetGlobalVector("KWS_WaterDropsTimer", new Vector3(Mathf.Clamp01(state.WaterDropsLensTimer), state.WaterDropsStretchTimer, Mathf.Clamp01(state.WaterDropsTimer)));
            if (state.WaterDropsLensTimer > 0 && state.WaterDropsLensTimer < 5)
            {
                UnityEngine.Rendering.CoreUtils.SetRenderTarget(waterContext.cmd, waterContext.cameraColor);
                waterContext.cmd.BlitTriangle(_waterDropsMaterial);
                state.IsRendered = true;
            }
            else state.IsRendered = false;

        }

        public override void ExecuteBeforeCameraRendering(Camera cam, ScriptableRenderContext context)
        {
            if (!WaterSystem.QualitySettings.UseUnderwaterEffect) return;

            if (WaterSystem.QualitySettings.UseWaterDropsEffect) UpdateWaterDropsTimer(cam, 1.5f);

            if (WaterSystem.IsCameraPartialUnderwater) ExecuteInstanceBeforeCameraRendering(cam);
        }

        private void UpdateWaterDropsTimer(Camera cam, float multiplier)
        {
            var state = WaterDropsState.GetCameraCache(cam);

            if (_lastUnderwaterState != WaterSystem.IsCameraFullUnderwater || WaterSystem.IsCameraRequireWaterDrops)
            {
                if (WaterSystem.IsCameraFullUnderwater == true)
                {
                    state.WaterDropsLensTimer = 0;
                    state.WaterDropsStretchTimer = 0;
                    state.WaterDropsTimer = 0; ;
                }
                else if (WaterSystem.IsCameraRequireWaterDrops)
                {
                    state.WaterDropsLensTimer = Mathf.Min(state.WaterDropsLensTimer + KW_Extensions.DeltaTime() * 0.5f * multiplier, 0.75f);
                    state.WaterDropsStretchTimer = 0;
                    state.WaterDropsTimer = 0;
                }
            }

            _lastUnderwaterState = WaterSystem.IsCameraFullUnderwater;

            if (WaterSystem.IsCameraFullUnderwater == false && state.WaterDropsLensTimer <= 10)
            {
                state.WaterDropsLensTimer += KW_Extensions.DeltaTime() * 0.25f * multiplier;
                state.WaterDropsStretchTimer += KW_Extensions.DeltaTime() * (Mathf.Clamp01(Mathf.Sin(state.WaterDropsLensTimer * 2))) * 0.3f * multiplier;
                state.WaterDropsTimer += KW_Extensions.DeltaTime() * 0.1f * multiplier;
            }
            //Debug.Log(_waterDropsLensTimer);
        }

        void ExecuteInstanceBeforeCameraRendering(Camera cam)
        {
            InitMaterial();
            UpdateShaderParams();

            _renderParams.camera = cam;
            _renderParams.material = _underwaterMaterial;
            _renderParams.renderingLayerMask = GraphicsSettings.defaultRenderingLayerMask;
            _renderParams.layer = KWS_Settings.Water.WaterLayer;

            if (_renderParams.camera == null || _renderParams.material == null)
            {
                Debug.LogError($"Water draw mesh rendering error: {_renderParams.camera}, { _renderParams.material}");
                return;
            }
            Graphics.RenderPrimitives(in _renderParams, MeshTopology.Triangles, 3, 1);
        }


        private void InitMaterial()
        {
            if (_underwaterMaterial == null)
            {
                _underwaterMaterial = KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.UnderwaterShaderName, useWaterStencilMask: true);
                _renderParams = new RenderParams(_underwaterMaterial);
                _renderParams.worldBounds = new Bounds(Vector3.zero, 10000000 * Vector3.one);
            }
        }

        private void UpdateShaderParams()
        {
            var usePhysicalApproximationColor = WaterSystem.QualitySettings.UnderwaterReflectionMode == WaterQualityLevelSettings.UnderwaterReflectionModeEnum.PhysicalAproximatedReflection && !WaterSystem.QualitySettings.UseScreenSpaceReflection;
            var usePhysicalApproximationSSR = WaterSystem.QualitySettings.UnderwaterReflectionMode == WaterQualityLevelSettings.UnderwaterReflectionModeEnum.PhysicalAproximatedReflection && WaterSystem.QualitySettings.UseScreenSpaceReflection;

            _underwaterMaterial.SetKeyword(KWS_ShaderConstants.UnderwaterKeywords.USE_PHYSICAL_APPROXIMATION_COLOR, usePhysicalApproximationColor);
            _underwaterMaterial.SetKeyword(KWS_ShaderConstants.UnderwaterKeywords.USE_PHYSICAL_APPROXIMATION_SSR, usePhysicalApproximationSSR);
            _underwaterMaterial.SetKeyword(KWS_ShaderConstants.UnderwaterKeywords.KWS_CAMERA_UNDERWATER, WaterSystem.IsCameraPartialUnderwater);

            _underwaterMaterial.renderQueue = KWS_Settings.Water.DefaultWaterQueue + WaterSystem.QualitySettings.WaterTransparentSortingPriority + KWS_Settings.Water.UnderwaterQueueOffset;
        }



    }
}