using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

using static KWS.KW_Extensions;
using static KWS.KWS_ShaderConstants;

namespace KWS
{
    public partial class WaterSystem
    {

        #region Initialization


        //If you press ctrl+z after deleting the water gameobject, unity returns all objects without links and save all objects until you close the editor. Not sure how to fix that =/ 
        void ClearUndoObjects(Transform parent)
        {
            if (parent.childCount > 0)
            {
                KW_Extensions.SafeDestroy(parent.GetChild(0).gameObject);
            }
        }

        //internal void UpdateState(WaterTab changedTab)
        //{
        //    OnWaterSettingsChanged?.Invoke(this, changedTab);
        //}


        private void OnAnyWaterSettingsChangedEvent(WaterSettingsCategory changedTab)
        {
            UpdateWaterInstance(changedTab);
            //WaterSharedResources.OnAnyWaterSettingsChanged?.Invoke(instance, changedTab);
        }

        private void UpdateWaterInstance(WaterSettingsCategory changedTab)
        {

            if (changedTab.HasTab(WaterSettingsCategory.Transform))
            {
                SetScaleRotationRelativeToMeshType();
            }

            if (changedTab.HasTab(WaterSettingsCategory.Mesh) || changedTab.HasTab(WaterSettingsCategory.Transform))
            {
                RebuildMesh();
            }

            LoadSharedResourceTextures();
        }


        private static void LoadSharedResourceTextures()
        {
            //if (QualitySettings.UseIntersectionFoam)
            //{
            //    if (WaterSharedResources.KWS_IntersectionFoamTex == null) WaterSharedResources.KWS_IntersectionFoamTex = Resources.Load<Texture2D>(KWS_Settings.ResourcesPaths.KWS_IntersectionFoamTex);
            //    Shader.SetGlobalTexture("KWS_IntersectionFoamTex", WaterSharedResources.KWS_IntersectionFoamTex);
            //}

            if (QualitySettings.UseOceanFoam)
            {
                if (WaterSharedResources.KWS_OceanFoamTex == null) WaterSharedResources.KWS_OceanFoamTex = Resources.Load<Texture2D>(KWS_Settings.ResourcesPaths.KWS_OceanFoamTex);
                Shader.SetGlobalTexture("KWS_OceanFoamTex", WaterSharedResources.KWS_OceanFoamTex);
            }

            //todo check zones relative load
            // if (WaterZoneManager.DynamicWavesSimulationZones.Count > 0 || QualitySettings.UseOceanFoam || QualitySettings.UseIntersectionFoam)
            {
                if (WaterSharedResources.KWS_FluidsFoamTex == null) WaterSharedResources.KWS_FluidsFoamTex = Resources.Load<Texture2D>(KWS_Settings.ResourcesPaths.KWS_FluidsFoamTex);
                Shader.SetGlobalTexture("KW_FluidsFoamTex", WaterSharedResources.KWS_FluidsFoamTex);

                if (WaterSharedResources.KWS_SplashTex == null)
                {
                    WaterSharedResources.KWS_SplashTex = Resources.Load<Texture2D>(KWS_Settings.ResourcesPaths.KWS_SplashTex0);
                }

                Shader.SetGlobalTexture("KWS_SplashTex0", WaterSharedResources.KWS_SplashTex);
            }

            if (QualitySettings.UseWaterDropsEffect)
            {
                if (WaterSharedResources.KWS_WaterDropsTexture == null) WaterSharedResources.KWS_WaterDropsTexture = Resources.Load<Texture2D>(KWS_Settings.ResourcesPaths.KWS_WaterDrops);
                Shader.SetGlobalTexture("KWS_WaterDropsTexture", WaterSharedResources.KWS_WaterDropsTexture);

                if (WaterSharedResources.KWS_WaterDropsMaskTexture == null) WaterSharedResources.KWS_WaterDropsMaskTexture = Resources.Load<Texture2D>(KWS_Settings.ResourcesPaths.KWS_WaterDropsMask);
                Shader.SetGlobalTexture("KWS_WaterDropsMaskTexture", WaterSharedResources.KWS_WaterDropsMaskTexture);
            }

            //  if (WaterZoneManager.DynamicWavesSimulationZones.Count > 0)
            {
                if (WaterSharedResources.KWS_WaterDynamicWavesFlowMapNormal == null) WaterSharedResources.KWS_WaterDynamicWavesFlowMapNormal = Resources.Load<Texture2D>(KWS_Settings.ResourcesPaths.KWS_WaterDynamicWavesFlowMapNormal);
                Shader.SetGlobalTexture("KWS_WaterDynamicWavesFlowMapNormal", WaterSharedResources.KWS_WaterDynamicWavesFlowMapNormal);
            }

#if KWS_DEBUG
            if (WaterSystem.TestTexture != null)
            {
                Shader.SetGlobalTexture("KWS_TestTexture", WaterSystem.TestTexture);
            }
#endif
        }


        float FftSmoothstep(float x, float scaleX, float scaleY)
        {
            if (x > scaleX) return scaleY;
            return (-2 * Mathf.Pow(x / scaleX, 3) + 3 * Mathf.Pow(x / scaleX, 2)) * scaleY;
        }

        float EvaluateMaxAmplitute()
        {
            int cascades = FftWavesCascades;
            float windSpeed = WindSpeed;
            float windTurbulence = WindTurbulence;
            float scale = WavesAreaScale * 0.5f;

            //main formula -2x^3+3x^2 -> (-2(x/scaleX)^3+3(x/scaleX)^2)*scaleY

            //  4 cascades, 1-50 windspeed, turbulence 0.0 scaleXY(35, 9.5)
            //  4 cascades, 1-50 windspeed, turbulence 1.0 scaleXY(40, 19.0)

            //  3 cascades, 1-50 windspeed, turbulence 0.0 scaleXY(18, 1.5)
            //  3 cascades, 1-50 windspeed, turbulence 1.0 scaleXY(22, 3.0)

            //  2 cascades, 1-50 windspeed, turbulence 0.0 scaleXY(8.5, 0.38)
            //  2 cascades, 1-50 windspeed, turbulence 1.0 scaleXY(8.5, 0.51)

            //  1 cascades, 1-50 windspeed, turbulence 0.0 scaleXY(4, 0.115)
            //  1 cascades, 1-50 windspeed, turbulence 1.0 scaleXY(4, 0.135)

            if (cascades == 4)
            {
                return scale * FftSmoothstep(windSpeed, Mathf.Lerp(35, 40, windTurbulence), Mathf.Lerp(9.5f, 23.0f, windTurbulence));
            }
            else if (cascades == 3)
            {
                return scale * FftSmoothstep(windSpeed, Mathf.Lerp(18, 22, windTurbulence), Mathf.Lerp(1.5f, 3.0f, windTurbulence));
            }
            else if (cascades == 2)
            {
                return scale * FftSmoothstep(windSpeed, Mathf.Lerp(8.5f, 8.5f, windTurbulence), Mathf.Lerp(0.38f, 0.51f, windTurbulence));
            }
            else if (cascades == 1)
            {
                return scale * FftSmoothstep(windSpeed, Mathf.Lerp(4, 4, windTurbulence), Mathf.Lerp(0.115f, 0.135f, windTurbulence));
            }


            return 1;

            //cascade | turbulence | wind speed | max amplitude
            //5 | 0 | 50 | 10
            //5 | 0.5 | 50 | 20
            //5 | 1 | 50 | 28
        }


        void CreateInfiniteOceanShadowCullingAnchor()
        {
            _infiniteOceanShadowCullingAnchor = KW_Extensions.CreateHiddenGameObject("InfiniteOceanShadowCullingAnchor");
            var shadowFixInfiniteOceanMaterial = KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.InfiniteOceanShadowCullingAnchorName);
            var shadowFixInfiniteOceanMesh = KWS_CoreUtils.CreateQuad();

            _infiniteOceanShadowCullingAnchor.AddComponent<MeshRenderer>().sharedMaterial = shadowFixInfiniteOceanMaterial;
            _infiniteOceanShadowCullingAnchor.AddComponent<MeshFilter>().sharedMesh = shadowFixInfiniteOceanMesh;


            _infiniteOceanShadowCullingAnchor.transform.rotation = Quaternion.Euler(270, 0, 0);
            _infiniteOceanShadowCullingAnchor.transform.localScale = new Vector3(100000, 100000, 1);
            _infiniteOceanShadowCullingAnchor.transform.parent = UpdateManagerObject.transform;
            _infiniteOceanShadowCullingAnchorTransform = _infiniteOceanShadowCullingAnchor.transform;
        }

        void UpdateInfiniteOceanShadowCullingAnchor(Camera cam)
        {
            var camPos = cam.transform.position;
            var waterRelativePos    = camPos;
            waterRelativePos.y                                  = WaterPivotWorldPosition.y;
            waterRelativePos.y                                  = Mathf.Min(waterRelativePos.y, camPos.y) - 250;
            _infiniteOceanShadowCullingAnchorTransform.position = waterRelativePos;
            _infiniteOceanShadowCullingAnchorTransform.rotation = Quaternion.Euler(270, 0, 0);
        }


        internal void InitializeOrUpdateMesh()
        {
            _meshQuadTree.Initialize(this);
        }


        internal Bounds CalculateCurrentWorldSpaceBounds()
        {
            CurrentMaxHeightOffsetRelativeToWater = Mathf.Max(CurrentMaxWaveHeight, KWS_TileZoneManager.MaxZoneHeight - WaterPivotWorldPosition.y);

            Bounds bounds;
            bounds = new Bounds(WaterPivotWorldPosition + new Vector3(0, -KWS_Settings.Mesh.MaxInfiniteOceanDepth + CurrentMaxHeightOffsetRelativeToWater, 0),
                                new Vector3(1000000, KWS_Settings.Mesh.MaxInfiniteOceanDepth * 2, 1000000));
            //bounds = (Settings.CustomMesh == null) ? new Bounds(WaterPivotWorldPosition, Vector3.one): KW_Extensions.BoundsLocalToWorld(Settings.CustomMesh.bounds, WaterRootTransform, 0, amplitudeOffset);
            WaterBoundsSurfaceHeight = bounds.max.y;

            return bounds;
        }


        internal void RebuildMesh()
        {
            InitializeOrUpdateMesh();
        }


        void SetScaleRotationRelativeToMeshType()
        {
            WaterRootTransform.rotation = Quaternion.identity;
            WaterRootTransform.localScale = Vector3.one;

        }

        internal void UpdateWaterTransformsData()
        {
            if (WaterRootTransform.hasChanged)
            {
                transform.hasChanged = false;

            }
        }


        void InitializeWaterCommonResources()
        {
            InitializeOrUpdateMesh();

            IsWaterInitialized = true;
        }

        private static void UnloadResources()
        {
            if (WaterSharedResources.KWS_OceanFoamTex != null)
            {
                Resources.UnloadAsset(WaterSharedResources.KWS_OceanFoamTex);
                WaterSharedResources.KWS_OceanFoamTex = null;
            }

            if (WaterSharedResources.KWS_IntersectionFoamTex != null)
            {
                Resources.UnloadAsset(WaterSharedResources.KWS_IntersectionFoamTex);
                WaterSharedResources.KWS_IntersectionFoamTex = null;
            }

            if (WaterSharedResources.KWS_FluidsFoamTex != null)
            {
                Resources.UnloadAsset(WaterSharedResources.KWS_FluidsFoamTex);
                WaterSharedResources.KWS_FluidsFoamTex = null;
            }
        }

        static WaterSurfaceRequestPoint _worldPointRequest = new WaterSurfaceRequestPoint();
        private static Dictionary<Camera, UnderwaterSurfaceState> _underwaterStateCameras = new Dictionary<Camera, UnderwaterSurfaceState>();

        private const int MinHeightToDropSplashDrops = 1;

        internal class UnderwaterSurfaceState
        {
            public WaterSurfaceRequestArray Request = new WaterSurfaceRequestArray();
            public Vector3[] CameraNearPlanePoints = new Vector3[6];

            public bool isCameraPartialUnderwater;
            public bool isCameraFullUnderwater;
            public bool IsCameraRequireWaterDrops;
        }

        internal static void RequestUnderwaterState(HashSet<Camera> cameras)
        {
            IsCameraPartialUnderwater = false;
            IsCameraFullUnderwater = false;

            foreach (var stateCamera in _underwaterStateCameras.Keys.ToList())
            {
                if (!cameras.Contains(stateCamera))
                {
                    _underwaterStateCameras.Remove(stateCamera);
                }
            }

            foreach (var camera in cameras)
            {
                if (!_underwaterStateCameras.ContainsKey(camera))
                {
                    _underwaterStateCameras[camera] = new UnderwaterSurfaceState();
                }
            }

            foreach (var stateCamera in _underwaterStateCameras)
            {
                var cam = stateCamera.Key;
                CalculateNearPlaneWorldPoints(stateCamera.Key, ref stateCamera.Value.CameraNearPlanePoints);
                stateCamera.Value.CameraNearPlanePoints[0] = ViewportToWorldPoint(cam, new Vector3(0, 0, cam.nearClipPlane)); //bot left
                stateCamera.Value.CameraNearPlanePoints[1] = ViewportToWorldPoint(cam, new Vector3(1, 0, cam.nearClipPlane)); //bot right
                stateCamera.Value.CameraNearPlanePoints[2] = ViewportToWorldPoint(cam, new Vector3(0, 1, cam.nearClipPlane)); //top left
                stateCamera.Value.CameraNearPlanePoints[3] = ViewportToWorldPoint(cam, new Vector3(1, 1, cam.nearClipPlane)); //top right

                var windSpeed = 0.1f + Mathf.Clamp01(Instance.WindSpeed / 15.0f);

                stateCamera.Value.CameraNearPlanePoints[4] = stateCamera.Value.CameraNearPlanePoints[0] + Vector3.down * windSpeed; //underwater prediction because async readback can't have 100% accuracy position, because ~1 frame delay. 
                stateCamera.Value.CameraNearPlanePoints[5] = stateCamera.Value.CameraNearPlanePoints[1] + Vector3.down * windSpeed;


                stateCamera.Value.Request.SetNewPositions(stateCamera.Value.CameraNearPlanePoints);
                WaterSystem.TryGetWaterSurfaceData(stateCamera.Value.Request);
            }

            foreach (var stateCamera in _underwaterStateCameras)
            {
                if (!stateCamera.Value.Request.IsDataReady) continue;

                var result = stateCamera.Value.Request.Result;
                var points = stateCamera.Value.CameraNearPlanePoints;
                const float cameraMinThreshold = 0.02f;

                if (points[5].y < result[5].Position.y + cameraMinThreshold
                 || points[4].y < result[4].Position.y + cameraMinThreshold
                 || points[0].y < result[0].Position.y + cameraMinThreshold
                 || points[1].y < result[1].Position.y + cameraMinThreshold
                 || points[2].y < result[2].Position.y + cameraMinThreshold
                 || points[3].y < result[3].Position.y + cameraMinThreshold)
                    stateCamera.Value.isCameraPartialUnderwater = true;
                else stateCamera.Value.isCameraPartialUnderwater = false;

                if (points[0].y < result[0].Position.y + cameraMinThreshold
                 && points[1].y < result[1].Position.y + cameraMinThreshold
                 && points[2].y < result[2].Position.y + cameraMinThreshold
                 && points[3].y < result[3].Position.y + cameraMinThreshold) stateCamera.Value.isCameraFullUnderwater = true;
                else stateCamera.Value.isCameraFullUnderwater = false;

                stateCamera.Value.IsCameraRequireWaterDrops = (points[0].y < result[0].Position.y + MinHeightToDropSplashDrops) && result[0].Foam > 0.25f;

            }

        }


        #endregion

        #region Render Logic 


        void RenderWater(Camera cam)
        {
            UpdateWaterTransformsData();

            if (!IsWaterInitialized) InitializeWaterCommonResources();
            if (_infiniteOceanShadowCullingAnchor == null) CreateInfiniteOceanShadowCullingAnchor();
            UpdateInfiniteOceanShadowCullingAnchor(cam);
        }


        internal static Matrix4x4[] KWS_MATRIX_VP;

        internal static void SetGlobalCameraShaderParams(Camera cam)
        {
            KWS_MATRIX_VP = KWS_CoreUtils.SetAllVPCameraMatricesAndGetVP(cam);

            Shader.SetGlobalInt(DynamicWaterParams.KWS_IsCameraPartialUnderwater, IsCameraPartialUnderwater ? 1 : 0);

            Shader.SetGlobalFloat(DynamicWaterParams.KWS_Time, KW_Extensions.TotalTime());
            Shader.SetGlobalFloat(ConstantWaterParams.KW_GlobalTimeScale, WaterSystem.GlobalTimeScale);

            Shader.SetGlobalInteger(ConstantWaterParams.KWS_WaterLayerMask, KWS_Settings.Water.WaterLayer);
            Shader.SetGlobalInteger(ConstantWaterParams.KWS_WaterLightLayerMask, KWS_Settings.Water.LightLayer);

            var camTransform = cam.transform;
            Shader.SetGlobalVector("KWS_CameraForward", camTransform.forward);
            Shader.SetGlobalVector("KWS_CameraRight", camTransform.right);
            Shader.SetGlobalVector("KWS_CameraUp", camTransform.up);

            Shader.SetGlobalInteger("KWS_IsEditorCamera", cam.cameraType == CameraType.SceneView ? 1 : 0);
        }


        internal static void SetGlobalOceanWaterShaderParams()
        {
            Shader.SetGlobalFloat(DynamicWaterParams.KWS_ScaledTime, KW_Extensions.TotalTime() * WaterSystem.Instance.WavesTimeScale);

            Shader.SetGlobalFloat(ConstantWaterParams.KWS_Transparent, Instance.Transparent);
            Shader.SetGlobalFloat(ConstantWaterParams.KWS_WindSpeed, Instance.WindSpeed);
            Shader.SetGlobalFloat(ConstantWaterParams.KWS_WindRotation, Instance.WindRotation);
            Shader.SetGlobalFloat(ConstantWaterParams.KWS_WindTurbulence, Instance.WindTurbulence);
            Shader.SetGlobalFloat(ConstantWaterParams.KWS_WavesCascades, Instance.FftWavesCascades);
            Shader.SetGlobalFloat(ConstantWaterParams.KWS_WavesAreaScale, Instance.WavesAreaScale);
            Shader.SetGlobalFloat(ConstantWaterParams.KW_GlobalTimeScale, GlobalTimeScale);
            Shader.SetGlobalFloat(ConstantWaterParams.KWS_SkyLodRelativeToWind, Instance.SkyLodRelativeToWind);

            Shader.SetGlobalVector(DynamicWaterParams.KWS_WaterPosition, Instance.WaterPivotWorldPosition);
            Shader.SetGlobalVector(ConstantWaterParams.KWS_DyeColor, Instance.DyeColor);
            Shader.SetGlobalVector(ConstantWaterParams.KWS_TurbidityColor, Instance.TurbidityColor);


            Shader.SetGlobalInteger(ConstantWaterParams.KWS_UseFilteredNormals, (int)Instance.FftWavesQuality <= 64 ? 1 : 0);

            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KWS_RefractionAproximatedDepth, Instance.RefractionAproximatedDepth);
            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KWS_RefractionSimpleStrength, Instance.RefractionSimpleStrength);
            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KWS_RefractionDispersionStrength, Instance.RefractionDispersionStrength * KWS_Settings.Water.MaxRefractionDispersion);

            //Shader.SetGlobalInteger(KWS_ShaderConstants.ConstantWaterParams.KWS_UseIntersectionFoam, WaterSystem.QualitySettings.UseIntersectionFoam ? 1 : 0);


            if (WaterSystem.QualitySettings.UseOceanFoam)
            {
                Shader.SetGlobalFloat("KWS_OceanFoamStrength",       Instance.OceanFoamStrength);
                Shader.SetGlobalFloat("KWS_OceanFoamDisappearSpeedMultiplier", Instance.OceanFoamDisappearSpeedMultiplier);

            }

            if (WaterSystem.QualitySettings.UseWetEffect)
            {
                Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KWS_WetStrength, Instance.WetStrength);
            }

            Shader.SetGlobalFloat(KWS_ShaderConstants.CausticID.KWS_CausticDepthScale, Instance.CausticDepth);
            Shader.SetGlobalFloat(KWS_ShaderConstants.CausticID.KWS_CausticStrength, Instance.CausticStrength);
        }


        static void SetQualitySettingsGlobalKeywords(Camera cam)
        {
            var useUnderwaterReflection = WaterSystem.QualitySettings.UseUnderwaterEffect
                                       && WaterSystem.QualitySettings.UnderwaterReflectionMode == WaterQualityLevelSettings.UnderwaterReflectionModeEnum.PhysicalAproximatedReflection
                                       && (WaterSystem.IsCameraPartialUnderwater)
                                       && !cam.orthographic;

      
            var useRefractionIOR = WaterSystem.QualitySettings.RefractionMode == WaterQualityLevelSettings.RefractionModeEnum.PhysicalAproximationIOR;
            var visibleZones = KWS_TileZoneManager.VisibleDynamicWavesZones.Count;

            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_STEREO_INSTANCING_ON, KWS_CoreUtils.SinglePassStereoEnabled);

            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_SSR_REFLECTION,        WaterSystem.QualitySettings.UseScreenSpaceReflection);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_USE_PLANAR_REFLECTION, WaterSystem.QualitySettings.UsePlanarReflection);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_REFLECT_SUN,           WaterSystem.QualitySettings.ReflectSun);

            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_USE_REFRACTION_IOR, useRefractionIOR);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_USE_REFRACTION_DISPERSION, WaterSystem.QualitySettings.UseRefractionDispersion);

            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_USE_VOLUMETRIC_LIGHT, WaterSystem.QualitySettings.UseVolumetricLight);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_USE_ADDITIONAL_CAUSTIC, WaterSystem.QualitySettings.VolumetricLightUseAdditionalLightsCaustic);

            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_DYNAMIC_WAVES_USE_COLOR,        KWS_TileZoneManager.IsAnyDynamicWavesUseColorMode);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_DYNAMIC_WAVES_VISIBLE_ZONES_1,  visibleZones == 1);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_DYNAMIC_WAVES_VISIBLE_ZONES_2,  visibleZones == 2);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_DYNAMIC_WAVES_VISIBLE_ZONES_4,  visibleZones >= 3 && visibleZones <= 4);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_DYNAMIC_WAVES_VISIBLE_ZONES_8,  visibleZones >= 5 && visibleZones <= 8);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE, KWS_TileZoneManager.MovableZone != null);

            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_USE_LOCAL_WATER_ZONES, KWS_TileZoneManager.VisibleLocalWaterZones.Count > 0);

            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_USE_CAUSTIC, WaterSystem.QualitySettings.UseCausticEffect);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_USE_CAUSTIC_FILTERING, WaterSystem.QualitySettings.UseCausticHighQualityFiltering);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_USE_CAUSTIC_DISPERSION, WaterSystem.QualitySettings.UseCausticDispersion);

            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_USE_UNDERWATER_REFLECTION, useUnderwaterReflection);
            Shader.SetKeyword(KWS_ShaderConstants.WaterKeywords.GlobalKeyword_KWS_USE_HALF_LINE_TENSION, WaterSystem.QualitySettings.UseUnderwaterHalfLineTensionEffect);
        }

        static void SetQualitySettingsShaderParams()
        {
            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KW_WaterFarDistance, WaterSystem.QualitySettings.MeshDetailingFarDistance);

            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KW_ReflectionClipOffset, WaterSystem.QualitySettings.ReflectionClipPlaneOffset);
            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KWS_SunCloudiness, WaterSystem.QualitySettings.ReflectedSunCloudinessStrength);
            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KWS_SunStrength, WaterSystem.QualitySettings.ReflectedSunStrength);


            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KWS_UnderwaterHalfLineTensionScale, WaterSystem.QualitySettings.UnderwaterHalfLineTensionScale);


            Shader.SetGlobalInteger(KWS_ShaderConstants.ConstantWaterParams.KWS_OverrideSkyColor, WaterSystem.QualitySettings.OverrideSkyColor ? 1 : 0);
            Shader.SetGlobalInteger(KWS_ShaderConstants.ConstantWaterParams.KWS_UseOceanFoam, WaterSystem.QualitySettings.UseOceanFoam ? 1 : 0);

            Shader.SetGlobalInteger(KWS_ShaderConstants.ConstantWaterParams.KWS_UseRefractionIOR, WaterSystem.QualitySettings.RefractionMode == WaterQualityLevelSettings.RefractionModeEnum.PhysicalAproximationIOR ? 1 : 0);
            Shader.SetGlobalInteger(KWS_ShaderConstants.ConstantWaterParams.KWS_UseRefractionDispersion, WaterSystem.QualitySettings.UseRefractionDispersion ? 1 : 0);

            Shader.SetGlobalVector(KWS_ShaderConstants.ConstantWaterParams.KWS_CustomSkyColor, WaterSystem.QualitySettings.CustomSkyColor);



            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KWS_ScreenSpaceBordersStretching, WaterSystem.QualitySettings.ScreenSpaceBordersStretching);
            Shader.SetGlobalInt(KWS_ShaderConstants.ConstantWaterParams.UseScreenSpaceReflectionSky, WaterSystem.QualitySettings.UseScreenSpaceReflectionSky ? 1 : 0);

            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KWS_AnisoReflectionsScale, WaterSystem.QualitySettings.AnisotropicReflectionsScale);
            Shader.SetGlobalFloat(KWS_ShaderConstants.VolumetricLightConstantsID.KWS_VolumetricLightTemporalAccumulationFactor, WaterSystem.QualitySettings.VolumetricLightTemporalReprojectionAccumulationFactor);

            Shader.SetGlobalInteger(KWS_ShaderConstants.VolumetricLightConstantsID.KWS_RayMarchSteps, WaterSystem.QualitySettings.VolumetricLightIteration);

        }

        static void SetSettingsConstantShaderParams()
        {
            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KWS_SunMaxValue, KWS_Settings.Reflection.MaxSunStrength);
            Shader.SetGlobalFloat(KWS_ShaderConstants.ConstantWaterParams.KWS_AbsorbtionOverrideMultiplier, KWS_Settings.VolumetricLighting.AbsorbtionOverrideMultiplier);
            Shader.SetGlobalFloatArray(KWS_ShaderConstants.ConstantWaterParams.KWS_WavesDomainSizes, KWS_Settings.FFT.FftDomainSizes);
            Shader.SetGlobalVectorArray(KWS_ShaderConstants.ConstantWaterParams.KWS_WavesDomainScales, KWS_Settings.FFT.FftDomainScales);
            Shader.SetGlobalFloatArray(KWS_ShaderConstants.ConstantWaterParams.KWS_WavesDomainVisiableArea, KWS_Settings.FFT.FftDomainVisiableArea);
        }

        #endregion

    }

}