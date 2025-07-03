#if UNITY_EDITOR
using System;
using UnityEditor;
using System.Linq;
using UnityEngine;

namespace KWS
{
    internal partial class KWS_Editor
    {
        void CheckPlatformSpecificMessages()
        {
            CheckPlatformSpecificMessages_VolumeLight();
            CheckPlatformSpecificMessages_Reflection();
        }

        void CheckPlatformSpecificMessages_VolumeLight()
        {
            #if !KWS_HDRP && !KWS_URP
                if (WaterSystem.QualitySettings.UseVolumetricLight) EditorGUILayout.HelpBox("Add the \"AddLightToWaterRendering\" script to the light source to ensure it works correctly with the volumetric lighting.", MessageType.Info);
            #endif
        }

        void CheckPlatformSpecificMessages_Reflection()
        {

#if !KWS_HDRP
            if (ReflectionProbe.defaultTexture.width == 1 && WaterSystem.QualitySettings.OverrideSkyColor == false)
            {
                EditorGUILayout.HelpBox("Sky reflection doesn't work in this scene, you need to generate scene lighting! " + Environment.NewLine +
                                        "Open the \"Lighting\" window -> select the Generate Lighting option Reflection Probes", MessageType.Error);
            }
#endif
        }
    }

}
#endif