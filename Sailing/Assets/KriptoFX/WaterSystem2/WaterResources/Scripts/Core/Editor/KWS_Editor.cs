#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static KWS.KWS_EditorUtils;
using Debug = UnityEngine.Debug;
using static KWS.WaterSystem;
using static KWS.KWS_Settings;

using Description = KWS.KWS_EditorTextDescription;
using link = KWS.KWS_EditorUrlLinks;

namespace KWS
{
    [System.Serializable]
    [CustomEditor(typeof(WaterSystem))]
    internal partial class KWS_Editor : Editor
    {
        private WaterSystem _waterInstance;
        private WaterQualityLevelSettings _settings;

        private bool _isActive;
        private SceneView.SceneViewState _lastSceneView;


        void OnDestroy()
        {
            KWS_EditorUtils.Release();
        }


        public override void OnInspectorGUI()
        {
            _waterInstance = (WaterSystem)target;

            if (_waterInstance.enabled && _waterInstance.gameObject.activeSelf)
            {
                _isActive = true;
                GUI.enabled = true;
            }
            else
            {
                _isActive = false;
                GUI.enabled = false;
            }

            UpdateWaterGUI();
        }


        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUICustom;
        }


        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUICustom;
        }

        void OnSceneGUICustom(SceneView sceneView)
        {
            if (Event.current.type == EventType.Repaint)
            {
                SceneView.RepaintAll();
            }
        }

        void UpdateWaterGUI()
        {
            _settings = WaterSystem.QualitySettings;
            if (_settings == null)
            {
                KWS_CustomSettingsProvider.LoadActualWaterSettings();
                _settings = WaterSystem.QualitySettings;
                if (_settings == null)
                {
                    Debug.Log("empty settings?");
                    return;
                }
            }

            Undo.RecordObject(_waterInstance, "Changed main water parameters");
#if KWS_DEBUG
            WaterSystem.Test4 = EditorGUILayout.Vector4Field("Test4", WaterSystem.Test4);
            if (KWS_CoreUtils.SinglePassStereoEnabled) VRScale = Slider("VR Scale", "", VRScale, 0.5f, 2.5f, "");
            WaterSystem.TestTexture = (Texture2D)EditorGUILayout.ObjectField(WaterSystem.TestTexture, typeof(Texture2D), true);
            if (WaterSystem.TestTexture != null)
            {
                Shader.SetGlobalTexture("KWS_TestTexture", WaterSystem.TestTexture);
            }
#endif
            //waterSystem.TestObj = (GameObject) EditorGUILayout.ObjectField(waterSystem.TestObj, typeof(GameObject), true);


            EditorGUI.BeginChangeCheck();

            //CheckMessages();

            GUI.enabled = _isActive;
            bool defaultVal = false;

            EditorGUILayout.Space(20);  

            KWS_Tab(ref _waterInstance.ShowColorSettings,         useHelpBox: false, useExpertButton: false, ref defaultVal, "Color Settings",                       ColorSettings,     WaterSettingsCategory.ColorSettings);
            KWS_Tab(ref _waterInstance.ShowWavesSettings,         useHelpBox: false, useExpertButton: false, ref defaultVal, "Waves",                                WavesSettings,     WaterSettingsCategory.Waves);
            KWS_Tab(ref _waterInstance.ShowRefractionSettings,    useHelpBox: false, useExpertButton: false, ref defaultVal, "Refraction (View Through Water)",      RefractionSetting, WaterSettingsCategory.Foam);
            KWS_Tab(ref _waterInstance.ShowFoamSettings,          useHelpBox: false, useExpertButton: false, ref defaultVal, "Ocean Foam",                           FoamSetting,       WaterSettingsCategory.Foam);
            KWS_Tab(ref _waterInstance.ShowWetSettings,           useHelpBox: false, useExpertButton: false, ref defaultVal, "Wet Effect",                           WetSetting,        WaterSettingsCategory.WetEffect);
            KWS_Tab(ref _waterInstance.ShowCausticEffectSettings, useHelpBox: false, useExpertButton: false, ref defaultVal, "Caustic (Light Patterns on Surfaces)", CausticSettings,   WaterSettingsCategory.WetEffect);

            if (EditorGUI.EndChangeCheck())
            {
                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(_waterInstance);
                    EditorSceneManager.MarkSceneDirty(_waterInstance.gameObject.scene);
                }
            }

        }



        void ColorSettings()
        {
            _waterInstance.Transparent = Slider("Transparent (Meters)", Description.Color.Transparent, _waterInstance.Transparent, 1f, 100f, link.Transparent);
            _waterInstance.DyeColor = ColorFieldHUE("Dye Color", Description.Color.WaterColor, _waterInstance.DyeColor, false, false, false, link.DyeColor);
            _waterInstance.TurbidityColor = ColorField("Turbidity Color", Description.Color.TurbidityColor, _waterInstance.TurbidityColor, false, false, false, link.TurbidityColor);
        }

        void WavesSettings()
        {

            _waterInstance.WindZone = (WindZone)EditorGUILayout.ObjectField(_waterInstance.WindZone, typeof(WindZone), true);
            if (_waterInstance.WindZone != null)
            {
                _waterInstance.WindZoneSpeedMultiplier = Slider("Wind Speed Multiplier", "", _waterInstance.WindZoneSpeedMultiplier, 0.01f, 10, link.WindZoneSpeedMultiplier);
                _waterInstance.WindZoneTurbulenceMultiplier = Slider("Wind Turbulence Multiplier", "", _waterInstance.WindZoneTurbulenceMultiplier, 0.01f, 10.0f, link.WindZoneTurbulenceMultiplier);
            }
            else
            {
                _waterInstance.WindSpeed = Slider(" Wind Speed", Description.Waves.WindSpeed, _waterInstance.WindSpeed, 0.1f, FFT.MaxWindSpeed, link.WindSpeed);
                _waterInstance.WindRotation = Slider(" Wind Rotation", Description.Waves.WindRotation, _waterInstance.WindRotation, 0.0f, 360.0f, link.WindRotation);
                _waterInstance.WindTurbulence = Slider(" Wind Turbulence", Description.Waves.WindTurbulence, _waterInstance.WindTurbulence, 0.0f, 1.0f, link.WindTurbulence);
            }
            Line();
            _waterInstance.FftWavesQuality = (WaterQualityLevelSettings.FftWavesQualityEnum)EnumPopup(" Waves Quality", Description.Waves.FftWavesQuality, _waterInstance.FftWavesQuality, link.FftWavesQuality);
            _waterInstance.FftWavesCascades = IntSlider(" Simulation Cascades", "", _waterInstance.FftWavesCascades, 1, FFT.MaxLods, link.FftWavesCascades);
            _waterInstance.WavesAreaScale = Slider(" Area Scale", "", _waterInstance.WavesAreaScale, 0.2f, KWS_Settings.FFT.MaxWavesAreaScale, link.WavesAreaScale);
            _waterInstance.WavesTimeScale = Slider(" Time Scale", Description.Waves.TimeScale, _waterInstance.WavesTimeScale, 0.0f, 2.0f, link.TimeScale);

#if KWS_DEBUG
            EditorGUILayout.Space(20);
            _waterInstance.DebugQuadtree = Toggle("Debug Quadtree", "", _waterInstance.DebugQuadtree, "");
            _waterInstance.DebugAABB = Toggle("Debug AABB", "", _waterInstance.DebugAABB, "");
            _waterInstance.DebugDynamicWaves = Toggle("Debug Dynamic Waves", "", _waterInstance.DebugDynamicWaves, "");
            _waterInstance.DebugBuoyancy = Toggle("Debug Buoyancy", "", _waterInstance.DebugBuoyancy, "");
            _waterInstance.DebugUpdateManager = Toggle("Debug Update Manager", "", _waterInstance.DebugUpdateManager, "");
#endif
        }

        void RefractionSetting()
        {
            if (_settings.RefractionMode == WaterQualityLevelSettings.RefractionModeEnum.PhysicalAproximationIOR)
            {
                _waterInstance.RefractionAproximatedDepth = Slider("Aproximated Depth", Description.Refraction.RefractionAproximatedDepth, _waterInstance.RefractionAproximatedDepth, 0.25f, 10f, link.RefractionAproximatedDepth);
            }

            if (_settings.RefractionMode == WaterQualityLevelSettings.RefractionModeEnum.Simple)
            {
                _waterInstance.RefractionSimpleStrength = Slider("Strength", Description.Refraction.RefractionSimpleStrength, _waterInstance.RefractionSimpleStrength, 0.02f, 1, link.RefractionSimpleStrength);
            }

            if (_settings.UseRefractionDispersion)
            {
                _waterInstance.RefractionDispersionStrength = Slider("Dispersion Strength", Description.Refraction.RefractionDispersionStrength, _waterInstance.RefractionDispersionStrength, 0.25f, 1,
                                                                     link.RefractionDispersionStrength);
            }

        }


        void FoamSetting()
        {
            _waterInstance.OceanFoamStrength                 = Slider("Ocean Foam Strength",        "", _waterInstance.OceanFoamStrength,       0.0f, 1,  link.OceanFoamStrength, false);
            _waterInstance.OceanFoamDisappearSpeedMultiplier = Slider("Ocean Foam Disappear Speed", "", _waterInstance.OceanFoamDisappearSpeedMultiplier, 0.25f,    1f, "",                     false);
            //_waterInstance.OceanFoamTextureSize = Slider("Ocean Foam Texture Size", "", _waterInstance.OceanFoamTextureSize, 5, 50, link.TextureFoamSize, false);
        }

        void WetSetting()
        {
#if KWS_URP
            if (!KWS_CustomSettingsProvider.IsDecalFeatureUsed)
            {
                EditorGUILayout.HelpBox("To make the \"Wet Effect\" work, you need to enable the URP Decal Rendering Feature." + Environment.NewLine +
                                        "Check the documentation for how to set it up.", MessageType.Error);
            }
                
#endif
            _waterInstance.WetStrength = Slider("Wet Strength", "", _waterInstance.WetStrength, 0.1f, 1.0f, "");
        }


        void CausticSettings()
        {
            _waterInstance.CausticDepth = Slider("Caustic Depth(m)", Description.Caustic.CausticDepthScale, _waterInstance.CausticDepth, 0.5f, Caustic.MaxCausticDepth, link.CausticDepthScale);
            _waterInstance.CausticStrength = Slider("Caustic Strength", Description.Caustic.CausticStrength, _waterInstance.CausticStrength, 0.25f, 5, link.CausticStrength);
        }
    }
}

#endif