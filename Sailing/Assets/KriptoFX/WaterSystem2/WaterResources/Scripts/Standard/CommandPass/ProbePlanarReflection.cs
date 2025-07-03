#if KWS_HDRP
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace KWS
{
    internal class ProbePlanarReflection: WaterPass
    {
        private PlanarReflectionProbe _probe;
        private GameObject _probeGO;
        private Transform _probeTransform;
        private Material _filteringMaterial;
        private CommandBuffer _cmdAnisoFiltering;
        RenderTexture _currentPlanarRT;
        RenderTexture _planarMipFilteredRT;
        private WaterSystem _waterInstance;

        private readonly Dictionary<WaterQualityLevelSettings.PlanarReflectionResolutionQualityEnum, PlanarReflectionAtlasResolution> _planarResolutions
            = new Dictionary<WaterQualityLevelSettings.PlanarReflectionResolutionQualityEnum, PlanarReflectionAtlasResolution>()

            {
                {WaterQualityLevelSettings.PlanarReflectionResolutionQualityEnum.Ultra, PlanarReflectionAtlasResolution.Resolution1024},
                {WaterQualityLevelSettings.PlanarReflectionResolutionQualityEnum.High, PlanarReflectionAtlasResolution.Resolution1024},
                {WaterQualityLevelSettings.PlanarReflectionResolutionQualityEnum.Medium, PlanarReflectionAtlasResolution.Resolution512},
                {WaterQualityLevelSettings.PlanarReflectionResolutionQualityEnum.Low, PlanarReflectionAtlasResolution.Resolution512},
                {WaterQualityLevelSettings.PlanarReflectionResolutionQualityEnum.VeryLow, PlanarReflectionAtlasResolution.Resolution256}
            };

        
        public ProbePlanarReflection()
        {
            WaterSystem.OnAnyWaterSettingsChanged += OnWaterSettingsChanged;
        }
        
        
        public override void ExecuteBeforeCameraRendering(Camera cam, ScriptableRenderContext context)
        {
            if (!WaterSystem.QualitySettings.UsePlanarReflection) return; 
            
            RenderPlanar(cam);
        }
        
        public void RenderPlanar(Camera currentCamera)
        {
            if (_probeGO == null) CreateProbe();

            var cameraPos = currentCamera.transform.position;
            _probeTransform.position = new Vector3(cameraPos.x, WaterSystem.Instance.WaterPivotWorldPosition.y, cameraPos.z);

            UpdateRT();
        }


        private void OnWaterSettingsChanged(WaterSystem.WaterSettingsCategory changedTab)
        {
            if (!changedTab.HasTab(WaterSystem.WaterSettingsCategory.Reflection)) return;

            if (!WaterSystem.QualitySettings.UsePlanarReflection)
            {
                if (_probeGO != null) KW_Extensions.SafeDestroy(_probeGO);
                return;
            }
            else
            {
                UpdateProbeSettings();
                UpdateRT();
            }

        }

        public override void Release()
        {
            WaterSystem.OnAnyWaterSettingsChanged -= OnWaterSettingsChanged;

            KW_Extensions.SafeDestroy(_probeGO, _filteringMaterial);
        }

        void UpdateRT()
        {
            if (_probe == null || _probeGO == null) return;

            _currentPlanarRT = _probe.realtimeTexture;
            if (_currentPlanarRT == null) return;
            CreateTargetTexture(_currentPlanarRT.width, _currentPlanarRT.graphicsFormat);

            WaterSharedResources.PlanarReflection = _currentPlanarRT;
        }

        void CreateProbe()
        {
            _probeGO               = new GameObject("PlanarReflectionProbe");
            _probeGO.layer         = KWS_Settings.Water.WaterLayer;
            _probeTransform        = _probeGO.transform;
            _probeTransform.parent = WaterSystem.UpdateManagerObject.transform;
            _probe                 = _probeGO.AddComponent<PlanarReflectionProbe>();
            
            _probe.mode                    = ProbeSettings.Mode.Realtime;
            _probe.realtimeMode            = ProbeSettings.RealtimeMode.EveryFrame;
            _probe.influenceVolume.boxSize = new Vector3(100000, float.MinValue, 100000);

            UpdateProbeSettings();
        }

        void UpdateProbeSettings()
        {
            if (_probe == null || _probeGO == null) return;

            _probe.DisableAllCameraFrameSettings();
           
            _probe.SetFrameSetting(FrameSettingsField.OpaqueObjects,      true);
            _probe.SetFrameSetting(FrameSettingsField.TransparentObjects, true);

            _probe.SetFrameSetting(FrameSettingsField.VolumetricClouds,      WaterSystem.QualitySettings.RenderPlanarClouds);
            _probe.SetFrameSetting(FrameSettingsField.AtmosphericScattering, WaterSystem.QualitySettings.RenderPlanarVolumetricsAndFog);
            _probe.SetFrameSetting(FrameSettingsField.Volumetrics,           WaterSystem.QualitySettings.RenderPlanarVolumetricsAndFog);
            _probe.SetFrameSetting(FrameSettingsField.ShadowMaps,            WaterSystem.QualitySettings.RenderPlanarShadows);

            _probe.settingsRaw.roughReflections                       = false;
            _probe.settings.resolutionScalable.useOverride            = true;
            _probe.settings.resolutionScalable.@override              = _planarResolutions[WaterSystem.QualitySettings.PlanarReflectionResolutionQuality];
            _probe.settingsRaw.cameraSettings.culling.cullingMask     = WaterSystem.QualitySettings.PlanarCullingMask;
            _probe.settingsRaw.cameraSettings.customRenderingSettings = true;

            _probeGO.SetActive(false);
            _probeGO.SetActive(true);
        }

        void CreateTargetTexture(int size, GraphicsFormat graphicsFormat)
        {
            if (_planarMipFilteredRT != null && (_planarMipFilteredRT.width != size || _planarMipFilteredRT.graphicsFormat != graphicsFormat))
            {
                _planarMipFilteredRT.Release();
                _planarMipFilteredRT = null;
            }

            if(_planarMipFilteredRT == null) _planarMipFilteredRT = new RenderTexture(size, size, 0, graphicsFormat) { name = "_planarMipFilteredRT", autoGenerateMips = false, useMipMap = false };
        }
    }
}
#endif