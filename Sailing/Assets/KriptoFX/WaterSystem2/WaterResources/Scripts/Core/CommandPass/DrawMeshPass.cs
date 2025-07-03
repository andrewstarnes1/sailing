using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static KWS.KWS_ShaderConstants;

namespace KWS
{
    internal class DrawMeshPass : WaterPass
    {
        internal override string PassName => "Water.DrawMeshPass";
        private RenderParams _renderParams = new RenderParams();
        Material _drawMeshMaterial;

        public DrawMeshPass()
        {
           _drawMeshMaterial = KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.WaterShaderName, useWaterStencilMask: true);
        }

        public override void Release()
        {
            KW_Extensions.SafeDestroy(_drawMeshMaterial);
            _drawMeshMaterial = null;
            //KW_Extensions.WaterLog(this, "Release", KW_Extensions.WaterLogMessageType.Release);
        }


        public override void ExecuteBeforeCameraRendering(Camera cam, ScriptableRenderContext context)
        {
            if (cam == null || _drawMeshMaterial == null) return;
           
            _renderParams.camera               = cam;
            _renderParams.material             = _drawMeshMaterial;
            _renderParams.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
            _renderParams.worldBounds          = WaterSystem.Instance.WorldSpaceBounds;
            _renderParams.renderingLayerMask   = GraphicsSettings.defaultRenderingLayerMask;
            _renderParams.layer                = KWS_Settings.Water.WaterLayer;
           
            DrawInstancedQuadTree(cam, WaterSystem.Instance, _drawMeshMaterial, false);
        }

      
        public void DrawInstancedQuadTree(Camera cam, WaterSystem waterInstance, Material mat, bool isPrePass)
        {
            waterInstance._meshQuadTree.UpdateQuadTree(cam, waterInstance, forceUpdate:false);
            var isFastMode = isPrePass && !WaterSystem.IsCameraPartialUnderwater;
            if (!waterInstance._meshQuadTree.TryGetRenderingContext(cam, isFastMode, out var context)) return;

            mat.SetBuffer(StructuredBuffers.InstancedMeshData, context.visibleChunksComputeBuffer);

            if (_renderParams.camera == null || _renderParams.material == null || context.chunkInstance == null || context.visibleChunksArgs == null || context.visibleChunksArgs.count == 0)
            {
                Debug.LogError($"Water draw mesh rendering error: {_renderParams.camera}, { _renderParams.material}, {context.chunkInstance}, {context.visibleChunksArgs}");
                return;
            }

            Graphics.RenderMeshIndirect(_renderParams, context.chunkInstance, context.visibleChunksArgs);
        }
        
    }
}