using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static KWS.KWS_CoreUtils;
using static KWS.KWS_ShaderConstants;

namespace KWS
{
    internal class WaterPrePass : WaterPass
    {
        internal override string  PassName => "Water.PrePass";

        readonly          Vector2 _rtScale         = new Vector2(0.5f,  0.5f);
        readonly          Vector2 _rtScaleTension  = new Vector2(0.35f, 0.35f);
        readonly          Vector2 _rtScaleAquarium = new Vector2(0.5f,  0.5f);

        private  KW_PyramidBlur _pyramidBlur = new KW_PyramidBlur();
        private  RTHandle       _tempIntersectionTensionRT;
        internal Material       _prePassMaterial;

        public WaterPrePass()
        {
            _prePassMaterial = KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.WaterPrePassShaderName, useWaterStencilMask: true);
        }

        void InitializePrepassTextures()
        {
            if (WaterSharedResources.WaterPrePassRT0 != null) return;

            WaterSharedResources.WaterPrePassRT0 = KWS_CoreUtils.RTHandleAllocVR(_rtScale, name: "_waterPrePassRT0", colorFormat: GraphicsFormat.R8G8B8A8_UNorm);
            WaterSharedResources.WaterPrePassRT1 = KWS_CoreUtils.RTHandleAllocVR(_rtScale, name: "_waterPrePassRT1", colorFormat: GraphicsFormat.R16G16_SNorm);
            WaterSharedResources.WaterDepthRT    = KWS_CoreUtils.RTHandleAllocVR(_rtScale, name: "_waterDepthRT",    depthBufferBits: DepthBits.Depth24);
           
            Shader.SetGlobalTexture(MaskPassID.KWS_WaterPrePassRT0, WaterSharedResources.WaterPrePassRT0);
            Shader.SetGlobalTexture(MaskPassID.KWS_WaterPrePassRT1, WaterSharedResources.WaterPrePassRT1);
            Shader.SetGlobalTexture(MaskPassID.KWS_WaterDepthRT,    WaterSharedResources.WaterDepthRT);

            this.WaterLog(WaterSharedResources.WaterPrePassRT0, WaterSharedResources.WaterPrePassRT1, WaterSharedResources.WaterDepthRT);
        }

        void InitializeIntersectionHalflineTensionTextures()
        {
            if (WaterSharedResources.WaterIntersectionHalfLineTensionMaskRT != null) return;

            WaterSharedResources.WaterIntersectionHalfLineTensionMaskRT = KWS_CoreUtils.RTHandleAllocVR(_rtScaleTension, name: "_waterIntersectionHalfLineTensionMaskRT", colorFormat: GraphicsFormat.R8_UNorm);
            _tempIntersectionTensionRT                                  = KWS_CoreUtils.RTHandleAllocVR(_rtScaleTension, name: "_tempIntersectionTensionRT",              colorFormat: GraphicsFormat.R8_UNorm);
            Shader.SetGlobalTexture(MaskPassID.KWS_WaterIntersectionHalfLineTensionMaskRT, WaterSharedResources.WaterIntersectionHalfLineTensionMaskRT);
        }


        void InitializeBackfaceTextures()
        {
            if (WaterSharedResources.WaterBackfacePrePassRT0 != null) return;

            WaterSharedResources.WaterBackfacePrePassRT0 = KWS_CoreUtils.RTHandleAllocVR(_rtScaleAquarium, name: "_waterAquariumBackfacePrePassRT0", colorFormat: GraphicsFormat.R8G8B8A8_UNorm);
            WaterSharedResources.WaterBackfacePrePassRT1 = KWS_CoreUtils.RTHandleAllocVR(_rtScaleAquarium, name: "_waterAquariumBackfacePrePassRT1", colorFormat: GraphicsFormat.R16G16_SNorm);
            WaterSharedResources.WaterBackfaceDepthRT = KWS_CoreUtils.RTHandleAllocVR(_rtScaleAquarium, name: "_waterAquariumBackfaceDepthRT", depthBufferBits: DepthBits.Depth24);

            Shader.SetGlobalTexture(MaskPassID.KWS_WaterBackfacePrePassRT0, WaterSharedResources.WaterBackfacePrePassRT0);
            Shader.SetGlobalTexture(MaskPassID.KWS_WaterBackfacePrePassRT1, WaterSharedResources.WaterBackfacePrePassRT1);
            Shader.SetGlobalTexture(MaskPassID.KWS_WaterBackfaceDepthRT,    WaterSharedResources.WaterBackfaceDepthRT);



            this.WaterLog(WaterSharedResources.WaterBackfacePrePassRT0, WaterSharedResources.WaterBackfacePrePassRT1, WaterSharedResources.WaterBackfaceDepthRT);
        }

        void ReleaseTextures()
        {
            WaterSharedResources.WaterPrePassRT0?.Release();
            WaterSharedResources.WaterPrePassRT1?.Release();
            WaterSharedResources.WaterDepthRT?.Release();
            WaterSharedResources.WaterIntersectionHalfLineTensionMaskRT?.Release();

            WaterSharedResources.WaterBackfacePrePassRT0?.Release();
            WaterSharedResources.WaterBackfacePrePassRT1?.Release();
            WaterSharedResources.WaterBackfaceDepthRT?.Release();

            WaterSharedResources.WaterPrePassRT0                 = WaterSharedResources.WaterPrePassRT1                 = WaterSharedResources.WaterDepthRT = WaterSharedResources.WaterIntersectionHalfLineTensionMaskRT = null;
            WaterSharedResources.WaterBackfacePrePassRT0 = WaterSharedResources.WaterBackfacePrePassRT1 = WaterSharedResources.WaterBackfaceDepthRT = null;

            _tempIntersectionTensionRT?.Release();
            _tempIntersectionTensionRT = null;

            this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.ReleaseRT);
        }

        public override void Release()
        {
            ReleaseTextures();
            _pyramidBlur.Release();
            KW_Extensions.SafeDestroy(_prePassMaterial);
            _prePassMaterial = null;

            this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.Release);
        }

        public override void ExecuteCommandBuffer(WaterPass.WaterPassContext waterContext)
        {
            if (_prePassMaterial == null) return;

            ExecutePrepass(waterContext);
           
            //if (WaterSharedResources.IsAnyAquariumWaterVisible) ExecuteBackfacePrepass(waterContext);
        }

        void ExecutePrepass(WaterPass.WaterPassContext waterContext)
        {
            if (!IsWaterVisibleAndActive()) return;

            var useIntersectionHalfline = WaterSystem.QualitySettings.UseUnderwaterEffect && WaterSystem.QualitySettings.UseUnderwaterHalfLineTensionEffect        && WaterSystem.IsCameraPartialUnderwater;
            var useOceanUnderwater      = WaterSystem.QualitySettings.UseUnderwaterEffect && WaterSystem.IsCameraPartialUnderwater;
           
            InitializePrepassTextures();
            if (useIntersectionHalfline) InitializeIntersectionHalflineTensionTextures();
            waterContext.cmd.SetGlobalVector(MaskPassID.KWS_WaterPrePass_RTHandleScale, WaterSharedResources.WaterPrePassRT0.rtHandleProperties.rtHandleScale);

            CoreUtils.SetRenderTarget(waterContext.cmd, KWS_CoreUtils.GetMrt(WaterSharedResources.WaterPrePassRT0, WaterSharedResources.WaterPrePassRT1), WaterSharedResources.WaterDepthRT, 
                                      ClearFlag.All, Color.clear);
            //draw far plane volume underwater mask for the ocean
            if (useOceanUnderwater)
            {
                waterContext.cmd.SetGlobalFloat(PrePass.KWS_OceanLevel, WaterSystem.Instance.WaterPivotWorldPosition.y);
                CoreUtils.SetRenderTarget(waterContext.cmd, WaterSharedResources.WaterPrePassRT0, ClearFlag.None, Color.clear);
                waterContext.cmd.BlitTriangle(_prePassMaterial, pass: 2);
            }


            CoreUtils.SetRenderTarget(waterContext.cmd, KWS_CoreUtils.GetMrt(WaterSharedResources.WaterPrePassRT0, WaterSharedResources.WaterPrePassRT1), WaterSharedResources.WaterDepthRT, 
                                      ClearFlag.None, Color.clear);
            ExecuteInstance(waterContext.cam, waterContext.cmd, isBackface: false);
            

            if (useIntersectionHalfline)
            {
                waterContext.cmd.BlitTriangleRTHandle(WaterSharedResources.WaterPrePassRT0, _tempIntersectionTensionRT, _prePassMaterial, ClearFlag.Color, Color.clear, pass: 3);
                var scale = WaterSystem.QualitySettings.UnderwaterHalfLineTensionScale;
                scale = Mathf.Lerp(1f, 3f, scale);
                _pyramidBlur.ComputeBlurPyramid(scale, _tempIntersectionTensionRT, WaterSharedResources.WaterIntersectionHalfLineTensionMaskRT, waterContext.cmd, _rtScale);
            }
        }


        void ExecuteBackfacePrepass(WaterPass.WaterPassContext waterContext)
        {
            InitializeBackfaceTextures();
            waterContext.cmd.SetGlobalVector(MaskPassID.KWS_WaterBackfacePrePass_RTHandleScale, WaterSharedResources.WaterBackfacePrePassRT0.rtHandleProperties.rtHandleScale);


            CoreUtils.SetRenderTarget(waterContext.cmd, KWS_CoreUtils.GetMrt(WaterSharedResources.WaterBackfacePrePassRT0, WaterSharedResources.WaterBackfacePrePassRT1), WaterSharedResources.WaterBackfaceDepthRT, 
                                      ClearFlag.All, Color.clear);
         
            ExecuteInstance(waterContext.cam, waterContext.cmd, isBackface: true);
        }


        void ExecuteInstance(Camera cam, CommandBuffer cmd, bool isBackface)
        {
            var shaderPass             = 0;
            if (isBackface) shaderPass += 1;
            DrawInstancedQuadTree(cam, cmd, _prePassMaterial, shaderPass);
            // DrawCustomMesh(cmd, waterInstance, mat, shaderPass);
        }

        public static void DrawInstancedQuadTree(Camera cam, CommandBuffer cmd,  Material mat, int shaderPass)
        {
            //var isFastMode = !waterInstance.IsCameraUnderwaterForInstance;
            var isFastMode = false;
            if (!WaterSystem.Instance._meshQuadTree.TryGetRenderingContext(cam, isFastMode, out var context)) return;
           
         if (context.chunkInstance == null || mat == null || context.visibleChunksArgs == null)
            {
                Debug.LogError($"Water PrePass.DrawInstancedQuadTree error: {context.chunkInstance}, { mat},  { context.visibleChunksArgs}");
                return;
            }

            cmd.SetGlobalBuffer(StructuredBuffers.InstancedMeshData, context.visibleChunksComputeBuffer);
            cmd.DrawMeshInstancedIndirect(context.chunkInstance, 0, mat, shaderPass, context.visibleChunksArgs);
        }

    }
}