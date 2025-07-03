#if !KWS_URP && !KWS_HDRP

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace KWS
{
    internal abstract class WaterPass
    {
        internal struct WaterPassContext
        {
            public Camera                 cam;
            public CommandBuffer          cmd;
            public RenderTargetIdentifier cameraDepth;

            public RenderTargetIdentifier cameraColor;

            //public int RequiredFixedUpdateCount;
            public CustomFixedUpdates FixedUpdates;
        }

        private CommandBuffer _commandBuffer;
        internal CameraEvent   cameraEvent = CameraEvent.BeforeForwardAlpha;


        public void ExecuteInjectionPointPass(WaterPassContext waterContext, bool useStereoTarget = false)
        {
            if (_commandBuffer == null) _commandBuffer = new CommandBuffer() { name = PassName };
            _commandBuffer.Clear();
            if (useStereoTarget && KWS_CoreUtils.SinglePassStereoEnabled) CoreUtils.SetRenderTarget(_commandBuffer, BuiltinRenderTextureType.CurrentActive);

            waterContext.cmd = _commandBuffer;
            ExecuteCommandBuffer(waterContext);

            waterContext.cam.AddCommandBuffer(cameraEvent, _commandBuffer);
        }

        public void ReleaseCameraBuffer(Camera cam)
        {
            if (_commandBuffer != null)
            {
                cam.RemoveCommandBuffer(cameraEvent, _commandBuffer);
            }

        }

        internal virtual string PassName                                                                             { get; }
        public virtual   void   ExecuteCommandBuffer(WaterPassContext waterContext)                                  { }
        public virtual   void   ExecuteBeforeCameraRendering(Camera   cam,     ScriptableRenderContext context)      { }
        public virtual   void   ExecutePerFrame(HashSet<Camera>       cameras, CustomFixedUpdates      fixedUpdates) { }
        public abstract  void   Release();
    }
}
#endif