using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace KWS
{
    internal class ScreenSpaceReflectionPass : WaterPass
    {
        internal override string PassName => "Water.ScreenSpaceReflectionPass";

        Dictionary<Camera, SsrData> _ssrDatas = new Dictionary<Camera, SsrData>();

        Material _anisoMaterial;
        private Material _ssrMaterial;
        ComputeShader _cs;


        int _kernelClear;
        int _kernelRenderHash;
        int _kernelRenderColorFromHash;


        private const int MaxSsrDataCameras = 5;
        const int shaderNumthreadX = 8;
        const int shaderNumthreadY = 8;


        public ScreenSpaceReflectionPass()
        {
            _ssrMaterial                                   =  KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.SsrShaderName);
            WaterSystem.OnAnyWaterSettingsChanged += OnAnyWaterSettingsChanged;
        }

        public override void Release()
        {
            WaterSystem.OnAnyWaterSettingsChanged -= OnAnyWaterSettingsChanged;
            _ssrDatas.ReleaseCameraCache();

            KW_Extensions.SafeDestroy(_cs, _anisoMaterial, _ssrMaterial);
        }

        private void OnAnyWaterSettingsChanged(WaterSystem.WaterSettingsCategory waterTab)
        {
            if (waterTab.HasTab(WaterSystem.WaterSettingsCategory.Reflection))
            {
                _ssrDatas.ReleaseCameraCache();
            }
        }

       
        internal void InitializeShaders()
        {
            _cs = KWS_CoreUtils.LoadComputeShader(KWS_ShaderConstants.ShaderNames.SsrComputePath);
            //_cs = (ComputeShader)Resources.Load(KWS_ShaderConstants.ShaderNames.SsrComputePath);
            if (_cs != null)
            {
                _kernelClear = _cs.FindKernel("Clear_kernel");
                _kernelRenderHash = _cs.FindKernel("RenderHash_kernel");
                _kernelRenderColorFromHash = _cs.FindKernel("RenderColorFromHash_kernel");
            }
        }

        internal static Vector2Int ScaleFunc(Vector2Int size)
        {
            float scale = (int)WaterSystem.QualitySettings.ScreenSpaceReflectionResolutionQuality / 100f;
            return new Vector2Int(Mathf.RoundToInt(scale * size.x), Mathf.RoundToInt(scale * size.y));
        }


        internal class SsrData : KW_Extensions.ICacheCamera
        {
            public RTHandle[] ReflectionRT = new RTHandle[2];
            public ComputeBuffer HashBuffer;


            public int Frame;

            public Matrix4x4 PrevVPMatrix;
            public Matrix4x4[] PrevVPMatrixStereo = new Matrix4x4[2];


            public Vector2Int GetCurrentResolution()
            {
                var scale = ReflectionRT[0].rtHandleProperties.rtHandleScale;
                return new Vector2Int(Mathf.RoundToInt(ReflectionRT[0].rt.width * scale.x), Mathf.RoundToInt(ReflectionRT[0].rt.height * scale.y));
            }

            public void InitializeHashBuffer(Vector2Int resolution)
            {
                var size = resolution.x * resolution.y;
                if (KWS_CoreUtils.SinglePassStereoEnabled) size *= 2;
                HashBuffer = KWS_CoreUtils.GetOrUpdateBuffer<uint>(ref HashBuffer, size);
            }


            internal void InitializeTextures()
            {
                var colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                for (int idx = 0; idx < 2; idx++)
                {
                    ReflectionRT[idx] = KWS_CoreUtils.RTHandleAllocVR(ScaleFunc, name: "_ssrReflectionRT" + idx, colorFormat: colorFormat, enableRandomWrite: true, useMipMap: true, autoGenerateMips: false, mipMapCount: 5);
                }


                this.WaterLog(ReflectionRT[0]);
            }

            public void ReleaseTextures()
            {
                ReflectionRT[0]?.Release();
                ReflectionRT[1]?.Release();

                ReflectionRT[0] = ReflectionRT[1] = null;

            }

            public void Release()
            {
                ReleaseTextures();

                HashBuffer?.Release();
                HashBuffer = null;

                this.WaterLog("", KW_Extensions.WaterLogMessageType.Release);
            }
        }

        public override void ExecuteCommandBuffer(WaterPass.WaterPassContext waterContext)
        {
            ExecuteRaymarchedReflection(waterContext);
            //ExecutePlanarRaymarchedReflection(waterContext); 
        }

        public void ExecuteRaymarchedReflection(WaterPass.WaterPassContext waterContext)
        {
            if (!WaterSystem.QualitySettings.UseScreenSpaceReflection) return;

            var cam = waterContext.cam;
            var cmd = waterContext.cmd;

            var data = _ssrDatas.GetCameraCache(cam);

            if (data.ReflectionRT[0] == null) data.InitializeTextures();

            var targetRT     = data.Frame % 2 == 0 ? data.ReflectionRT[0] : data.ReflectionRT[1];
            var lastTargetRT = data.Frame % 2 == 0 ? data.ReflectionRT[1] : data.ReflectionRT[0];


            cmd.SetGlobalInt("KWS_Frame", data.Frame);
          

            cmd.BlitTriangleRTHandle(lastTargetRT, targetRT, _ssrMaterial, ClearFlag.All, Color.clear, pass: 0);

            WaterSharedResources.SsrReflection = targetRT;
            WaterSharedResources.SsrReflectionCurrentResolution = data.GetCurrentResolution();

            cmd.SetGlobalTexture(KWS_ShaderConstants.SSR_ID.KWS_ScreenSpaceReflectionRT, WaterSharedResources.SsrReflection);
            cmd.SetGlobalVector(KWS_ShaderConstants.SSR_ID.KWS_ScreenSpaceReflection_RTHandleScale, WaterSharedResources.SsrReflection.rtHandleProperties.rtHandleScale);
            //WaterSharedResources.SsrReflectionRaw = targetRT;
            data.Frame++;
        }
        
    }
}