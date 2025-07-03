using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace KWS
{
    internal class MotionVectorsPass : WaterPass
    {
        internal override string PassName => "Water.MotionVectorsPass";

        public MotionVectorsPass()
        {
            WaterSystem.OnAnyWaterSettingsChanged += OnAnyWaterSettingsChanged;
           
        }

        void InitializeTextures()
        {
           
            //this.WaterLog(WaterSharedResources.CausticRTArray);
        }

        void ReleaseTextures()
        {
           
            this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.ReleaseRT);
        }


        public override void Release()
        {
            WaterSystem.OnAnyWaterSettingsChanged -= OnAnyWaterSettingsChanged;
            ReleaseTextures();

            this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.Release);
        }

        private void OnAnyWaterSettingsChanged(WaterSystem.WaterSettingsCategory changedTabs)
        {
            //if (changedTabs.HasFlag(WaterSystem.WaterTab.Caustic))
            {
                
            }
        }

        public override void ExecuteCommandBuffer(WaterPass.WaterPassContext waterContext)
        {
           
        }

    }
}