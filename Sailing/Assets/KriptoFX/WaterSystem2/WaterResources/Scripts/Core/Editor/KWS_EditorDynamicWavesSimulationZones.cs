#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static KWS.KWS_EditorUtils;
using Description = KWS.KWS_EditorTextDescription;
using link = KWS.KWS_EditorUrlLinks;

namespace KWS
{
    [CustomEditor(typeof(KWS_DynamicWavesSimulationZone))]
    internal class KWS_EditorDynamicWavesSimulationZones : Editor
    {
        private KWS_DynamicWavesSimulationZone _target;
        public override void OnInspectorGUI()
        {
            _target = (KWS_DynamicWavesSimulationZone)target;
            
            Undo.RecordObject(_target, "Changed Dynamic Waves Simulation Zone");

           // EditorGUI.BeginChangeCheck();
           // script.IsBakeMode = GUILayout.Toggle(script.IsBakeMode, "Bake Simulation", "Button");
           // if (EditorGUI.EndChangeCheck())
          //  {
          //      script.ChangeSimulationState();
          //  }
           
           // if(isChanged) script.ValueChanged();
            
            EditorGUI.BeginChangeCheck();
            EditorGUIUtility.labelWidth = 220;
          
            bool defaultVal       = false;
            EditorGUILayout.Space(20);  
          
            KWS2_Tab(ref _target.ShowSimulationSettings, false, false, ref defaultVal, "Simulation Settings", SimulationSettings, WaterSystem.WaterSettingsCategory.SimulationZone, foldoutSpace: 14);
            KWS2_TabWithEnabledToogle(ref _target.UseFoamParticles, ref _target.ShowFoamParticlesSettings, useExpertButton: false, ref defaultVal, "Foam Particles", FoamParticlesSettings, WaterSystem.WaterSettingsCategory.SimulationZone, foldoutSpace: 14);
            KWS2_TabWithEnabledToogle(ref _target.UseSplashParticles, ref _target.ShowFoamParticlesSettings, useExpertButton: false, ref defaultVal, "Splash Particles", SplashParticlesSettings, WaterSystem.WaterSettingsCategory.SimulationZone, foldoutSpace: 14);

            if(_target.ZoneType != KWS_DynamicWavesSimulationZone.SimulationZoneTypeMode.MovableZone) BakeSettings();

            if (EditorGUI.EndChangeCheck())
            {
                _target.ValueChanged();
                EditorUtility.SetDirty(_target);
                // AssetDatabase.SaveAssets();
            }

        }

        private void BakeSettings()
        {
            EditorGUI.BeginChangeCheck();
            _target.IsBakeMode = GUILayout.Toggle(_target.IsBakeMode, "Precompute Start", "Button");
           
            if (EditorGUI.EndChangeCheck())
            {
                if (_target.IsBakeMode == true)
                {
                    if (EditorUtility.DisplayDialog("Start Precomputation?", "This will overwrite the existing simulation cache. Do you want to continue?",
                                                    "Start", "Cancel"))
                    {
                        _target.ChangeSimulationState();
                    }
                    else _target.IsBakeMode = false;
                }
                else _target.ChangeSimulationState();
                
            }
            
            EditorGUI.BeginChangeCheck();
            GUILayout.Toggle(false, "Clear Precomputed Cache", "Button");
            if (EditorGUI.EndChangeCheck())
            {
                if (EditorUtility.DisplayDialog("Confirm Deletion",
                                                "Are you sure you want to delete the precomputed cache textures?",
                                                "Yes",
                                                "Cancel"))
                {
                    _target.ClearSimulationCache();
                    _target.ChangeSimulationState();
                }
                
            }
        }

        void SimulationSettings()
        {
            var isBakedSim = _target.SavedDepth != null;

            if (isBakedSim)
            {
                GUI.enabled = false;
                EditorGUILayout.HelpBox("You can't change some parameters of a precomputed simulation. " + Environment.NewLine +
                                        "Clear the simulation or recompute it again with new parameters", MessageType.Info);
            }
            _target.ZoneType = (KWS_DynamicWavesSimulationZone.SimulationZoneTypeMode)EnumPopup("Zone Type", "", _target.ZoneType, "");
            if (_target.ZoneType == KWS_DynamicWavesSimulationZone.SimulationZoneTypeMode.MovableZone)
            {
                _target.FollowObject = (GameObject)EditorGUILayout.ObjectField(_target.FollowObject, typeof(GameObject), true);
            }
            
            var layerNames = new List<string>();
            for (int i = 0; i <= 31; i++)
            {
                var maskName = LayerMask.LayerToName(i);
                if(maskName != String.Empty) layerNames.Add(maskName);
            }
            _target.IntersectionLayerMask        = MaskField("Intersection Layer Mask", "", _target.IntersectionLayerMask, layerNames.ToArray(), "");
            _target.SimulationResolutionPerMeter = Slider("Simulation Resolution Per Meter", "", _target.SimulationResolutionPerMeter, 2, 3, "", false);
            
            if (isBakedSim)
            {
                GUI.enabled = true;
            }
            
            _target.FlowSpeedMultiplier = Slider("Flow Speed Multiplier", "", _target.FlowSpeedMultiplier, 0.5f, 1.5f, "", false);
            
            Line();
            _target.FoamStrengthRiver = Slider("Foam Strength River", "", _target.FoamStrengthRiver, 0.001f, 0.5f, "", false);
            _target.FoamStrengthShoreline = Slider("Foam Strength Shoreline", "", _target.FoamStrengthShoreline, 0.001f, 0.5f, "", false);
        }
        
        void FoamParticlesSettings()
        {
           _target.MaxFoamParticlesBudget = (KWS_DynamicWavesSimulationZone.FoamParticlesMaxLimitEnum)EnumPopup("Max Particles Budget", "", _target.MaxFoamParticlesBudget, "");
           _target.FoamParticlesScale = Slider("Particles Scale", "", _target.FoamParticlesScale, 0f, 1f, "", false);
           _target.FoamParticlesAlphaMultiplier = Slider("Particles Alpha Multiplier", "", _target.FoamParticlesAlphaMultiplier, 0f, 1f, "", false);
           _target.RiverEmissionRateFoam = Slider("River Emission Rate", "", _target.RiverEmissionRateFoam, 0f, 1f, "", false);
           _target.ShorelineEmissionRateFoam = Slider("Shoreline Emission Rate", "", _target.ShorelineEmissionRateFoam, 0f, 1f, "", false);
           _target.UsePhytoplanktonEmission = Toggle("Use Phytoplankton Emission", "", _target.UsePhytoplanktonEmission, "", false);
        }
        
        void SplashParticlesSettings()
        {
            _target.MaxSplashParticlesBudget       = (KWS_DynamicWavesSimulationZone.SplashParticlesMaxLimitEnum)EnumPopup("Max Particles Budget", "", _target.MaxSplashParticlesBudget, "");
            _target.SplashParticlesScale           = Slider("Particles Scale",            "", _target.SplashParticlesScale,           0f, 1f, "", false);
            _target.SplashParticlesAlphaMultiplier = Slider("Particles Alpha Multiplier", "", _target.SplashParticlesAlphaMultiplier, 0f, 1f, "", false);
            _target.RiverEmissionRateSplash        = Slider("River Emission Rate",        "", _target.RiverEmissionRateSplash,        0f, 1f, "", false);
            _target.ShorelineEmissionRateSplash    = Slider("Shoreline Emission Rate",    "", _target.ShorelineEmissionRateSplash,    0f, 1f, "", false);
            _target.WaterfallEmissionRateSplash    = Slider("Waterfall Emission Rate",    "", _target.WaterfallEmissionRateSplash,    0f, 1f, "", false);
            
            Line();
            _target.ReceiveShadowMode = (KWS_DynamicWavesSimulationZone.SplashReceiveShadowModeEnum)EnumPopup("Receive Shadow Mode", "", _target.ReceiveShadowMode, "");
            _target.CastShadowMode    = (KWS_DynamicWavesSimulationZone.SplashCasticShadowModeEnum)EnumPopup("Cast Shadow Mode",     "", _target.CastShadowMode, "");
        }
        
        void HeightSettings()
        {
          
        }

    }
}

#endif