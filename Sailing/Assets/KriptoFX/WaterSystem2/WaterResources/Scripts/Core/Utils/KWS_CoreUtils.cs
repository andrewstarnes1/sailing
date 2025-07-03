using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
#if ENABLE_VR_MODULE && ENABLE_VR && ENABLE_XR_MODULE
using UnityEngine.XR;
#endif


//Stencil Builtin (free 32)
//Bit #7 (value=128) indicates any non-background object.
//Bit #6 (value=64) indicates non-lightmapped objects.
//Bit #5 (value=32) is not used by Unity.
//Bit #4 (value=16) is used for light shape culling during the lighting pass, so that the lighting shader is only executed on pixels that the light touches, and not on pixels where the surface geometry is actually behind the light volume.
//Lowest four bits (values 1, 2, 4, 8) are used for light layer culling masks


//Stencil URP (free 0, 2, 4, 8)
//- bit 0-1-2-3 are unused and users can override them (this could change if we run out of bits)
//- bit 4 is reserved for stenciling light volumes
//- bit 5-6 are used to store material type (00 = unlit/bakedLit, 01 = Lit, 10 = SimpleLit)
//- bit 7: kept for future usage



//Stencil HDRP (free 64, 128)

//internal enum StencilUsage
//{
//    Clear = 0,

//    // Note: first bit is free and can still be used by both phases.

//    // --- Following bits are used before transparent rendering ---

//    RequiresDeferredLighting = (1 << 1),
//    SubsurfaceScattering     = (1 << 2), //  SSS, Split Lighting
//    TraceReflectionRay       = (1 << 3), //  SSR or RTR - slot is reuse in transparent
//    Decals                   = (1 << 4), //  Use to tag when an Opaque Decal is render into DBuffer
//    ObjectMotionVector       = (1 << 5), //  Animated object (for motion blur, SSR, SSAO, TAA)

//    // --- Stencil  is cleared after opaque rendering has finished ---

//    // --- Following bits are used exclusively for what happens after opaque ---
//    ExcludeFromTAA    = (1 << 1), // Disable Temporal Antialiasing for certain objects
//    DistortionVectors = (1 << 2), // Distortion pass - reset after distortion pass, shared with SMAA
//    SMAA              = (1 << 2), // Subpixel Morphological Antialiasing
//    // Reserved TraceReflectionRay = (1 << 3) for transparent SSR or RTR
//    AfterOpaqueReservedBits = 0x38, // Reserved for future usage

//    // --- Following are user bits, we don't touch them inside HDRP and is up to the user to handle them ---
//    UserBit0 = (1 << 6),
//    UserBit1 = (1 << 7),

//    HDRPReservedBits = 255 & ~(UserBit0 | UserBit1),
//}

///// Stencil bit exposed to user and not reserved by HDRP.
///// Note that it is important that the Write Mask used in conjunction with these bits includes only this bits.
///// For example if you want to tag UserBit0, the shaderlab code for the stencil state setup would look like:
/////
///// WriteMask 64 // Value of UserBit0
///// Ref  64 // Value of UserBit0
///// Comp Always
///// Pass Replace
/////


namespace KWS
{
    internal static partial class KWS_CoreUtils
    {
        internal static bool SinglePassStereoEnabled;
        static RTHandleSystem _RTHandles;

        internal static RTHandleSystem RTHandles
        {
            get
            {
                if (_RTHandles == null)
                {
                    _RTHandles = new RTHandleSystem();
                    var screenSize = GetScreenSizeLimited(SinglePassStereoEnabled);
                    _RTHandles.Initialize(screenSize.x, screenSize.y);
                    _RTHandles.SetHardwareDynamicResolutionState(false);
                }
                return _RTHandles;
            }
        }

        internal static void ReleaseRTHandles()
        {
            _RTHandles?.Dispose();
            _RTHandles = null;

            //_defaultGrayTexture?.Release();
            //_defaultBlackTexture?.Release();
            //_defaultWhiteTexture?.Release();
            //_defaultGrayArrayTexture?.Release();
            //_defaultBlack3DTexture?.Release();
            //_defaultBlackCubeTexture?.Release();
           
        }

        internal static int _sourceRT_id            = Shader.PropertyToID("_SourceRT"); //by some reason _MainTex don't work
        internal static int _sourceRTHandleScale_id = Shader.PropertyToID("_SourceRTHandleScale");
        internal static int KWS_WaterViewPort_id    = Shader.PropertyToID("KWS_WaterViewPort");
        const           int MaxHeight               = 1080;
        const           int MaxHeightVR             = 2000;

        public static GraphicsFormat GetGraphicsFormatHDR()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Render)) return GraphicsFormat.B10G11R11_UFloatPack32;
            else return GraphicsFormat.R16G16B16A16_SFloat;
        }
        

        [Serializable]
        public struct Uint2
        {
            public uint X;
            public uint Y;
        }

        [Serializable]
        public struct Uint3
        {
            public uint X;
            public uint Y;
            public uint Z;
        }

        [Serializable]
        public struct Uint4
        {
            public uint X;
            public uint Y;
            public uint Z;
            public uint W;
        }


        static RenderTexture _defaultWhiteTexture;
        public static RenderTexture DefaultWhiteTexture
        {
            get
            {
                if (_defaultGrayTexture == null)
                {
                    _defaultGrayTexture = new RenderTexture(1, 1, 0);
                    _defaultGrayTexture.Create();
                    ClearRenderTexture(_defaultGrayTexture, ClearFlag.Color, new Color(1f, 1f, 1f, 1f));
                }

                return _defaultGrayTexture;
            }
        }

        static RenderTexture _defaultGrayTexture;
        static RenderTexture _defaultGrayArrayTexture;
        public static RenderTexture DefaultGrayTexture
        {
            get
            {
                if (_defaultGrayTexture == null)
                {
                    _defaultGrayTexture = new RenderTexture(1, 1, 0);
                    _defaultGrayTexture.Create();
                    ClearRenderTexture(_defaultGrayTexture, ClearFlag.Color, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                }

                return _defaultGrayTexture;
            }
        }

        public static RenderTexture DefaultGrayArrayTexture
        {
            get
            {
                if (_defaultGrayArrayTexture == null)
                {
                    _defaultGrayArrayTexture = new RenderTexture(1, 1, 0) {dimension = TextureDimension.Tex2DArray};
                    _defaultGrayArrayTexture.Create();
                    ClearRenderTexture(_defaultGrayArrayTexture, ClearFlag.Color, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                }

                return _defaultGrayArrayTexture;
            }
        }



        static RenderTexture _defaultBlackTexture;

        public static RenderTexture DefaultBlackTexture
        {
            get
            {
                if (_defaultBlackTexture == null)
                {
                    _defaultBlackTexture = new RenderTexture(1, 1, 0);
                    _defaultBlackTexture.Create();
                }

                return _defaultBlackTexture;
            }
        }

        static RenderTexture _defaultBlackCubeTexture;

        public static RenderTexture DefaultBlackCubeTexture
        {
            get
            {
                if (_defaultBlackCubeTexture == null)
                {
                    _defaultBlackCubeTexture = new RenderTexture(1, 1, 0) {dimension = TextureDimension.Cube};
                    _defaultBlackCubeTexture.Create();
                }

                return _defaultBlackCubeTexture;
            }
        }

        static RenderTexture _defaultBlack3DTexture;

        public static RenderTexture DefaultBlack3DTexture
        {
            get
            {
                if (_defaultBlack3DTexture == null)
                {
                    _defaultBlack3DTexture = new RenderTexture(1, 1, 0) {dimension = TextureDimension.Tex3D};
                    _defaultBlack3DTexture.Create();
                }

                return _defaultBlack3DTexture;
            }
        }

        public static Vector2 GetRTHandleResolution(RTHandle target)
        {
            var scale = target.rtHandleProperties.rtHandleScale;
            return new Vector2(target.rt.width * scale.x, target.rt.height * scale.y);
        }

        public static bool IsAtomicsSupported()
        {
            var api = SystemInfo.graphicsDeviceType;
            return api == GraphicsDeviceType.Direct3D11 || api == GraphicsDeviceType.Direct3D12;
        }


        public static bool IsWaterVisibleAndActive()
        {
            if (!WaterSystem.Instance.IsWaterVisible) return false;
            if (!WaterSystem.Instance.WaterRenderingActive) return false;

            return true;
        }

        //public static bool IsAnyWaterVisibleAndActive()
        //{
        //    foreach (var waterInstance in WaterSharedResources.WaterInstances)
        //    {
        //        if (IsWaterVisibleAndActive(waterInstance)) return true;
        //    }

        //    return false;
        //}
        public static bool CanRenderAnyWater()
        {
#if UNITY_EDITOR
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) return false;
            if (Application.isPlaying && Time.frameCount <= 3) return false; // by some reason single pass disabled first 2 frames on builtin and 3 frames in urp, WTF?
#else
            if(Time.frameCount <= 3) return false;
#endif
            return true;
        }

        public static bool CanRenderCurrentCamera(Camera cam)
        {
            if (cam == null) return false;
            var camType = cam.cameraType;
            if (camType != CameraType.Game && camType != CameraType.SceneView && camType != CameraType.VR) return false;
            if (!cam.IsLayerRendered(KWS_Settings.Water.WaterLayer)) return false;
            if (!CanRenderWaterForCurrentCamera_PlatformSpecific(cam)) return false;
            if (!CanRenderAnyWater()) return false;
            //if (KWS_CoreUtils.SinglePassStereoEnabled && (camType != CameraType.Game && camType != CameraType.VR)) return false;
            ////if (cam.name == "TopViewDepthCamera" || cam.name.Contains("reflect")) return false; //todo check GC
#if UNITY_EDITOR
           // if (KWS_CoreUtils.SinglePassStereoEnabled && cam.name == "Preview Camera") return false;
#endif

            return true;
        }


        public static bool CanRenderUnderwater()
        {
            if (!WaterSystem.QualitySettings.UseUnderwaterEffect) return false;
            return WaterSystem.IsCameraPartialUnderwater;
        }


        public static Camera GetFixedUpdateCamera(HashSet<Camera> cameras)
        {
            if (Application.isPlaying == false)
            {
#if UNITY_EDITOR
                UnityEditor.SceneView sceneView   = UnityEditor.SceneView.lastActiveSceneView;
                return sceneView != null ? sceneView.camera : null;
#else
                return null;
#endif
            }
            else
            {
                Camera minDepthCamera = null;
                foreach (var cam in cameras)
                {
                    if(cam == null) continue;
                    if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.VR) continue;

                    if (minDepthCamera == null || minDepthCamera.depth < cam.depth) minDepthCamera = cam;
                }

                return minDepthCamera;
            }
        }

        public static bool IsFrameDebuggerEnabled()
        {
#if UNITY_EDITOR
            //return true;
            var focusedWindow = UnityEditor.EditorWindow.focusedWindow;
            return focusedWindow != null && focusedWindow.titleContent.text == "Frame Debug";
#else
            return false;
#endif
        }

        public static void SetKeyword(string keyword, bool state)
        {
            if (state)
                Shader.EnableKeyword(keyword);
            else
                Shader.DisableKeyword(keyword);
        }

        public static void SetKeyword(this Material mat, string keyword, bool state)
        {
            if (state)
                mat.EnableKeyword(keyword);
            else
                mat.DisableKeyword(keyword);
        }

        public static void SetKeyword(this CommandBuffer buffer, string keyword, bool state)
        {
            if (state)
                buffer.EnableShaderKeyword(keyword);
            else
                buffer.DisableShaderKeyword(keyword);
        }

        public static void SetKeyword(this ComputeShader cs, string keyword, bool state)
        {
            if (state)
                cs.EnableKeyword(keyword);
            else
                cs.DisableKeyword(keyword);
        }

        public static void SetMatrices<T>(T obj, params (int id, Matrix4x4 value)[] datas) where T : class
        {
            if (obj is Material mat)
            {
                foreach (var data in datas)
                {
                    mat.SetMatrix(data.id, data.value);
                }
            }
            else if (obj is ComputeShader cs)
            {
                foreach (var data in datas)
                {
                    cs.SetMatrix(data.id, data.value);
                }
            }
        }

        public static void SetFloats<T>(T obj, params (int id, float value)[] datas) where T : class
        {
            if (obj is Material mat)
            {
                foreach (var data in datas)
                {
                    mat.SetFloat(data.id, data.value);
                }
            }
            else if (obj is ComputeShader cs)
            {
                foreach (var data in datas)
                {
                    cs.SetFloat(data.id, data.value);
                }
            }
        }

        public static void SetInts<T>(T obj, params (int id, int value)[] datas) where T : class
        {
            if (obj is Material mat)
            {
                foreach (var data in datas)
                {
                    mat.SetInt(data.id, data.value);
                }
            }
            else if (obj is ComputeShader cs)
            {
                foreach (var data in datas)
                {
                    cs.SetInt(data.id, data.value);
                }
            }
        }

        public static void SetVectors<T>(T obj, params (int id, Vector4 value)[] datas) where T : class
        {
            if (obj is Material mat)
            {
                foreach (var data in datas)
                {
                    mat.SetVector(data.id, data.value);
                }
            }
            else if (obj is ComputeShader cs)
            {
                foreach (var data in datas)
                {
                    cs.SetVector(data.id, data.value);
                }
            }
        }

        public static void SetKeywords<T>(T obj, params (string key, bool value)[] datas) where T : class
        {
            if (obj is Material mat)
            {
                foreach (var data in datas)
                {
                    mat.SetKeyword(data.key, data.value);
                }
            }
            else if (obj is ComputeShader cs)
            {
                foreach (var data in datas)
                {
                    cs.SetKeyword(data.key, data.value);
                }
            }
        }

        public static Texture GetSafeTexture(this Texture rt, Color color = default)
        {
            if (rt != null) return rt;
            return color == Color.gray ? DefaultGrayTexture : DefaultBlackTexture;
        }

        public static RenderTexture GetSafeTexture(this RenderTexture rt, Color color = default)
        {
            if (rt != null) return rt;
            return color == Color.gray ? DefaultGrayTexture : DefaultBlackTexture;
        }

        public static RenderTexture GetSafeCubeTexture(this RenderTexture rt, Color color = default)
        {
            if (rt != null) return rt;
            return DefaultBlackCubeTexture;
        }

        public static RenderTexture GetSafeArrayTexture(this RTHandle rtHandle)
        {
            if (rtHandle != null && rtHandle.rt != null) return rtHandle.rt;
            return DefaultGrayArrayTexture;
        }

        public static RenderTexture GetSafeTexture(this RTHandle rtHandle, Color color = default)
        {
            if (rtHandle != null && rtHandle.rt != null) return rtHandle.rt;
            return color == Color.gray ? DefaultGrayTexture : DefaultBlackTexture;
        }

        public static Mesh CreateQuad()
        {
            var mesh = new Mesh();
            mesh.hideFlags = HideFlags.DontSave;

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f,  -0.5f, 0),
                new Vector3(-0.5f, 0.5f,  0),
                new Vector3(0.5f,  0.5f,  0)
            };
            mesh.vertices = vertices;

            int[] tris = new int[6]
            {
                0, 2, 1,
                2, 3, 1
            };
            mesh.triangles = tris;

            Vector3[] normals = new Vector3[4]
            {
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward
            };
            mesh.normals = normals;

            Vector2[] uv = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            mesh.uv = uv;

            return mesh;
        }

      
        public static Material CreateMaterial(string shaderName, string prefix)
        {
            return CreateMaterial(string.Format("{0}_{1}", shaderName, prefix));
        }

        public static Material CreateMaterial(string shaderName, bool useWaterStencilMask = false)
        {
            var waterShader = Shader.Find(shaderName);
            if (waterShader == null)
            {
                Debug.LogError("Can't find the shader '" + shaderName + "' in the resources folder. Try to reimport the package.");
                return null;
            }

            var waterMaterial = new Material(waterShader);
            waterMaterial.hideFlags = HideFlags.HideAndDontSave;
            if(useWaterStencilMask) waterMaterial.SetInt(KWS_ShaderConstants.ConstantWaterParams.KWS_StencilMaskValue, KWS_Settings.MaskStencilValue);
            return waterMaterial;
        }


        public static ComputeShader LoadComputeShader(string shaderName)
        {
            var cs = Resources.Load<ComputeShader>(shaderName);
            if (cs == null)
            {
                Debug.LogError("Can't load the shader '" + shaderName + "'. Try to right click this shader -> reimport");
                return null;
            }

            var newShader = UnityEngine.Object.Instantiate(cs);
            Resources.UnloadAsset(cs);
            return newShader;
        }

        public static  void SetFallbackBuffer<T>(ref ComputeBuffer buffer, string shaderName) where T : struct
        {
            buffer = KWS_CoreUtils.GetOrUpdateBuffer<T>(ref buffer, 1);
            buffer.SetData(new T[1]);
            Shader.SetGlobalBuffer(shaderName, buffer);
        }
        
        public static  void SetFallbackBuffer<T>(ref ComputeBuffer buffer, int shaderNameID) where T : struct
        {
            buffer = KWS_CoreUtils.GetOrUpdateBuffer<T>(ref buffer, 1);
            buffer.SetData(new T[1]);
            Shader.SetGlobalBuffer(shaderNameID, buffer);
        }

        public static ComputeBuffer GetOrUpdateBuffer<T>(ref ComputeBuffer buffer, int size, ComputeBufferType bufferType = ComputeBufferType.Default, ComputeBufferMode bufferMode = ComputeBufferMode.Immutable) where T : struct
        {
            if (buffer == null)
            {
                buffer = new ComputeBuffer(size, System.Runtime.InteropServices.Marshal.SizeOf<T>(), bufferType, bufferMode);
            }
            else if (size > buffer.count)
            {
                buffer.Dispose();
                buffer = new ComputeBuffer(size, System.Runtime.InteropServices.Marshal.SizeOf<T>(), bufferType, bufferMode);
                // Debug.Log("ReInitializeHashBuffer");
            }

            return buffer;
        }

        public static NativeArray<T> GetOrUpdateNativeArray<T>(ref NativeArray<T> array, int size, Allocator allocator) where T : struct
        {
            if (array == null || !array.IsCreated || size > array.Length)
            {
                if (array.IsCreated) array.Dispose();

                int newSize = Mathf.NextPowerOfTwo(size);
                array = new NativeArray<T>(newSize, allocator);
            }

            return array;
        }

        public static void SetSource(this CommandBuffer cmd, RenderTargetIdentifier source)
        {
            cmd.SetGlobalTexture(_sourceRT_id, source);
        }


        public static void BlitTriangle(this CommandBuffer cmd, Material mat, RenderTargetIdentifier dest, int pass = 0, int slice = -1)
        {
            cmd.SetRenderTarget(dest, 0, CubemapFace.Unknown, slice);
            cmd.DrawProcedural(Matrix4x4.identity, mat, pass, MeshTopology.Triangles, 3);
        }

        public static void BlitTriangle(this CommandBuffer cmd, Material mat, int pass = 0)
        {
            cmd.DrawProcedural(Matrix4x4.identity, mat, pass, MeshTopology.Triangles, 3);
        }

        public static void BlitTriangle(this CommandBuffer cmd, RenderTargetIdentifier source, Material mat, int pass = 0)
        {
            cmd.SetGlobalTexture(_sourceRT_id, source);
            cmd.DrawProcedural(Matrix4x4.identity, mat, pass, MeshTopology.Triangles, 3);
        }

        public static void BlitTriangle(this CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat, int pass = 0)
        {
            cmd.SetGlobalTexture(_sourceRT_id, source);
            cmd.BlitTriangle(mat, dest, pass);
        }

        public static void BlitTriangle(this CommandBuffer cmd, RenderTargetIdentifier source, Vector4 sourceRTHandleScale, RenderTargetIdentifier dest, Material mat, int pass = 0)
        {
            cmd.SetGlobalVector(_sourceRTHandleScale_id, sourceRTHandleScale);
            cmd.SetGlobalTexture(_sourceRT_id, source);
            cmd.BlitTriangle(mat, dest, pass);
        }

        public static void BlitTriangle(this CommandBuffer cmd, RenderTargetIdentifier target, Vector2Int viewPortSize, Material mat, ClearFlag clearFlag, Color clearColor, int pass = 0, int slice = -1)
        {
            cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, slice);
            SetViewportAndClear(cmd, new Rect(0, 0, viewPortSize.x, viewPortSize.y), clearFlag, clearColor);
            cmd.DrawProcedural(Matrix4x4.identity, mat, pass, MeshTopology.Triangles, 3);
        }

        public static void BlitTriangle(this CommandBuffer cmd, RenderTargetIdentifier source, Vector4 sourceRTHandleScale, RenderTargetIdentifier dest, Vector2Int viewPortSize, Material mat, int pass = 0)
        {
            cmd.SetGlobalVector(_sourceRTHandleScale_id, sourceRTHandleScale);
            cmd.SetGlobalTexture(_sourceRT_id, source);
            cmd.BlitTriangle(dest, viewPortSize, mat, ClearFlag.None, Color.clear, pass);
        }

        public static void SetViewport(this CommandBuffer cmd, RTHandle target)
        {
            if (target.useScaling)
            {
                var scaledViewportSize = target.GetScaledSize(target.rtHandleProperties.currentViewportSize);
                cmd.SetViewport(new Rect(0.0f, 0.0f, scaledViewportSize.x, scaledViewportSize.y));
            }
        }

        public static void SetViewport(this CommandBuffer cmd, Rect viewPort, RTHandle target)
        {
            if (target.useScaling)
            {
                var scaledViewportSize = target.GetScaledSize(new Vector2Int((int) viewPort.width, (int) viewPort.height));
                var scaledViewportPos  = target.GetScaledSize(new Vector2Int((int) viewPort.x,     (int) viewPort.y));
                cmd.SetViewport(new Rect(scaledViewportPos.x, scaledViewportPos.y, scaledViewportSize.x, scaledViewportSize.y));
            }
        }


        public static void SetViewportAndClear(this CommandBuffer cmd, Rect viewPort, RTHandle target, ClearFlag clearFlag, Color clearColor)
        {
#if !UNITY_EDITOR
            SetViewport(cmd, viewPort, target);
#endif
            CoreUtils.ClearRenderTarget(cmd, clearFlag, clearColor);
#if UNITY_EDITOR
            SetViewport(cmd, viewPort, target);
#endif
        }

        public static void SetViewportAndClear(this CommandBuffer cmd, RTHandle target, ClearFlag clearFlag, Color clearColor)
        {
#if !UNITY_EDITOR
            SetViewport(cmd, target);
#endif
            CoreUtils.ClearRenderTarget(cmd, clearFlag, clearColor);
#if UNITY_EDITOR
            SetViewport(cmd, target);
#endif
        }

        public static void SetViewportAndClear(this CommandBuffer cmd, Rect rect, ClearFlag clearFlag, Color clearColor)
        {
            cmd.SetViewport(rect);
            CoreUtils.ClearRenderTarget(cmd, clearFlag, clearColor);
        }

        public static void BlitTriangleRTHandle(this CommandBuffer cmd, Rect viewPort, RTHandle target, Material mat, ClearFlag clearFlag, Color clearColor, int pass = 0)
        {
            var waterViewportSize  = target.GetScaledSize(new Vector2Int((int) viewPort.width, (int) viewPort.height));
            var waterViewportPos   = target.GetScaledSize(new Vector2Int((int) viewPort.x,     (int) viewPort.y));
            var targetViewportSize = target.GetScaledSize(target.rtHandleProperties.currentViewportSize);

            var waterViewPort = new Vector4((float) waterViewportSize.x / targetViewportSize.x, (float) waterViewportSize.y / targetViewportSize.y,
                                            (float) waterViewportPos.x  / targetViewportSize.x, (float) waterViewportPos.y  / targetViewportSize.y);
            cmd.SetGlobalVector(KWS_WaterViewPort_id, waterViewPort);

            cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, -1);
            SetViewportAndClear(cmd, viewPort, target, clearFlag, clearColor);
            cmd.DrawProcedural(Matrix4x4.identity, mat, pass, MeshTopology.Triangles, 3);
            cmd.SetGlobalVector(KWS_WaterViewPort_id, Vector4.zero);
        }

        public static void BlitTriangleRTHandle(this CommandBuffer cmd, RTHandle target, Material mat, ClearFlag clearFlag, Color clearColor, int pass = 0)
        {
            cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, -1);
            SetViewportAndClear(cmd, target, clearFlag, clearColor);
            cmd.DrawProcedural(Matrix4x4.identity, mat, pass, MeshTopology.Triangles, 3);
        }

        public static void BlitTriangleRTHandleDepth(this CommandBuffer cmd, RTHandle target, RenderTargetIdentifier depth, Material mat, ClearFlag clearFlag, Color clearColor, int pass = 0)
        {
            cmd.SetRenderTarget(target, depth, 0, CubemapFace.Unknown, -1);
            SetViewportAndClear(cmd, target, clearFlag, clearColor);
            cmd.DrawProcedural(Matrix4x4.identity, mat, pass, MeshTopology.Triangles, 3);
        }

        public static void BlitTriangleRTHandle(this CommandBuffer cmd, RenderTargetIdentifier source, Vector4 sourceRTHandleScale, RTHandle target, Material mat, ClearFlag clearFlag, Color clearColor, int pass = 0)
        {
            cmd.SetGlobalVector(_sourceRTHandleScale_id, sourceRTHandleScale);
            cmd.SetGlobalTexture(_sourceRT_id, source);
            cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, -1);
            SetViewportAndClear(cmd, target, clearFlag, clearColor);
            cmd.DrawProcedural(Matrix4x4.identity, mat, pass, MeshTopology.Triangles, 3);
        }

        public static void BlitTriangleRTHandle(this CommandBuffer cmd, RTHandle source, RTHandle target, Material mat, ClearFlag clearFlag, Color clearColor, int pass = 0)
        {
            cmd.SetGlobalVector(_sourceRTHandleScale_id, source.rtHandleProperties.rtHandleScale);
            cmd.SetGlobalTexture(_sourceRT_id, source);
            cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, -1);
            SetViewportAndClear(cmd, target, clearFlag, clearColor);
            cmd.DrawProcedural(Matrix4x4.identity, mat, pass, MeshTopology.Triangles, 3);
        }


        public static Vector2Int GetScreenSizeLimited(bool isStereoEnabled)
        {
            var size = GetScreenSize(isStereoEnabled);

            var maxHeight = isStereoEnabled ? MaxHeightVR : MaxHeight;
            if (size.y > maxHeight)
            {
                size.x = (int) (maxHeight * size.x / (float)size.y);
                size.y = maxHeight;
            }

            return size;
        }

        public static Vector2Int GetScreenSize(bool isStereoEnabled)
        {
            int width;
            int height;

#if ENABLE_VR_MODULE && ENABLE_VR && ENABLE_XR_MODULE
            if (isStereoEnabled)
            {
                width  = XRSettings.eyeTextureWidth;
                height = XRSettings.eyeTextureHeight;
            }
            else
#endif
            {
                width  = Screen.width;
                height = Screen.height;
            }

            return new Vector2Int(width, height);
        }

        private static RenderTargetIdentifier[] mrt2       = new RenderTargetIdentifier[2];
        private static RenderTargetIdentifier[] mrt3       = new RenderTargetIdentifier[3];
        private static RenderTargetIdentifier[] mrt4       = new RenderTargetIdentifier[4];
        private static RTHandle[]               mrtHandle2 = new RTHandle[2];
        private static RTHandle[]               mrtHandle3 = new RTHandle[3];

        public static RenderTargetIdentifier[] GetMrt(RenderTargetIdentifier rt1, RenderTargetIdentifier rt2)
        {
            mrt2[0] = rt1;
            mrt2[1] = rt2;
            return mrt2;
        }
        public static RenderTargetIdentifier[] GetMrt(RenderTargetIdentifier rt1, RenderTargetIdentifier rt2, RenderTargetIdentifier rt3, RenderTargetIdentifier rt4)
        {
            mrt4[0] = rt1;
            mrt4[1] = rt2;
            mrt4[2] = rt3;
            mrt4[3] = rt4;
            return mrt4;
        }
        public static RenderTargetIdentifier[] GetMrt(RenderTargetIdentifier rt1, RenderTargetIdentifier rt2, RenderTargetIdentifier rt3)
        {
            mrt3[0] = rt1;
            mrt3[1] = rt2;
            mrt3[2] = rt3;
            return mrt3;
        }

        public static RTHandle[] GetMrtHandle(RTHandle rt1, RTHandle rt2)
        {
            mrtHandle2[0] = rt1;
            mrtHandle2[1] = rt2;
            return mrtHandle2;
        }

        public static RTHandle[] GetMrtHandle(RTHandle rt1, RTHandle rt2, RTHandle rt3)
        {
            mrtHandle3[0] = rt1;
            mrtHandle3[1] = rt2;
            mrtHandle3[2] = rt3;
            return mrtHandle3;
        }

        public static void ReleaseComputeBuffers(params ComputeBuffer[] computeBuffers)
        {
            for (var i = 0; i < computeBuffers.Length; i++)
            {
                if (computeBuffers[i] == null) continue;
                computeBuffers[i].Release();
            }
        }

        public static void ReleaseGraphicsBuffers(params GraphicsBuffer[] graphicsBuffers)
        {
            for (var i = 0; i < graphicsBuffers.Length; i++)
            {
                if (graphicsBuffers[i] == null) continue;
                graphicsBuffers[i].Release();
            }
        }


        public static void ReleaseRenderTextures(params RenderTexture[] renderTextures)
        {
            for (var i = 0; i < renderTextures.Length; i++)
            {
                if (renderTextures[i] == null) continue;
                renderTextures[i].Release();
            }
        }

        public static void ClearRenderTexture(RenderTexture rt, ClearFlag clearFlag, Color clearColor)
        {
            var activeRT = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear((clearFlag & ClearFlag.Depth) != 0, (clearFlag & ClearFlag.Color) != 0, clearColor);
            RenderTexture.active = activeRT;
        }


        public static RTHandle RTHandleAllocVR(
            int                     width,
            int                     height,
            DepthBits               depthBufferBits   = DepthBits.None,
            GraphicsFormat          colorFormat       = GraphicsFormat.R8G8B8A8_SRGB,
            FilterMode              filterMode        = FilterMode.Point,
            TextureWrapMode         wrapMode          = TextureWrapMode.Repeat,
            bool                    enableRandomWrite = false,
            bool                    useMipMap         = false,
            bool                    autoGenerateMips  = true,
            int                     mipMapCount       = 0,
            bool                    isShadowMap       = false,
            int                     anisoLevel        = 1,
            float                   mipMapBias        = 0,
            MSAASamples             msaaSamples       = MSAASamples.None,
            bool                    bindTextureMS     = false,
            bool                    useDynamicScale   = false,
            RenderTextureMemoryless memoryless        = RenderTextureMemoryless.None,
            string                  name              = ""
        )
        {

            var dimension = SinglePassStereoEnabled ? TextureDimension.Tex2DArray : TextureDimension.Tex2D;
            var slices    = SinglePassStereoEnabled ? 2 : 1;

            return RTHandles.Alloc(width, height, slices: slices, depthBufferBits: depthBufferBits, colorFormat: colorFormat, filterMode: filterMode, wrapMode: wrapMode, dimension: dimension,
                                               enableRandomWrite: enableRandomWrite, useMipMap: useMipMap, autoGenerateMips: autoGenerateMips, isShadowMap: isShadowMap, anisoLevel: anisoLevel,
                                               mipMapBias: mipMapBias, msaaSamples: msaaSamples, bindTextureMS: bindTextureMS, useDynamicScale: useDynamicScale, memoryless: memoryless, name: name);
        }


        public static RTHandle RTHandleAllocVR(
            Vector2                 scaleFactor,
            DepthBits               depthBufferBits   = DepthBits.None,
            GraphicsFormat          colorFormat       = GraphicsFormat.R8G8B8A8_SRGB,
            FilterMode              filterMode        = FilterMode.Point,
            TextureWrapMode         wrapMode          = TextureWrapMode.Repeat,
            bool                    enableRandomWrite = false,
            bool                    useMipMap         = false,
            bool                    autoGenerateMips  = true,
            int                     mipMapCount       = 0,
            bool                    isShadowMap       = false,
            int                     anisoLevel        = 1,
            float                   mipMapBias        = 0,
            MSAASamples             msaaSamples       = MSAASamples.None,
            bool                    bindTextureMS     = false,
            bool                    useDynamicScale   = false,
            RenderTextureMemoryless memoryless        = RenderTextureMemoryless.None,
            string                  name              = ""
        )
        {
            var dimension = SinglePassStereoEnabled ? TextureDimension.Tex2DArray : TextureDimension.Tex2D;
            var slices    = SinglePassStereoEnabled ? 2 : 1;

            return RTHandles.Alloc(scaleFactor, slices: slices, depthBufferBits: depthBufferBits, colorFormat: colorFormat, filterMode: filterMode, wrapMode: wrapMode, dimension: dimension,
                                               enableRandomWrite: enableRandomWrite, useMipMap: useMipMap, autoGenerateMips: autoGenerateMips, isShadowMap: isShadowMap, anisoLevel: anisoLevel,
                                               mipMapBias: mipMapBias, msaaSamples: msaaSamples, bindTextureMS: bindTextureMS, useDynamicScale: useDynamicScale, memoryless: memoryless, name: name);
        }

        public static RTHandle RTHandleAllocVR(
            ScaleFunc               scaleFunc,
            DepthBits               depthBufferBits   = DepthBits.None,
            GraphicsFormat          colorFormat       = GraphicsFormat.R8G8B8A8_SRGB,
            FilterMode              filterMode        = FilterMode.Point,
            TextureWrapMode         wrapMode          = TextureWrapMode.Repeat,
            bool                    enableRandomWrite = false,
            bool                    useMipMap         = false,
            bool                    autoGenerateMips  = true,
            bool                    isShadowMap       = false,
            int                     anisoLevel        = 1,
            float                   mipMapBias        = 0,
            int                     mipMapCount       = 0,
            MSAASamples             msaaSamples       = MSAASamples.None,
            bool                    bindTextureMS     = false,
            bool                    useDynamicScale   = false,
            RenderTextureMemoryless memoryless        = RenderTextureMemoryless.None,
            string                  name              = ""
        )
        {
            var dimension = SinglePassStereoEnabled ? TextureDimension.Tex2DArray : TextureDimension.Tex2D;
            var slices    = SinglePassStereoEnabled ? 2 : 1;

            return RTHandles.Alloc(scaleFunc, slices: slices, depthBufferBits: depthBufferBits, colorFormat: colorFormat, filterMode: filterMode, wrapMode: wrapMode, dimension: dimension,
                                               enableRandomWrite: enableRandomWrite, useMipMap: useMipMap, autoGenerateMips: autoGenerateMips, isShadowMap: isShadowMap, anisoLevel: anisoLevel,
                                               mipMapBias: mipMapBias, msaaSamples: msaaSamples, bindTextureMS: bindTextureMS, useDynamicScale: useDynamicScale, memoryless: memoryless, name: name);
        }

        public static void SetOrthoMatrix_VP(CommandBuffer cmd, Vector3 size, Vector3 position, Quaternion zoneRotation)
        {
            var halfWidth  = size.x * 0.5f;
            var halfHeight = size.z * 0.5f;
            var far        = size.y;
            var near       = 0.0001f;
          
            var orthoProjMatrix = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-halfWidth, halfWidth, halfHeight, -halfHeight, near, far), true);

            var modelRotation = Quaternion.Euler(-90, 0, 0) * zoneRotation;
            var modelView     = Matrix4x4.TRS(new Vector3(0, 0, -far * 0.5f), modelRotation, new Vector3(1, -1, -1));

            cmd.SetGlobalMatrix(KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_VP_ORTHO, orthoProjMatrix * modelView);
            cmd.SetGlobalVector("KWS_WorldSpaceCameraPosOrtho", position);
        }


        internal static Matrix4x4[] KWS_MATRIX_I_P  = new Matrix4x4[2];
        internal static Matrix4x4[] KWS_MATRIX_VP   = new Matrix4x4[2];
        internal static Matrix4x4[] KWS_MATRIX_I_VP = new Matrix4x4[2];

        public static void UpdateCameraMatrices(Camera cam)
        {
            var cameraProjectionMatrix = cam.projectionMatrix;
            if (KWS_CoreUtils.SinglePassStereoEnabled)
            {
                for (uint eyeIndex = 0; eyeIndex < 2; eyeIndex++)
                {
                    var matrix_cameraProjeciton = cam.GetStereoProjectionMatrix((Camera.StereoscopicEye)eyeIndex);
                    var matrix_V                = GL.GetGPUProjectionMatrix(matrix_cameraProjeciton, true);
                    var matrix_P                = cam.GetStereoViewMatrix((Camera.StereoscopicEye)eyeIndex);
                    var matrix_VP               = matrix_V * matrix_P;

                    KWS_MATRIX_I_P[eyeIndex]  = matrix_P.inverse;
                    KWS_MATRIX_VP[eyeIndex]   = matrix_VP;
                    KWS_MATRIX_I_VP[eyeIndex] = matrix_VP.inverse;
                }
            }
            else
            {
                var matrix_V  = GL.GetGPUProjectionMatrix(cameraProjectionMatrix, true);
                var matrix_P  = cam.worldToCameraMatrix;
                var matrix_VP = matrix_V * matrix_P;

                KWS_MATRIX_I_P[0]  = matrix_P.inverse;
                KWS_MATRIX_VP[0]   = matrix_VP;
                KWS_MATRIX_I_VP[0] = matrix_VP.inverse;
            }
        }


        public static Matrix4x4[] SetAllVPCameraMatricesAndGetVP(Camera cam)
        {
            UpdateCameraMatrices(cam);
            if (KWS_CoreUtils.SinglePassStereoEnabled)
            {
                Shader.SetGlobalMatrixArray(KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_I_P_STEREO,  KWS_MATRIX_I_P);
                Shader.SetGlobalMatrixArray(KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_VP_STEREO,   KWS_MATRIX_VP);
                Shader.SetGlobalMatrixArray(KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_I_VP_STEREO, KWS_MATRIX_I_VP);
            }
            else
            {
               
                Shader.SetGlobalMatrix(KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_I_P,  KWS_MATRIX_I_P[0]);
                Shader.SetGlobalMatrix(KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_VP,   KWS_MATRIX_VP[0]);
                Shader.SetGlobalMatrix(KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_I_VP, KWS_MATRIX_I_VP[0]);
            }

            return KWS_MATRIX_VP;
        }

        public static void SetAllVPCameraMatrices(Camera cam, CommandBuffer cmd, ComputeShader cs)
        {
            UpdateCameraMatrices(cam);
            if (KWS_CoreUtils.SinglePassStereoEnabled)
            {
                cmd.SetComputeMatrixArrayParam(cs, KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_I_P_STEREO,  KWS_MATRIX_I_P);
                cmd.SetComputeMatrixArrayParam(cs, KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_VP_STEREO,   KWS_MATRIX_VP);
                cmd.SetComputeMatrixArrayParam(cs, KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_I_VP_STEREO, KWS_MATRIX_I_VP);
            }
            else
            {

                cmd.SetComputeMatrixParam(cs, KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_I_P, KWS_MATRIX_I_P[0]);
                cmd.SetComputeMatrixParam(cs, KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_VP,  KWS_MATRIX_VP[0]);
                cmd.SetComputeMatrixParam(cs, KWS_ShaderConstants.CameraMatrix.KWS_MATRIX_I_VP, KWS_MATRIX_I_VP[0]);
            }
        }


        public static Material CreateDepthSdfMaterial()
        {
            return KWS_CoreUtils.CreateMaterial("Hidden/KriptoFX/KWS/KWS_JumpFloodSDF");
        }

        public static void ComputeSDF(CommandBuffer cmd, Material sdfMaterial, float maxAreaSize, Vector4 BakedNearFarSizeXZ, RenderTexture depthSource, RenderTexture sdfTarget)
        {
            if (cmd == null || sdfMaterial == null || depthSource == null || sdfTarget == null)
            {
                Debug.LogError($"cant render ComputeSDF {cmd} {sdfMaterial} {depthSource} {sdfTarget}");
                return;
            }

            cmd.Clear();

            KWS.CoreUtils.SetRenderTarget(cmd, sdfTarget, ClearFlag.Color, Color.clear);
            var tempRt1 = RenderTexture.GetTemporary(sdfTarget.width, sdfTarget.height, 0, GraphicsFormat.R16G16_SFloat);
            var tempRt2 = RenderTexture.GetTemporary(sdfTarget.width, sdfTarget.height, 0, GraphicsFormat.R16G16_SFloat);

            cmd.SetGlobalTexture(KWS_ShaderConstants.OrthoDepth.KWS_WaterOrthoDepthRT, depthSource);
            cmd.SetGlobalVector(KWS_ShaderConstants.OrthoDepth.KWS_OrthoDepthNearFarSize, BakedNearFarSizeXZ);
            KWS_CoreUtils.BlitTriangle(cmd, sdfMaterial, tempRt1,  0);

            var stepSize = new Vector2(tempRt1.width, tempRt1.height);
            var           passes   = Mathf.CeilToInt(Mathf.Log(Mathf.Max(tempRt1.width, tempRt1.height)) / Mathf.Log(2.0f));
            RenderTexture target   = tempRt1;

            cmd.SetGlobalFloat("KWS_MaxAreaSize", maxAreaSize);

            for (int i = 0; i < passes; i++)
            {
                var source = i % 2 == 0 ? tempRt1 : tempRt2;
                target = i % 2 == 0 ? tempRt2 : tempRt1;

                stepSize /= 2;
                cmd.SetGlobalVector("KWS_StepSize", stepSize);
               
               
                CoreUtils.SetRenderTarget(cmd, KWS_CoreUtils.GetMrt(target, sdfTarget), target);
                BlitTriangle(cmd, source, sdfMaterial, 1);
            }

            RenderTexture.ReleaseTemporary(tempRt1);
            RenderTexture.ReleaseTemporary(tempRt2);

            Graphics.ExecuteCommandBuffer(cmd);
        }

    }
}