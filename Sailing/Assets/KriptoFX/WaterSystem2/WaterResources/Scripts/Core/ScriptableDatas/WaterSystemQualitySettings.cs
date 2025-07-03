using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KWS
{
    public class WaterSystemQualitySettings : ScriptableObject
    {
        public List<WaterQualityLevelSettings> qualityLevelSettings = new List<WaterQualityLevelSettings>();



        public bool ShowExpertReflectionSettings;

        public bool ShowReflectionSettings;
        public bool ShowRefractionSettings       = false;
        public bool ShowFoamSettings             = false;
        public bool ShowWetSettings  = false;
        public bool ShowVolumetricLightSettings  = false;
        public bool ShowCausticEffectSettings    = false;
        public bool ShowUnderwaterEffectSettings = false;
        public bool ShowMeshSettings             = false;
        public bool ShowRendering                = false;

        public int SelectedThirdPartyFogMethod = 0;
        
        [Serializable]
        internal class ThirdPartyAssetDescription
        {
            public string EditorName;
            public string ShaderDefine           = String.Empty;
            public string ShaderInclude          = String.Empty;
            public string AssetNameSearchPattern = String.Empty;
            public bool   DrawToDepth;
            public int    CustomQueueOffset;
            public bool   IgnoreInclude;
            public bool   OverrideNativeCubemap;
        }

    }
}