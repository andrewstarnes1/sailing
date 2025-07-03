using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KWS
{
    internal static class DebugHelpers
    {
        private static Material _debugTextureSliceMaterial;

        public static void DebugFft()
        {
            if (Event.current.type.Equals(EventType.Repaint))
            {
                if (_debugTextureSliceMaterial == null) _debugTextureSliceMaterial = KWS_CoreUtils.CreateMaterial("Hidden/_KriptoFX/KWS/Water/Debug/TextureSlice");
                if (WaterSharedResources.FftWavesDisplacement == null) return;

                var fftRT = WaterSharedResources.FftWavesDisplacement.rt;
                if (fftRT != null)
                {
                    for (int i = 0; i < fftRT.volumeDepth; i++)
                    {
                        _debugTextureSliceMaterial.SetInt("_slice", i);
                        Graphics.DrawTexture(new Rect(i * fftRT.width + i * 5, 0, fftRT.width, fftRT.height), fftRT, _debugTextureSliceMaterial, 0);
                    }
                }

                var fftNormal = WaterSharedResources.FftWavesNormal.rt;
                if (fftNormal != null)
                {
                    for (int i = 0; i < fftNormal.volumeDepth; i++)
                    {
                        _debugTextureSliceMaterial.SetInt("_slice", i);
                        Graphics.DrawTexture(new Rect(i * fftNormal.width + i * 5, fftNormal.height + 5, fftNormal.width, fftNormal.height), fftNormal, _debugTextureSliceMaterial, 1);
                    }
                }

            }
        }

        public static void DebugDynamicWaves()
        {
            if (Event.current.type.Equals(EventType.Repaint))
            {
                var data           = WaterSharedResources.DynamicWavesRT;
                var additionalData = WaterSharedResources.DynamicWavesAdditionalDataRT;
                var maskDepth      = WaterSharedResources.DynamicWavesMaskRT;

                if (data != null)
                {
                    Graphics.DrawTexture(new Rect(0, 0,              512, 512), KWS_CoreUtils.DefaultGrayTexture);
                   
                    Graphics.DrawTexture(new Rect(0, 0,              data.width, data.height),         data);
                   
                }

                if (additionalData != null)
                {
                    Graphics.DrawTexture(new Rect(1920 - 512 - 10, 0, 512, 512), KWS_CoreUtils.DefaultGrayTexture);
                    Graphics.DrawTexture(new Rect(1920 - 512 - 10, 0, 512, 512), additionalData);
                }

                if (maskDepth != null)
                {
                    Graphics.DrawTexture(new Rect(0, 512 + 10, 512, 512), KWS_CoreUtils.DefaultGrayTexture);
                    Graphics.DrawTexture(new Rect(0, 512 + 10, 512, 512), maskDepth);
                }

                //if (atlas != null)
                //{
                //    Graphics.DrawTexture(new Rect(0, 512 + 10, atlas.width, atlas.height), KWS_CoreUtils.DefaultGrayTexture);
                //    Graphics.DrawTexture(new Rect(0, 512 + 10, atlas.width, atlas.height), atlas);
                //}
            }
        }

        public static void DebugOrthoDepth()
        {
            if (Event.current.type.Equals(EventType.Repaint))
            {
                var depth   = WaterSharedResources.OrthoDepth;
                if (depth != null)
                {
                    Graphics.DrawTexture(new Rect(0, 0,        512, 512), depth);
                }

                var depthSDF = WaterSharedResources.OrthoDepthSDF;
                if (depthSDF != null)
                {
                    Graphics.DrawTexture(new Rect(0, 512 + 20, 512, 512), depthSDF);
                }
            }
        }
        private static WaterSurfaceRequestList _request = new WaterSurfaceRequestList();

        public static void RequestBuoyancy()
        {
            var sizeX        = 30;
            var sizeY        = 30;
            var worldPos     = new List<Vector3>();
            var worldNormals = new List<Vector3>();
            var waterPivot   = WaterSystem.Instance.WaterPivotWorldPosition;
            waterPivot.y =  0;

            /////////////////////////////////////////////////////////////////////////////
            int idx = 0;
            for (int x = -sizeX; x < sizeX; x++)
            {
                for (int z = -sizeY; z < sizeY; z++)
                {
                    worldPos.Add(new Vector3(x, 0, z) * 0.5f + waterPivot);
                    idx++;
                }
            }

            _request.SetNewPositions(worldPos);
            WaterSystem.TryGetWaterSurfaceData(_request);
        }


        public static void DebugBuoyancy()
        {
            
            for (int i = 0; i < _request.Result.Count; i++)
            {
                Gizmos.DrawCube(_request.Result[i].Position, Vector3.one * 0.1f);
               // Gizmos.DrawRay(_request.Result[i].position, Vector3.up * 0.25f);
            }

            ///////////////////////////////////////////////////////////////////////////

            //Debug.Log(WaterSystem.GetWaterSurfaceData(new Vector3(10, 0, 10)).Position);
        }

        public static void DebugQuadtree(WaterSystem waterInstance)
        {
            var quadTreeInstance = waterInstance._meshQuadTree.Instances[Camera.main];
            
            foreach (var visibleNode in quadTreeInstance.VisibleGPUChunks)
            {
                var center = visibleNode.Position;
                var size   = visibleNode.Size;
                center.y -= size.y * 0.5f;
                Gizmos.DrawWireCube(center, size);
            }
            
            Debug.Log($"Visible nodes {quadTreeInstance.VisibleNodes.Count}");
        }
     
      
        public static void Release()
        {
            KW_Extensions.SafeDestroy(_debugTextureSliceMaterial);
        }

    }
}
