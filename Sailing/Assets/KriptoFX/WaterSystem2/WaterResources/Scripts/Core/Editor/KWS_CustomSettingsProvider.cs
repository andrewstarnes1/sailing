using System.IO;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UIElements;


using Description = KWS.KWS_EditorTextDescription;
using link = KWS.KWS_EditorUrlLinks;
using static KWS.KWS_Settings;
using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using static KWS.KWS_EditorUtils;
#endif


namespace KWS
{
    
    public static class KWS_WaterSettingsRuntimeLoader
    {
        internal static WaterQualityLevelSettings  _settings;
        
        internal static void  LoadCurrentSettings(out WaterSystemQualitySettings settingsContainer, out WaterQualityLevelSettings currentSettings)
        {
            settingsContainer = null;
            currentSettings   = null;
            
            settingsContainer = Resources.Load<WaterSystemQualitySettings>("WaterQualitySettings");
            if (!settingsContainer)
            {
                Debug.LogError("WaterQualitySettings.asset not found in Resources.");
                return;
            }

            var levelName       = QualitySettings.names[QualitySettings.GetQualityLevel()];
            currentSettings = settingsContainer.qualityLevelSettings.FirstOrDefault(x => x.levelName == levelName);

            if (currentSettings == null)
            {
                Debug.LogWarning($"No settings found for quality level '{levelName}'.");
            }

        }

        public static void LoadActualWaterSettings()
        {
            WaterSystemQualitySettings settingsContainer;
            KWS_WaterSettingsRuntimeLoader.LoadCurrentSettings(out settingsContainer, out _settings);
        }
       
    }
#if UNITY_EDITOR
    class KWS_CustomSettingsProvider : SettingsProvider
    {
        static          SerializedObject           _settingsObject;
        static          WaterSystemQualitySettings _settingsContainer;

        bool                                       _isThirdPartyFogAvailable;
        internal static bool                       IsDecalFeatureUsed;


        public const string settingsName = "WaterQualitySettings.asset";
        public KWS_CustomSettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope) { }

        public static void LoadActualWaterSettings()
        {
            KWS_WaterSettingsRuntimeLoader.LoadCurrentSettings(out _settingsContainer, out KWS_WaterSettingsRuntimeLoader._settings);

            if (!_settingsContainer || _settingsContainer.qualityLevelSettings.Count == 0 || _settingsContainer.qualityLevelSettings.Count != QualitySettings.names.Length)
            {
                GetSerializedSettings(out _settingsObject, out _settingsContainer);
                SyncWithUnityQualityLevels(_settingsContainer);
                KWS_WaterSettingsRuntimeLoader.LoadCurrentSettings(out _settingsContainer, out KWS_WaterSettingsRuntimeLoader._settings);
            }

            #if KWS_URP
                var renderFeatures        = GetRendererFeatures();
                IsDecalFeatureUsed = renderFeatures.Any(f => f.name == "DecalRendererFeature");
                
            #endif
        }

        internal static WaterSystemQualitySettings GetOrCreateSettings()
        {
            var pathToResourcesFolder = KW_Extensions.GetFullPathToResourcesFolder();
            if (string.IsNullOrEmpty(pathToResourcesFolder)) return null;

            var pathToSettingsFile = Path.Combine(pathToResourcesFolder.GetRelativeToAssetsPath(), settingsName);
            var settings = Resources.Load<WaterSystemQualitySettings>("WaterQualitySettings");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<WaterSystemQualitySettings>();
                AssetDatabase.CreateAsset(settings, pathToSettingsFile);
                AssetDatabase.SaveAssets();
                KWS_EditorUtils.DisplayMessageNotification($"The water settings file is saved in {pathToSettingsFile}", false, 6);
                Debug.Log($"The water settings file is saved in {pathToSettingsFile}");
            }
            return settings;
        }

        public static void GetSerializedSettings(out SerializedObject settingsObject, out WaterSystemQualitySettings settings)
        {
            settings = GetOrCreateSettings();
            settingsObject = new SerializedObject(settings);
        }

        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            return new KWS_CustomSettingsProvider("Project/KWS Water Settings", SettingsScope.Project);
        }

        public static void SyncWithUnityQualityLevels(WaterSystemQualitySettings settings)
        {
            var unityLevels = QualitySettings.names;

            foreach (var level in unityLevels)
            {
                if (!settings.qualityLevelSettings.Exists(x => x.levelName == level))
                {
                    var newQualityLevel = new WaterQualityLevelSettings { levelName = level };
                    SyncDefualtQualitySettings(newQualityLevel, level);
                    settings.qualityLevelSettings.Add(newQualityLevel);
                }
            }

            settings.qualityLevelSettings.RemoveAll(x => !unityLevels.Contains(x.levelName));
        }

        public static void SyncDefualtQualitySettings(WaterQualityLevelSettings settings, string level)
        {
            level                                           = level.Replace(" ", "");
            settings.ScreenSpaceReflectionResolutionQuality = KW_Extensions.StringToEnum(level, WaterQualityLevelSettings.ScreenSpaceReflectionResolutionQualityEnum.High);
            settings.VolumetricLightResolutionQuality       = KW_Extensions.StringToEnum(level, WaterQualityLevelSettings.VolumetricLightResolutionQualityEnum.High);
            settings.CausticTextureResolutionQuality        = KW_Extensions.StringToEnum(level, WaterQualityLevelSettings.CausticTextureResolutionQualityEnum.High);
            settings.WaterMeshDetailing                     = KW_Extensions.StringToEnum(level, WaterQualityLevelSettings.WaterMeshQualityEnum.High);
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            GetSerializedSettings(out _settingsObject, out _settingsContainer);
            SyncWithUnityQualityLevels(_settingsContainer);
        }

        public override void OnGUI(string searchContext)
        {
            bool defaultVal = false;

            Undo.RecordObject(_settingsContainer, "Changed water quality parameters");

            EditorGUIUtility.labelWidth = 80;
            GUILayout.Space(10);
            UnityQualitySettings();
            GUILayout.Space(15);

            EditorGUI.BeginChangeCheck();
            EditorGUIUtility.labelWidth = 220;

            CheckWarnings();

            var _settings = KWS_WaterSettingsRuntimeLoader._settings;

            KWS2_Tab(ref _settingsContainer.ShowReflectionSettings, useHelpBox: true, useExpertButton: true, ref _settingsContainer.ShowExpertReflectionSettings, "Reflection", ReflectionSettings, WaterSystem.WaterSettingsCategory.Reflection);
            KWS2_Tab(ref _settingsContainer.ShowRefractionSettings, useHelpBox: true, useExpertButton: false, ref defaultVal, "Refraction (View Through Water)", RefractionSetting, WaterSystem.WaterSettingsCategory.ColorRefraction);
            KWS2_TabWithEnabledToogle(ref _settings.UseOceanFoam, ref _settingsContainer.ShowWetSettings, useExpertButton: false, ref defaultVal, "Ocean Foam", FoamSetting, WaterSystem.WaterSettingsCategory.Foam);
            KWS2_TabWithEnabledToogle(ref _settings.UseWetEffect, ref _settingsContainer.ShowWetSettings, useExpertButton: false, ref defaultVal, "Wet Effect", WetSetting, WaterSystem.WaterSettingsCategory.WetEffect);
            KWS2_TabWithEnabledToogle(ref _settings.UseVolumetricLight, ref _settingsContainer.ShowVolumetricLightSettings, useExpertButton: false, ref defaultVal, "Volumetric Lighting", VolumetricLightingSettings, WaterSystem.WaterSettingsCategory.VolumetricLighting);
            KWS2_TabWithEnabledToogle(ref _settings.UseCausticEffect, ref _settingsContainer.ShowCausticEffectSettings, useExpertButton: false, ref defaultVal, "Caustic (Light Patterns on Surfaces)", CausticSettings, WaterSystem.WaterSettingsCategory.Caustic);
            KWS2_TabWithEnabledToogle(ref _settings.UseUnderwaterEffect, ref _settingsContainer.ShowUnderwaterEffectSettings, useExpertButton: false, ref defaultVal, "Underwater Effects", UnderwaterSettings, WaterSystem.WaterSettingsCategory.Underwater);
            KWS2_Tab(ref _settingsContainer.ShowMeshSettings, useHelpBox: true, useExpertButton: false, ref defaultVal, "Mesh Settings", MeshSettings, WaterSystem.WaterSettingsCategory.Mesh);
            KWS2_Tab(ref _settingsContainer.ShowRendering, useHelpBox: true, useExpertButton: false, ref defaultVal, "Rendering Settings", RenderingSetting, WaterSystem.WaterSettingsCategory.Rendering);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settingsContainer);
                // AssetDatabase.SaveAssets();
            }

            if (_settingsObject != null && _settingsObject.targetObject != null)
            {
                _settingsObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }


        void CheckWarnings()
        {
#if KWS_URP
            if (!IsDecalFeatureUsed)
            {
                EditorGUILayout.HelpBox("To make the \"Wet Effect\" work, you need to enable the URP Decal Rendering Feature." + Environment.NewLine +
                                        "Check the documentation for how to set it up.", MessageType.Error);
            }
                
#endif
        }
        
        
        void UnityQualitySettings()
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("Unity Quality Levels", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            var qualityLevels = QualitySettings.names;
            var currentQualityLevel = QualitySettings.GetQualityLevel();
            var selectedLevel = currentQualityLevel;

            for (int idx = 0; idx < qualityLevels.Length; idx++)
            {
                string level = qualityLevels[idx];
                var newValue = Toggle(level, "", selectedLevel == idx, "", false);
                if (newValue == true) selectedLevel = idx;
            }
            QualitySettings.SetQualityLevel(selectedLevel);
            EditorGUI.indentLevel--;


            var levelName = qualityLevels[selectedLevel];
            var levelSettings = _settingsContainer.qualityLevelSettings.Find(x => x.levelName == levelName);
            if (levelSettings == null)
            {
                levelSettings = new WaterQualityLevelSettings { levelName = levelName };
                _settingsContainer.qualityLevelSettings.Add(levelSettings);

                EditorUtility.SetDirty(_settingsContainer);
                AssetDatabase.SaveAssets();
            }
            KWS_WaterSettingsRuntimeLoader._settings = levelSettings;

            if (currentQualityLevel != selectedLevel) WaterSystem.OnAnyWaterSettingsChanged?.Invoke(WaterSystem.WaterSettingsCategory.All);

        }


        void ReflectionSettings()
        {
            //KWS_EditorProfiles.PerfomanceProfiles.Reflection.ReadDataFromProfile(_waterSystem);
            var _settings = KWS_WaterSettingsRuntimeLoader._settings;
            _settings.UseScreenSpaceReflection = Toggle("Use Screen Space Reflection", Description.Reflection.UseScreenSpaceReflection, _settings.UseScreenSpaceReflection, link.UseScreenSpaceReflection);

            if (_settings.UseScreenSpaceReflection)
            {
                _settings.ScreenSpaceReflectionResolutionQuality = (WaterQualityLevelSettings.ScreenSpaceReflectionResolutionQualityEnum)EnumPopup("Screen Space Resolution Quality",
                                                                                                                                                   Description.Reflection.ScreenSpaceReflectionResolutionQuality, _settings.ScreenSpaceReflectionResolutionQuality, link.ScreenSpaceReflectionResolutionQuality);

                if (_settingsContainer.ShowExpertReflectionSettings)
                {
                    //_settings.UseScreenSpaceReflectionHolesFilling = Toggle("Holes Filling", "", _settings.UseScreenSpaceReflectionHolesFilling, link.UseScreenSpaceReflectionHolesFilling));
                    _settings.UseScreenSpaceReflectionSky = Toggle("Use Screen Space Skybox", "", _settings.UseScreenSpaceReflectionSky, "");
                    _settings.ScreenSpaceBordersStretching = Slider("Borders Stretching", "", _settings.ScreenSpaceBordersStretching, 0f, 0.05f, link.ScreenSpaceBordersStretching);
                }

                Line();
            }



            _settings.UsePlanarReflection = Toggle("Use Planar Reflection", Description.Reflection.UsePlanarReflection, _settings.UsePlanarReflection, link.UsePlanarReflection);
            if (_settings.UsePlanarReflection)
            {
                var layerNames = new List<string>();
                for (int i = 0; i <= 31; i++)
                {
                    layerNames.Add(LayerMask.LayerToName(i));
                }

                EditorGUILayout.HelpBox(Description.Warnings.PlanarReflectionUsed, MessageType.Warning);
                _settings.RenderPlanarShadows = Toggle("Planar Shadows", "", _settings.RenderPlanarShadows, link.RenderPlanarShadows);

                if (Reflection.IsVolumetricsAndFogAvailable)
                    _settings.RenderPlanarVolumetricsAndFog = Toggle("Planar Volumetrics and Fog", "", _settings.RenderPlanarVolumetricsAndFog, link.RenderPlanarVolumetricsAndFog);
                if (Reflection.IsCloudRenderingAvailable) _settings.RenderPlanarClouds = Toggle("Planar Clouds", "", _settings.RenderPlanarClouds, link.RenderPlanarClouds);

                _settings.PlanarReflectionResolutionQuality =
                    (WaterQualityLevelSettings.PlanarReflectionResolutionQualityEnum)EnumPopup("Planar Resolution Quality", Description.Reflection.PlanarReflectionResolutionQuality, _settings.PlanarReflectionResolutionQuality,
                                                                                               link.PlanarReflectionResolutionQuality);

                var planarCullingMask = MaskField("Planar Layers Mask", Description.Reflection.PlanarCullingMask, _settings.PlanarCullingMask, layerNames.ToArray(), link.PlanarCullingMask);
                _settings.PlanarCullingMask = planarCullingMask & ~(1 << Water.WaterLayer);

            }

            if (_settingsContainer.ShowExpertReflectionSettings && (_settings.UsePlanarReflection || _settings.UseScreenSpaceReflection))
            {
                _settings.ReflectionClipPlaneOffset = Slider("Clip Plane Offset", Description.Reflection.ReflectionClipPlaneOffset, _settings.ReflectionClipPlaneOffset, 0, 0.07f,
                                                             link.ReflectionClipPlaneOffset);
            }

            if (_settings.UseScreenSpaceReflection || _settings.UsePlanarReflection)
            {
                Line();
                _settings.UseAnisotropicReflections = Toggle("Use Anisotropic Reflections", Description.Reflection.UseAnisotropicReflections, _settings.UseAnisotropicReflections, link.UseAnisotropicReflections);

                if (_settings.UseAnisotropicReflections && _settingsContainer.ShowExpertReflectionSettings)
                {
                    _settings.AnisotropicReflectionsScale = Slider("Anisotropic Reflections Scale", Description.Reflection.AnisotropicReflectionsScale, _settings.AnisotropicReflectionsScale, 0.1f, 1.0f,
                                                                   link.AnisotropicReflectionsScale);
                    _settings.AnisotropicReflectionsHighQuality = Toggle("High Quality Anisotropic", Description.Reflection.AnisotropicReflectionsHighQuality, _settings.AnisotropicReflectionsHighQuality,
                                                                         link.AnisotropicReflectionsHighQuality);
                }

            }


            Line();

            _settings.OverrideSkyColor = Toggle("Override Sky Color", "", _settings.OverrideSkyColor, link.OverrideSkyColor);
            if (_settings.OverrideSkyColor)
            {
                _settings.CustomSkyColor = ColorField("Custom Sky Color", "", _settings.CustomSkyColor, false, false, false, link.OverrideSkyColor);
            }

            _settings.ReflectSun = Toggle("Reflect Sunlight", Description.Reflection.ReflectSun, _settings.ReflectSun, link.ReflectSun);
            if (_settings.ReflectSun)
            {
                _settings.ReflectedSunCloudinessStrength = Slider("Sun Cloudiness", Description.Reflection.ReflectedSunCloudinessStrength, _settings.ReflectedSunCloudinessStrength, 0.03f, 0.25f,
                                                                  link.ReflectedSunCloudinessStrength);
                if (_settingsContainer.ShowExpertReflectionSettings)
                    _settings.ReflectedSunStrength = Slider("Sun Strength", Description.Reflection.ReflectedSunStrength, _settings.ReflectedSunStrength, 0f, 1f, link.ReflectedSunStrength);
            }

            CheckPlatformSpecificMessages_Reflection();

            //KWS_EditorProfiles.PerfomanceProfiles.Reflection.CheckDataChangesAnsSetCustomProfile(_settings);
        }

        void RefractionSetting()
        {
            var _settings                                                                  = KWS_WaterSettingsRuntimeLoader._settings;
            if (Refraction.IsRefractionDownsampleAvailable) _settings.RefractionResolution = (WaterQualityLevelSettings.RefractionResolutionEnum)EnumPopup("Resolution", "", _settings.RefractionResolution, link.RefractionResolution);
            _settings.RefractionMode          = (WaterQualityLevelSettings.RefractionModeEnum)EnumPopup("Refraction Mode", Description.Refraction.RefractionMode, _settings.RefractionMode, link.RefractionMode);
            _settings.UseRefractionDispersion = Toggle("Use Dispersion", Description.Refraction.UseRefractionDispersion, _settings.UseRefractionDispersion, link.UseRefractionDispersion);
        }

        void FoamSetting()
        {  
            var _settings = KWS_WaterSettingsRuntimeLoader._settings;
            if (_settings.UseOceanFoam)
            {
                //if (_waterInstance.Settings.CurrentWindSpeed < 7.1f) EditorGUILayout.HelpBox("Foam appears during strong winds (from ~8 meters and above)", MessageType.Info);
            }

        }

        void WetSetting()
        {
           
        }


        void VolumetricLightingSettings()
        {  
            var _settings = KWS_WaterSettingsRuntimeLoader._settings;
            CheckPlatformSpecificMessages_VolumeLight();

            _settings.VolumetricLightResolutionQuality =
                (WaterQualityLevelSettings.VolumetricLightResolutionQualityEnum)EnumPopup("Resolution Quality", Description.VolumetricLight.ResolutionQuality, _settings.VolumetricLightResolutionQuality, link.VolumetricLightResolutionQuality);
            _settings.VolumetricLightIteration = IntSlider("Iterations", Description.VolumetricLight.Iterations, _settings.VolumetricLightIteration, 2, KWS_Settings.VolumetricLighting.MaxIterations, link.VolumetricLightIteration);
            _settings.VolumetricLightTemporalReprojectionAccumulationFactor = Slider("Temporal Accumulation Factor", "", _settings.VolumetricLightTemporalReprojectionAccumulationFactor, 0.1f, 0.75f, link.VolumetricLightTemporalAccumulationFactor);
            _settings.VolumetricLightUseBlur = Toggle("Use Blur", "", _settings.VolumetricLightUseBlur, "");
            if (_settings.VolumetricLightUseBlur) _settings.VolumetricLightBlurRadius = Slider("Blur Radius", "", _settings.VolumetricLightBlurRadius, 1f, 3f, "");
            Line();

            if (_settings.VolumetricLightUseAdditionalLightsCaustic) EditorGUILayout.HelpBox("AdditionalLightsCaustic with multiple light sources can cause dramatic performance drop.", MessageType.Warning);
            _settings.VolumetricLightUseAdditionalLightsCaustic = Toggle("Use Additional Lights Caustic", "", _settings.VolumetricLightUseAdditionalLightsCaustic, link.VolumetricLightUseAdditionalLightsCaustic);
        }

        void CausticSettings()
        {
            var   _settings             = KWS_WaterSettingsRuntimeLoader._settings;
            var   size                  = (int)_settings.CausticTextureResolutionQuality;
            float currentRenderedPixels = size * size;
            currentRenderedPixels *= _settings.UseCausticHighQualityFiltering ? 2 : 1;
            currentRenderedPixels = (currentRenderedPixels / 1000000f);
            EditorGUILayout.LabelField("Simulation rendered pixels (less is better): " + currentRenderedPixels.ToString("0.0") + " millions", KWS_EditorUtils.NotesLabelStyleFade);

            _settings.CausticTextureResolutionQuality = (WaterQualityLevelSettings.CausticTextureResolutionQualityEnum)EnumPopup("Caustic Resolution", "", _settings.CausticTextureResolutionQuality, link.CausticTextureSize);
            _settings.UseCausticHighQualityFiltering  = Toggle("Use High Quality Filtering", "",                                       _settings.UseCausticHighQualityFiltering, link.UseCausticBicubicInterpolation);
            _settings.UseCausticDispersion            = Toggle("Use Dispersion",             Description.Caustic.UseCausticDispersion, _settings.UseCausticDispersion,           link.UseCausticDispersion);
        }

        void UnderwaterSettings()
        {
            var _settings = KWS_WaterSettingsRuntimeLoader._settings;
            _settings.UnderwaterReflectionMode = (WaterQualityLevelSettings.UnderwaterReflectionModeEnum)EnumPopup("Internal Reflection Mode", "", _settings.UnderwaterReflectionMode, link.UnderwaterReflectionMode);

            _settings.UseUnderwaterHalfLineTensionEffect = Toggle("Use Half Line Tension Effect", "", _settings.UseUnderwaterHalfLineTensionEffect, link.UnderwaterHalfLineTensionEffect);
            if (_settings.UseUnderwaterHalfLineTensionEffect) _settings.UnderwaterHalfLineTensionScale = Slider("Tension Scale", "", _settings.UnderwaterHalfLineTensionScale, 0.2f, 1f, link.TensionScale);
            _settings.UseWaterDropsEffect = Toggle("Use Water Drops Effect", "", _settings.UseWaterDropsEffect, link.Default);

            _settings.OverrideUnderwaterTransparent = Toggle("Override Transparent", "", _settings.OverrideUnderwaterTransparent, link.OverrideUnderwaterTransparent);
            if (_settings.OverrideUnderwaterTransparent)
            {
                _settings.UnderwaterTransparentOffset = Slider("Transparent Offset", Description.Color.Transparent, _settings.UnderwaterTransparentOffset, -100, 100, link.Transparent);
            }

        }

        void MeshSettings()
        {
            var _settings = KWS_WaterSettingsRuntimeLoader._settings;
            _settings.WaterMeshDetailing       = (WaterQualityLevelSettings.WaterMeshQualityEnum)EnumPopup("Mesh Detailing", "", _settings.WaterMeshDetailing, link.WaterMeshQualityInfinite);
            _settings.MeshDetailingFarDistance = IntSlider("Mesh Detailing Far Distance", "", _settings.MeshDetailingFarDistance, 500, 5000, link.OceanDetailingFarDistance);

        }

        void RenderingSetting()
        {
            var _settings = KWS_WaterSettingsRuntimeLoader._settings;
            ReadSelectedThirdPartyFog();
            var selectedThirdPartyFogMethod = WaterSystem.ThirdPartyFogAssetsDescriptions[_settingsContainer.SelectedThirdPartyFogMethod];


            if (selectedThirdPartyFogMethod.CustomQueueOffset != 0)
            {
                EditorGUILayout.LabelField($"Min TransparentSortingPriority overrated by {selectedThirdPartyFogMethod.EditorName}", KWS_EditorUtils.NotesLabelStyleFade);
                _settings.WaterTransparentSortingPriority = IntSlider("Transparent Sorting Priority", "", _settings.WaterTransparentSortingPriority, selectedThirdPartyFogMethod.CustomQueueOffset, 50, link.TransparentSortingPriority);
            }
            else
            {
                _settings.WaterTransparentSortingPriority = IntSlider("Transparent Sorting Priority", "", _settings.WaterTransparentSortingPriority, -50, 50, link.TransparentSortingPriority);
            }

            //_settings.EnabledMeshRendering       = Toggle("Enabled Mesh Rendering", "", _settings.EnabledMeshRendering, link.EnabledMeshRendering), false);

            if (selectedThirdPartyFogMethod.DrawToDepth)
            {
                EditorGUILayout.LabelField($"Draw To Depth override by {selectedThirdPartyFogMethod.EditorName}", KWS_EditorUtils.NotesLabelStyleFade);
                GUI.enabled = false;
                _settings.DrawToPosteffectsDepth = Toggle("Draw To Depth", Description.Rendering.DrawToPosteffectsDepth, true, link.DrawToPosteffectsDepth);
                GUI.enabled = true;
            }
            else
            {
                _settings.DrawToPosteffectsDepth = Toggle("Draw To Depth", Description.Rendering.DrawToPosteffectsDepth, _settings.DrawToPosteffectsDepth, link.DrawToPosteffectsDepth);
            }


            _settings.WideAngleCameraRenderingMode = Toggle("Wide-Angle Camera Mode", "", _settings.WideAngleCameraRenderingMode, link.WideAngleCameraRenderingMode);
            //if (_waterSystem.UseTesselation)
            //{
            //    _waterSystem.WireframeMode = false;
            //    EditorGUILayout.LabelField($"Wireframe mode doesn't work with tesselation (water -> mesh -> use tesselation)", KWS_EditorUtils.NotesLabelStyleFade);
            //    GUI.enabled                           = false;
            //    _waterSystem.WireframeMode = Toggle("Wireframe Mode", "", _waterSystem.WireframeMode, nameof(_waterSystem.WireframeMode));
            //    GUI.enabled = _isActive;
            //}
            //else
            //{
            //    _waterSystem.WireframeMode = Toggle("Wireframe Mode", "", _waterSystem.WireframeMode, nameof(_waterSystem.WireframeMode));
            //}

            var assets = WaterSystem.ThirdPartyFogAssetsDescriptions;
            var fogDisplayedNames = new string[assets.Count + 1];
            for (var i = 0; i < assets.Count; i++)
            {
                fogDisplayedNames[i] = assets[i].EditorName;
            }
            EditorGUI.BeginChangeCheck();
            _settingsContainer.SelectedThirdPartyFogMethod = EditorGUILayout.Popup("Third-Party Fog Support", _settingsContainer.SelectedThirdPartyFogMethod, fogDisplayedNames);
            if (EditorGUI.EndChangeCheck())
            {
                UpdateThirdPartyFog();
            }
            if (_settingsContainer.SelectedThirdPartyFogMethod != 0 && !_isThirdPartyFogAvailable)
            {
                EditorGUILayout.HelpBox($"Can't find the asset {WaterSystem.ThirdPartyFogAssetsDescriptions[_settingsContainer.SelectedThirdPartyFogMethod].EditorName}", MessageType.Error);
            }
#if KWS_DEBUG
            Line();

            //if (_settings.WaterMeshType == WaterMeshTypeEnum.InfiniteOcean || _settings.WaterMeshType == WaterMeshTypeEnum.FiniteBox)
            //{
            //    WaterSystem.DebugQuadtree = Toggle("Debug Quadtree", "", WaterSystem.DebugQuadtree, "");
            //}
            //_waterInstance.DebugAABB = Toggle("Debug AABB", "", _waterInstance.DebugAABB, "");
            //_waterInstance.DebugFft = Toggle("Debug Fft", "", _waterInstance.DebugFft, "");
            //_waterInstance.DebugDynamicWaves = Toggle("Debug Dynamic Waves", "", _waterInstance.DebugDynamicWaves, "");
            //_waterInstance.DebugOrthoDepth = Toggle("Debug Ortho Depth", "", _waterInstance.DebugOrthoDepth, "");
            //_waterInstance.DebugBuoyancy = Toggle("Debug Buoyancy", "", _waterInstance.DebugBuoyancy, "");
            //WaterSystem.DebugUpdateManager = Toggle("Debug Update Manager", "", WaterSystem.DebugUpdateManager, "");
            //Line();
#endif

        }

        void ReadSelectedThirdPartyFog()
        {
            //load enabled third-party asset for all water instances
            if (_settingsContainer.SelectedThirdPartyFogMethod == -1)
            {
                var defines = WaterSystem.ThirdPartyFogAssetsDescriptions.Select(n => n.ShaderDefine).ToList<string>();
                _settingsContainer.SelectedThirdPartyFogMethod = KWS_EditorUtils.GetEnabledDefineIndex(ShaderPaths.KWS_PlatformSpecificHelpers, defines);
            }

        }

        void UpdateThirdPartyFog()
        {  
            var _settings = KWS_WaterSettingsRuntimeLoader._settings;
            if (_settingsContainer.SelectedThirdPartyFogMethod > 0)
            {
                var selectedMethod = WaterSystem.ThirdPartyFogAssetsDescriptions[_settingsContainer.SelectedThirdPartyFogMethod];
                if (!selectedMethod.IgnoreInclude)
                {
                    var inlcudeFileName = KW_Extensions.GetAssetsRelativePathToFile(selectedMethod.ShaderInclude, selectedMethod.AssetNameSearchPattern);
                    if (String.IsNullOrEmpty(inlcudeFileName))
                    {
                        _isThirdPartyFogAvailable = false;
                        Debug.LogError($"Can't find the asset {WaterSystem.ThirdPartyFogAssetsDescriptions[_settingsContainer.SelectedThirdPartyFogMethod].EditorName}");
                        return;
                    }
                    else _isThirdPartyFogAvailable = true;
                }
            }

            //replace defines
            for (int i = 1; i < WaterSystem.ThirdPartyFogAssetsDescriptions.Count; i++)
            {
                var selectedMethod = WaterSystem.ThirdPartyFogAssetsDescriptions[i];
                SetShaderTextDefine(ShaderPaths.KWS_PlatformSpecificHelpers, false, selectedMethod.ShaderDefine, _settingsContainer.SelectedThirdPartyFogMethod == i);
            }

            //replace paths to assets
            if (_settingsContainer.SelectedThirdPartyFogMethod > 0)
            {
                var selectedMethod = WaterSystem.ThirdPartyFogAssetsDescriptions[_settingsContainer.SelectedThirdPartyFogMethod];
                if (!selectedMethod.IgnoreInclude)
                {
                    var inlcudeFileName = KW_Extensions.GetAssetsRelativePathToFile(selectedMethod.ShaderInclude, selectedMethod.AssetNameSearchPattern);
                    KWS_EditorUtils.ChangeShaderTextIncludePath(KWS_Settings.ShaderPaths.KWS_PlatformSpecificHelpers, selectedMethod.ShaderDefine, inlcudeFileName);
                }
            }
        
            var thirdPartySelectedFog = WaterSystem.ThirdPartyFogAssetsDescriptions[_settingsContainer.SelectedThirdPartyFogMethod];
            if (thirdPartySelectedFog.DrawToDepth) _settings.DrawToPosteffectsDepth = true;

            AssetDatabase.Refresh();

            if (WaterSystem.Instance)
            {
                WaterSystem.Instance.gameObject.SetActive(false);
                WaterSystem.Instance.gameObject.SetActive(true);
            }
        }

      
        void CheckPlatformSpecificMessages_Reflection()
        {  
            var _settings = KWS_WaterSettingsRuntimeLoader._settings;
            //if (_waterInstance.Settings.ReflectSun)
            //{
            //    if (KWS_WaterLights.Lights.Count == 0 || KWS_WaterLights.Lights.Count(l => l.Light.type == LightType.Directional) == 0)
            //    {
            //        EditorGUILayout.HelpBox("'Water->Reflection->Reflect Sunlight' doesn't work because no directional light has been added for water rendering! Add the script 'AddLightToWaterRendering' to your directional light!", MessageType.Error);
            //    }
            //}

            #if KWS_BUILTIN || KWS_URP
                if (ReflectionProbe.defaultTexture.width == 1 && _settings.OverrideSkyColor == false)
                {
                    EditorGUILayout.HelpBox("Sky reflection doesn't work in this scene, you need to generate scene lighting! " + Environment.NewLine +
                                            "Open the \"Lighting\" window -> select the Generate Lighting option Reflection Probes", MessageType.Error);
                }
            #endif
        }

        void CheckPlatformSpecificMessages_VolumeLight()
        {
            //if (_waterInstance.Settings.UseVolumetricLight && KWS_WaterLights.Lights.Count == 0) EditorGUILayout.HelpBox("Water->'Volumetric lighting' doesn't work because no lights has been added for water rendering! Add the script 'AddLightToWaterRendering' to your light.", MessageType.Error);
        }
    }
#endif
}