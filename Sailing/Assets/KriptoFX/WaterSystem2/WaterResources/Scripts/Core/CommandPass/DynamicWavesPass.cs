//#define DEBUG_SIMULATION

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static KWS.KWS_DynamicWavesSimulationZone;

#if KWS_URP
using UnityEngine.Rendering.Universal;
#endif

#if KWS_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace KWS
{

    internal class DynamicWavesPass : WaterPass
    {
        private const string FoamParticlesShaderName   = "Hidden/KriptoFX/KWS/KWS_DynamicWavesFoamParticles";
        private const string SplashParticlesShaderName = "Hidden/KriptoFX/KWS/KWS_DynamicWavesSplashParticles";

        private const string FoamComputeShaderKeyword   = "KWS_FOAM_MODE";
        private const string SplashComputeShaderKeyword = "KWS_SPLASH_MODE";


        private static List<KWS_DynamicWavesObject>                       _interactScriptsInArea       = new();
        private static List<KWS_DynamicWavesObject.DynamicWaveDataStruct> _visibleInteractionSpheres   = new();
        private static List<KWS_DynamicWavesObject.DynamicWaveDataStruct> _visibleInteractionCubes     = new();
        private static List<KWS_DynamicWavesObject.DynamicWaveDataStruct> _visibleInteractionTriangles = new();

        private static Material      _dynamicWavesMaterial;
        private static ComputeBuffer _computeBufferDynamicWavesMask;
        
        private static ComputeBuffer _defaultComputeBufferFoam;
        private static ComputeBuffer _defaultComputeBufferSplash;

        private static Dictionary<int, int> _dynamicWavesTextureNames               = new();
        private static Dictionary<int, int> _dynamicWavesAdditionalDataTextureNames = new();
        private static Dictionary<int, int> _dynamicWavesNormalTextureNames         = new();
        private static Dictionary<int, int> _dynamicWavesColorNames                 = new();
        private static Dictionary<int, int> _dynamicWavesDepthMaskNames             = new();

        private static Vector4[] _dynamicWavesZonePositions         = new Vector4[KWS_TileZoneManager.MaxVisibleZones];
        private static Vector4[] _dynamicWavesZoneSizes             = new Vector4[KWS_TileZoneManager.MaxVisibleZones];
        private static Vector4[] _dynamicWavesOrthoDepthNearFarSize = new Vector4[KWS_TileZoneManager.MaxVisibleZones];
        private static Vector4[] _dynamicWavesRotationMatrixes      = new Vector4[KWS_TileZoneManager.MaxVisibleZones];

        private static Dictionary<SplashParticlesMaxLimitEnum, float> _normalizedBudget = new()
        {
            { SplashParticlesMaxLimitEnum._50k, 1 },
            { SplashParticlesMaxLimitEnum._25k, 0.75f },
            { SplashParticlesMaxLimitEnum._15k, 0.5f },
            { SplashParticlesMaxLimitEnum._5k, 0 }
        };

        private static readonly int KwsDynamicWavesTimeScale                    = Shader.PropertyToID("KWS_DynamicWavesTimeScale");
        private static readonly int KwsDynamicWavesFlowSpeedMultiplier          = Shader.PropertyToID("KWS_DynamicWavesFlowSpeedMultiplier");
        private static readonly int KwsFoamStrengthRiver                        = Shader.PropertyToID("KWS_FoamStrengthRiver");
        private static readonly int KwsFoamStrengthShoreline                    = Shader.PropertyToID("KWS_FoamStrengthShoreline");
        private static readonly int KwsFoamParticlesEmissionRateRiver           = Shader.PropertyToID("KWS_FoamParticlesEmissionRateRiver");
        private static readonly int KwsFoamParticlesEmissionRateShoreline       = Shader.PropertyToID("KWS_FoamParticlesEmissionRateShoreline");
        private static readonly int KwsSplashParticlesEmissionRateRiver         = Shader.PropertyToID("KWS_SplashParticlesEmissionRateRiver");
        private static readonly int KwsSplashParticlesEmissionRateShoreline     = Shader.PropertyToID("KWS_SplashParticlesEmissionRateShoreline");
        private static readonly int KwsWaterfallEmissionRateSplash              = Shader.PropertyToID("KWS_WaterfallEmissionRateSplash");
        private static readonly int KwsDynamicWavesUseWaterIntersection         = Shader.PropertyToID("KWS_DynamicWavesUseWaterIntersection");
        private static readonly int KwsDynamicWavesZoneInteractionType          = Shader.PropertyToID("KWS_DynamicWavesZoneInteractionType");
        private static readonly int KwsCurrentFrame                             = Shader.PropertyToID("KWS_CurrentFrame");
        private static readonly int KwsDynamicWavesZonePositionArray            = Shader.PropertyToID("KWS_DynamicWavesZonePositionArray");
        private static readonly int KwsDynamicWavesZoneSizeArray                = Shader.PropertyToID("KWS_DynamicWavesZoneSizeArray");
        private static readonly int KwsDynamicWavesOrthoDepthNearFarSizeArray   = Shader.PropertyToID("KWS_DynamicWavesOrthoDepthNearFarSizeArray");
        private static readonly int KwsDynamicWavesZoneRotationMatrixArray      = Shader.PropertyToID("KWS_DynamicWavesZoneRotationMatrixArray");
        private static readonly int KwsDynamicWavesZonePositionMovable          = Shader.PropertyToID("KWS_DynamicWavesZonePositionMovable");
        private static readonly int KwsDynamicWavesZoneSizeMovable              = Shader.PropertyToID("KWS_DynamicWavesZoneSizeMovable");
        private static readonly int KwsDynamicWavesZoneBoundsMovable            = Shader.PropertyToID("KWS_DynamicWavesZoneBoundsMovable");
        private static readonly int KwsDynamicWavesOrthoDepthNearFarSizeMovable = Shader.PropertyToID("KWS_DynamicWavesOrthoDepthNearFarSizeMovable");
        private static readonly int KwsMovableZoneFlowSpeedMultiplier           = Shader.PropertyToID("KWS_MovableZoneFlowSpeedMultiplier");
        private static readonly int KwsDynamicWavesMovable                      = Shader.PropertyToID("KWS_DynamicWavesMovable");
        private static readonly int KwsDynamicWavesNormalsMovable               = Shader.PropertyToID("KWS_DynamicWavesNormalsMovable");
        private static readonly int KwsDynamicWavesAdditionalDataRTMovable      = Shader.PropertyToID("KWS_DynamicWavesAdditionalDataRTMovable");
        private static readonly int KwsDynamicWavesDepthMaskMovable             = Shader.PropertyToID("KWS_DynamicWavesDepthMaskMovable");
        private static readonly int KwsDynamicWavesZonePosition                 = Shader.PropertyToID("KWS_DynamicWavesZonePosition");
        private static readonly int KwsDynamicWavesZoneSize                     = Shader.PropertyToID("KWS_DynamicWavesZoneSize");
        private static readonly int KwsDynamicWavesZoneRotationMatrix           = Shader.PropertyToID("KWS_DynamicWavesZoneRotationMatrix");
        private static readonly int KwsTilesCount                               = Shader.PropertyToID("KWS_TilesCount");
        private static readonly int KwsCounterBuffer                            = Shader.PropertyToID("KWS_CounterBuffer");
        private static readonly int KwsDispatchIndirectArgs                     = Shader.PropertyToID("KWS_DispatchIndirectArgs");
        private static readonly int KwsParticlesIndirectArgs                    = Shader.PropertyToID("KWS_ParticlesIndirectArgs");
        private static readonly int KwsDeltaTime                                = Shader.PropertyToID("KWS_deltaTime");
        private static readonly int KwsDistancePerPixel                         = Shader.PropertyToID("KWS_DistancePerPixel");
        private static readonly int MaxParticles                                = Shader.PropertyToID("maxParticles");
        private static readonly int KwsWorldSpaceCameraPos                      = Shader.PropertyToID("KWS_WorldSpaceCameraPos");
        private static readonly int KwsCameraForward                            = Shader.PropertyToID("KWS_CameraForward");
        private static readonly int KwsParticlesTimeSlicingFrame                = Shader.PropertyToID("KWS_ParticlesTimeSlicingFrame");
        private static readonly int KwsSplashParticlesBudgetNormalized          = Shader.PropertyToID("KWS_SplashParticlesBudgetNormalized");
        private static readonly int KwsCurrentScreenSize                        = Shader.PropertyToID("KWS_CurrentScreenSize");
        private static readonly int KwsUsePhytoplanktonEmission                 = Shader.PropertyToID("KWS_UsePhytoplanktonEmission");
        private static readonly int KwsTileParticleCount                        = Shader.PropertyToID("KWS_TileParticleCount");
        private static readonly int KwsFoamParticlesBuffer                      = Shader.PropertyToID("KWS_FoamParticlesBuffer");
        private static readonly int KwsParticlesFoamInterpolationTime           = Shader.PropertyToID("KWS_ParticlesFoamInterpolationTime");
        private static readonly int KwsFoamParticlesScale                       = Shader.PropertyToID("KWS_FoamParticlesScale");
        private static readonly int KwsFoamParticlesAlphaMultiplier             = Shader.PropertyToID("KWS_FoamParticlesAlphaMultiplier");
        private static readonly int KwsSplashParticlesBuffer                    = Shader.PropertyToID("KWS_SplashParticlesBuffer");
        private static readonly int KwsSplashParticlesScale                     = Shader.PropertyToID("KWS_SplashParticlesScale");
        private static readonly int KwsParticlesSplashInterpolationTime         = Shader.PropertyToID("KWS_ParticlesSplashInterpolationTime");
        private static readonly int KwsSplashParticlesAlphaMultiplier           = Shader.PropertyToID("KWS_SplashParticlesAlphaMultiplier");

        private CommandBuffer _cmd;

        private Mesh _cubeMesh;

        private KWS_DynamicWavesSimulationZone _currentBakingZone;
        private bool                           _isBakingMode;
        private Mesh                           _sphereMesh;
        private Mesh                           _triangleMesh;
        private GameObject                     _wetDecalObject;
        #if KWS_URP || KWS_HDRP
        private DecalProjector _wetDecalProjector;
        #endif
        
        private Material                       _wetMaterial;

        private int ID_KWS_FoamParticlesBuffer  = Shader.PropertyToID("KWS_FoamParticlesBuffer");
        private int ID_KWS_FoamParticlesBuffer1 = Shader.PropertyToID("KWS_FoamParticlesBuffer1");
        private int ID_KWS_FoamParticlesBuffer2 = Shader.PropertyToID("KWS_FoamParticlesBuffer2");

        private int ID_KWS_SplashParticlesBuffer  = Shader.PropertyToID("KWS_SplashParticlesBuffer");
        private int ID_KWS_SplashParticlesBuffer1 = Shader.PropertyToID("KWS_SplashParticlesBuffer1");
        private int ID_KWS_SplashParticlesBuffer2 = Shader.PropertyToID("KWS_SplashParticlesBuffer2");


        public DynamicWavesPass()
        {
            KWS_TileZoneManager.OnDynamicWavesBakingStateChanged += OnDynamicWavesBakingStateChanged;
            _dynamicWavesMaterial                                =  KWS_CoreUtils.CreateMaterial(KWS_ShaderConstants.ShaderNames.DynamicWavesShaderName);
            InitializeTextureNames();
            SetDefaultBuffers();
        }

        internal override string PassName => "Water.DynamicWavesPass";

        public override void Release()
        {
            KWS_TileZoneManager.OnDynamicWavesBakingStateChanged -= OnDynamicWavesBakingStateChanged;
            _computeBufferDynamicWavesMask?.Release();
            _computeBufferDynamicWavesMask = null;
            
            _defaultComputeBufferFoam?.Release();
            _defaultComputeBufferFoam = null;
            
            _defaultComputeBufferSplash?.Release();
            _defaultComputeBufferSplash = null;

            KW_Extensions.SafeDestroy(_dynamicWavesMaterial, _cubeMesh, _sphereMesh, _triangleMesh, _wetDecalObject);
            _dynamicWavesMaterial = null;
            _wetDecalObject       = null;
           
#if !KWS_HDRP && !KWS_URP
            KW_Extensions.SafeDestroy(_wetMaterial);
            _wetMaterial = null;
 #endif

            this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.Release);
        }

        private void OnDynamicWavesBakingStateChanged(KWS_DynamicWavesSimulationZone zone, bool state)
        {
            _currentBakingZone = zone;
            _isBakingMode      = state;
        }

        void SetDefaultBuffers()
        {
            KWS_CoreUtils.SetFallbackBuffer<KWS_DynamicWavesObject.DynamicWaveDataStruct>(ref _computeBufferDynamicWavesMask, KWS_ShaderConstants.StructuredBuffers.KWS_DynamicWavesMaskBuffer);
            KWS_CoreUtils.SetFallbackBuffer<KWS_DynamicWavesHelpers.FoamParticle>(ref _defaultComputeBufferFoam, KwsFoamParticlesBuffer);
            KWS_CoreUtils.SetFallbackBuffer<KWS_DynamicWavesHelpers.SplashParticle>(ref _defaultComputeBufferSplash, KwsFoamParticlesBuffer);
        }
        
        private void InitializeTextureNames()
        {
            _dynamicWavesTextureNames.Clear();
            _dynamicWavesNormalTextureNames.Clear();
            _dynamicWavesAdditionalDataTextureNames.Clear();
            _dynamicWavesColorNames.Clear();
            _dynamicWavesDepthMaskNames.Clear();

            for (var i = 0; i < KWS_TileZoneManager.MaxVisibleZones; i++)
            {
                _dynamicWavesTextureNames.Add(i, Shader.PropertyToID("KWS_DynamicWaves"                               + i));
                _dynamicWavesNormalTextureNames.Add(i, Shader.PropertyToID("KWS_DynamicWavesNormals"                  + i));
                _dynamicWavesAdditionalDataTextureNames.Add(i, Shader.PropertyToID("KWS_DynamicWavesAdditionalDataRT" + i));
                _dynamicWavesColorNames.Add(i, Shader.PropertyToID("KWS_DynamicWavesColorDataRT"                      + i));
                _dynamicWavesDepthMaskNames.Add(i, Shader.PropertyToID("KWS_DynamicWavesDepthMask"                    + i));
            }
        }

        public override void ExecutePerFrame(HashSet<Camera> cameras, CustomFixedUpdates fixedUpdates)
        {
            
            var cam = KWS_CoreUtils.GetFixedUpdateCamera(cameras);
            if (cam == null) return;
            
            if (WaterSystem.QualitySettings.UseWetEffect) UpdateWetDecal(cam);

            if (KWS_TileZoneManager.DynamicWavesZones.Count == 0) return;

         
            foreach (var iZone in KWS_TileZoneManager.DynamicWavesZones)
            {
                if (!iZone.IsZoneVisible) continue;

                var zone = (KWS_DynamicWavesSimulationZone)iZone;
                if (zone.UseFoamParticles) zone.SimulationData.FoamParticlesData.UpdateInterpolationTime(fixedUpdates.FramesCount_60fps, zone.MaxSkippedFrames, true);
                if (zone.UseSplashParticles) zone.SimulationData.SplashParticlesData.UpdateInterpolationTime(fixedUpdates.FramesCount_60fps, zone.MaxSkippedFrames, false);
                UpdateShaderTexturesByID(zone);
            }
            
            if (fixedUpdates.FramesCount_60fps == 0) return;

#if !DEBUG_SIMULATION
            if (_cmd == null) _cmd = new CommandBuffer { name = PassName };
            _cmd.Clear();

            UpdateSimulation(_cmd, cam, fixedUpdates.FramesCount_60fps, KWS_TileZoneManager.DynamicWavesZones);

            Graphics.ExecuteCommandBuffer(_cmd);
#endif
        }

        private void UpdateSimulation(CommandBuffer cmd, Camera cam, int frames, List<KWS_TileZoneManager.IWaterZone> simZones)
        {
            if (_isBakingMode)
            {
                frames = (int)(frames * WaterSystem.GlobalTimeScale);
                Shader.SetGlobalFloat(KwsDynamicWavesTimeScale, WaterSystem.GlobalTimeScale);
                UpdateZone(cmd, cam, frames, 0, _currentBakingZone);
            }
            else
            {
                Shader.SetGlobalFloat(KwsDynamicWavesTimeScale, 1);
                foreach (var iZone in simZones)
                {
                    var zone = (KWS_DynamicWavesSimulationZone)iZone;
                    if (zone.CanRender(cam)) UpdateZone(cmd, cam, frames + zone.MaxSkippedFrames, zone.MaxSkippedFrames, zone);
                }
            }
        }


        private void UpdateZone(CommandBuffer cmd, Camera cam, int frames, int skippedFrames, KWS_DynamicWavesSimulationZone zone)
        {
            cmd.SetGlobalFloat(KwsDynamicWavesFlowSpeedMultiplier,      zone.FlowSpeedMultiplier);
            cmd.SetGlobalFloat(KwsFoamStrengthRiver,                    zone.FoamStrengthRiver);
            cmd.SetGlobalFloat(KwsFoamStrengthShoreline,                zone.FoamStrengthShoreline);
            cmd.SetGlobalFloat(KwsFoamParticlesEmissionRateRiver,       zone.RiverEmissionRateFoam);
            cmd.SetGlobalFloat(KwsFoamParticlesEmissionRateShoreline,   zone.ShorelineEmissionRateFoam);
            cmd.SetGlobalFloat(KwsSplashParticlesEmissionRateRiver,     zone.RiverEmissionRateSplash);
            cmd.SetGlobalFloat(KwsSplashParticlesEmissionRateShoreline, zone.ShorelineEmissionRateSplash);
            cmd.SetGlobalFloat(KwsWaterfallEmissionRateSplash,          zone.WaterfallEmissionRateSplash);

            var zoneOffset = Vector3.zero;
            if (zone.ZoneType == SimulationZoneTypeMode.MovableZone)
            {
                KWS_TileZoneManager.MovableZone.UpdateMovableZoneTransform();
                zoneOffset = GetMovableZoneOffset(zone);
            }

            UpdateSimulationDataShaderParams(cmd, zone);

            if (zone.ZoneType != SimulationZoneTypeMode.BakedSimulation)
            {
                DrawIntersectionMask(cmd, frames, zone);
                ExecuteDynamicWaves(cmd, frames, zoneOffset, zone);
            }
            else
            {
                if (!Application.isPlaying || (Application.isPlaying && zone.SimulationData.CurrentFrame < 60 * 5))
                {
                    DrawIntersectionMask(cmd, frames, zone);
                    ExecuteDynamicWaves(cmd, frames, zoneOffset, zone);
                }
            }

            if (zone.UseFoamParticles) ExecuteFoamParticles(cmd, cam, frames     + 0, zone);
            if (zone.UseSplashParticles) ExecuteSplashParticles(cmd, cam, frames + 0, zone);

#if KWS_DEBUG
            WaterSharedResources.DynamicWavesRT               = zone.SimulationData.GetTarget;
            WaterSharedResources.DynamicWavesAdditionalDataRT = zone.SimulationData.GetAdditionalTarget;
            WaterSharedResources.DynamicWavesMaskRT           = zone.SimulationData.DynamicWavesMask;

#endif

            if (zone.ZoneType == SimulationZoneTypeMode.MovableZone) UpdateMovableShaderTextures(KWS_TileZoneManager.MovableZone);
        }

        public override void ExecuteBeforeCameraRendering(Camera cam, ScriptableRenderContext context)
        {
           
#if DEBUG_SIMULATION
            if (_cmd == null) _cmd = new CommandBuffer() { name = PassName };
            _cmd.Clear();

            UpdateSimulation(_cmd, cam, 1, KWS_TileZoneManager.DynamicWavesZones);

            Graphics.ExecuteCommandBuffer(_cmd);
#endif
            ExecuteParticles(cam);
        }

        public override void ExecuteCommandBuffer(WaterPassContext waterContext)
        {
            ExecuteWetMap(waterContext);
        }

        public void DrawMeshInstancedProcedural(CommandBuffer cmd, Mesh mesh, List<KWS_DynamicWavesObject.DynamicWaveDataStruct> objects)
        {
            MeshUtils.InitializePropertiesBuffer(cmd, objects, ref _computeBufferDynamicWavesMask, false);
            cmd.SetGlobalBuffer(KWS_ShaderConstants.StructuredBuffers.KWS_DynamicWavesMaskBuffer, _computeBufferDynamicWavesMask);
            cmd.DrawMeshInstancedProcedural(mesh, 0, _dynamicWavesMaterial, 1, objects.Count);
        }


        internal void DrawIntersectionMask(CommandBuffer cmd, int frames, KWS_DynamicWavesSimulationZone zone)
        {
            var simulationData = zone.SimulationData;

            var interactScripts = GetInteractScriptsInArea(zone.Position, zone.Size, zone.Rotation);
            KWS_CoreUtils.SetOrthoMatrix_VP(cmd, zone.Size, zone.Position, zone.transform.rotation);

            _visibleInteractionCubes.Clear();
            _visibleInteractionSpheres.Clear();
            _visibleInteractionTriangles.Clear();

            if (!_cubeMesh) _cubeMesh         = MeshUtils.CreateCubeMesh();
            if (!_sphereMesh) _sphereMesh     = MeshUtils.CreateSphereMesh(0.5f, 7, 4);
            if (!_triangleMesh) _triangleMesh = MeshUtils.CreateTriangle(1);

            foreach (var instance in interactScripts) instance.CustomUpdate(frames);


            if (KWS_TileZoneManager.IsAnyDynamicWavesUseColorMode)
            {
                if (simulationData.DynamicWavesMaskColor == null || simulationData.DynamicWavesMaskColor.rt == null) simulationData.InitializeSimTexturesColor();

                CoreUtils.SetRenderTarget(cmd, simulationData.DynamicWavesMaskColor, ClearFlag.Color, Color.clear);

                CoreUtils.SetRenderTarget(cmd, simulationData.DynamicWavesMask,      ClearFlag.Color, new Color(0.5f, 0.5f, 0.5f, 0));
                CoreUtils.SetRenderTarget(cmd, simulationData.DynamicWavesMaskDepth, ClearFlag.Depth, new Color(0.5f, 0.5f, 0.5f, 0));

                CoreUtils.SetRenderTarget(cmd, KWS_CoreUtils.GetMrt(simulationData.DynamicWavesMask, simulationData.DynamicWavesMaskColor), simulationData.DynamicWavesMaskDepth);
            }
            else
            {
                CoreUtils.SetRenderTarget(cmd, simulationData.DynamicWavesMask, simulationData.DynamicWavesMaskDepth, ClearFlag.All, new Color(0.5f, 0.5f, 0.5f, 0));
            }


            foreach (var instance in interactScripts)
            {
                if (instance.InteractionType == KWS_DynamicWavesObject.InteractionTypeEnum.ObstacleObject)
                {
                    if (!instance.CurrentMesh) continue;
                    
                    cmd.SetGlobalFloat(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesWaterSurfaceHeight, instance.DynamicWaveData.WaterHeight);
                    cmd.SetGlobalFloat(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesForce,              instance.DynamicWaveData.Force);
                    cmd.SetGlobalVector(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesForceDirection,     instance.DynamicWaveData.ForceDirection);
                    cmd.SetGlobalInteger(KwsDynamicWavesUseWaterIntersection, (int)instance.DynamicWaveData.UseWaterIntersection);
                    cmd.SetGlobalInteger(KwsDynamicWavesZoneInteractionType,  (int)instance.DynamicWaveData.ZoneInteractionType);
                    cmd.DrawMesh(instance.CurrentMesh, instance.DynamicWaveData.MatrixTRS, _dynamicWavesMaterial, 0, 0);
                }
                else
                {
                    switch (instance.ForceType)
                    {
                        case KWS_DynamicWavesObject.ForceTypeEnum.Sphere:
                            _visibleInteractionSpheres.Add(instance.DynamicWaveData);
                            break;
                        case KWS_DynamicWavesObject.ForceTypeEnum.Box:  
                            _visibleInteractionCubes.Add(instance.DynamicWaveData);
                            break;
                        case KWS_DynamicWavesObject.ForceTypeEnum.Triangle: 
                            _visibleInteractionTriangles.Add(instance.DynamicWaveData);
                            break;
                    }
                }
            }
             
            if (_visibleInteractionSpheres.Count   > 0) DrawMeshInstancedProcedural(cmd, _sphereMesh,   _visibleInteractionSpheres);
            if (_visibleInteractionCubes.Count     > 0) DrawMeshInstancedProcedural(cmd, _cubeMesh,     _visibleInteractionCubes);
            if (_visibleInteractionTriangles.Count > 0) DrawMeshInstancedProcedural(cmd, _triangleMesh, _visibleInteractionTriangles);
        }

        private Vector3 GetMovableZoneOffset(KWS_DynamicWavesSimulationZone zone)
        {
            var areaSize = zone.Size;
            var offset   = Vector3.zero;

            offset                            =  zone.transform.position - zone._lastRenderedDynamicPosition;
            offset.x                          /= areaSize.x;
            offset.z                          /= areaSize.z;
            zone._lastRenderedDynamicPosition =  zone.transform.position;
            return offset;
        }


        private void ExecuteDynamicWaves(CommandBuffer cmd, int fpsFrames, Vector3 worldOffsetFromTheLastFrame, KWS_DynamicWavesSimulationZone zone)
        {
            var data = zone.SimulationData;

            // cmd.SetKeyword("KW_USE_RAIN_EFFECT", WaterSystem.QualitySettings.UseDynamicWavesRainEffect);
            cmd.SetKeyword("KWS_DYNAMIC_WAVES_USE_MOVABLE_ZONE", zone.ZoneType == SimulationZoneTypeMode.MovableZone);

            //if (settings.UseDynamicWavesRainEffect) cmd.SetGlobalFloat("KWS_DynamicWavesRainStrength", settings.DynamicWavesRainStrength);

            var currentOffset = worldOffsetFromTheLastFrame;

            for (var i = 0; i < fpsFrames; i++)
            {
                cmd.SetGlobalInteger(KwsCurrentFrame, data.CurrentFrame);

                cmd.SetGlobalVector(KWS_ShaderConstants.DynamicWaves.KW_AreaOffset, currentOffset);
                cmd.SetGlobalTexture(KWS_ShaderConstants.DynamicWaves.KWS_CurrentTarget,           data.DynamicWaves2);
                cmd.SetGlobalTexture(KWS_ShaderConstants.DynamicWaves.KWS_CurrentAdditionalTarget, data.GetAdditionalTarget);
                if (data.DynamicWavesMaskColor != null) cmd.SetGlobalTexture(KWS_ShaderConstants.DynamicWaves.KWS_CurrentColorTarget, data.GetColorTarget);

                if (data.DynamicWavesColorData1 != null)
                    CoreUtils.SetRenderTarget(cmd, KWS_CoreUtils.GetMrt(data.DynamicWaves1, data.DynamicWavesNormals, data.GetAdditionalTargetNext, data.GetColorTargetNext), data.DynamicWaves1);
                else
                    CoreUtils.SetRenderTarget(cmd, KWS_CoreUtils.GetMrt(data.DynamicWaves1, data.DynamicWavesNormals, data.GetAdditionalTargetNext), data.DynamicWaves1);
                cmd.BlitTriangle(_dynamicWavesMaterial, 2);

                currentOffset = Vector4.zero;

                cmd.SetGlobalVector(KWS_ShaderConstants.DynamicWaves.KW_AreaOffset, currentOffset);
                cmd.SetGlobalTexture(KWS_ShaderConstants.DynamicWaves.KWS_CurrentTarget, data.DynamicWaves1);

                CoreUtils.SetRenderTarget(cmd, data.DynamicWaves2);
                cmd.BlitTriangle(_dynamicWavesMaterial, 3);

                data.SwapSimulationBuffers();
            }
        }

        internal static void UpdateShaderTexturesByID(KWS_DynamicWavesSimulationZone zone)
        {
            var simData = zone.SimulationData;
            var id      = ((KWS_TileZoneManager.IWaterZone)zone).ID;

            _dynamicWavesZoneSizes[id]             = zone.Size;
            _dynamicWavesZonePositions[id]         = zone.Position;
            _dynamicWavesOrthoDepthNearFarSize[id] = zone.BakedNearFarSizeXZ;
            _dynamicWavesRotationMatrixes[id]      = zone.RotationMatrix;

            Shader.SetGlobalVectorArray(KwsDynamicWavesZonePositionArray,          _dynamicWavesZonePositions);
            Shader.SetGlobalVectorArray(KwsDynamicWavesZoneSizeArray,              _dynamicWavesZoneSizes);
            Shader.SetGlobalVectorArray(KwsDynamicWavesOrthoDepthNearFarSizeArray, _dynamicWavesOrthoDepthNearFarSize);
            Shader.SetGlobalVectorArray(KwsDynamicWavesZoneRotationMatrixArray,    _dynamicWavesRotationMatrixes);

            Shader.SetGlobalTexture(_dynamicWavesTextureNames[id],               simData.GetTarget);
            Shader.SetGlobalTexture(_dynamicWavesNormalTextureNames[id],         simData.GetNormals);
            Shader.SetGlobalTexture(_dynamicWavesAdditionalDataTextureNames[id], simData.GetAdditionalTarget);
            Shader.SetGlobalTexture(_dynamicWavesDepthMaskNames[id],             simData.DynamicWavesMaskDepth);

            if (KWS_TileZoneManager.IsAnyDynamicWavesUseColorMode) Shader.SetGlobalTexture(_dynamicWavesColorNames[id], simData.GetColorTarget);
        }


        internal static void UpdateMovableShaderTextures(KWS_DynamicWavesSimulationZone zone)
        {
            // KWS_DynamicWavesMovable
            var simData    = zone.SimulationData;
            var zoneBounds = zone.Bounds;
            var dataBounds = new Vector4(zoneBounds.min.x, zoneBounds.min.z, zoneBounds.max.x, zoneBounds.max.z);

            Shader.SetGlobalVector(KwsDynamicWavesZonePositionMovable,          zone.Position);
            Shader.SetGlobalVector(KwsDynamicWavesZoneSizeMovable,              zone.Size);
            Shader.SetGlobalVector(KwsDynamicWavesZoneBoundsMovable,            dataBounds);
            Shader.SetGlobalVector(KwsDynamicWavesOrthoDepthNearFarSizeMovable, zone.BakedNearFarSizeXZ);
            Shader.SetGlobalFloat(KwsMovableZoneFlowSpeedMultiplier, zone.FlowSpeedMultiplier);

            Shader.SetGlobalTexture(KwsDynamicWavesMovable,                 simData.GetTarget);
            Shader.SetGlobalTexture(KwsDynamicWavesNormalsMovable,          simData.GetNormals);
            Shader.SetGlobalTexture(KwsDynamicWavesAdditionalDataRTMovable, simData.GetAdditionalTarget);
            Shader.SetGlobalTexture(KwsDynamicWavesDepthMaskMovable,        simData.DynamicWavesMaskDepth);
        }


        internal static void SetDefaultComputeShaderTextures(CommandBuffer cmd, ComputeShader cs, int kernelIndex)
        {
            for (var i = 0; i < KWS_TileZoneManager.MaxVisibleZones; i++)
            {
                cmd.SetComputeTextureParam(cs, kernelIndex, _dynamicWavesTextureNames[i],               KWS_CoreUtils.DefaultGrayTexture);
                cmd.SetComputeTextureParam(cs, kernelIndex, _dynamicWavesNormalTextureNames[i],         KWS_CoreUtils.DefaultGrayTexture);
                cmd.SetComputeTextureParam(cs, kernelIndex, _dynamicWavesAdditionalDataTextureNames[i], KWS_CoreUtils.DefaultGrayTexture);
                cmd.SetComputeTextureParam(cs, kernelIndex, _dynamicWavesDepthMaskNames[i],             KWS_CoreUtils.DefaultGrayTexture);
                cmd.SetComputeTextureParam(cs, kernelIndex, _dynamicWavesColorNames[i],                 KWS_CoreUtils.DefaultGrayTexture);

                cmd.SetComputeTextureParam(cs, kernelIndex, KwsDynamicWavesMovable,                 KWS_CoreUtils.DefaultGrayTexture);
                cmd.SetComputeTextureParam(cs, kernelIndex, KwsDynamicWavesAdditionalDataRTMovable, KWS_CoreUtils.DefaultGrayTexture);
                cmd.SetComputeTextureParam(cs, kernelIndex, KwsDynamicWavesDepthMaskMovable,        KWS_CoreUtils.DefaultGrayTexture);
            }
        }

        internal static void UpdateShaderTexturesByID(int id, KWS_DynamicWavesSimulationZone zone, CommandBuffer cmd, ComputeShader cs, int kernelIndex)
        {
            var simData = zone.SimulationData;
            _dynamicWavesZoneSizes[id]             = zone.Size;
            _dynamicWavesZonePositions[id]         = zone.Position;
            _dynamicWavesOrthoDepthNearFarSize[id] = zone.BakedNearFarSizeXZ;
            _dynamicWavesRotationMatrixes[id]      = zone.RotationMatrix;

            cmd.SetComputeVectorArrayParam(cs, KwsDynamicWavesZonePositionArray,          _dynamicWavesZonePositions);
            cmd.SetComputeVectorArrayParam(cs, KwsDynamicWavesZoneSizeArray,              _dynamicWavesZoneSizes);
            cmd.SetComputeVectorArrayParam(cs, KwsDynamicWavesOrthoDepthNearFarSizeArray, _dynamicWavesOrthoDepthNearFarSize);
            cmd.SetComputeVectorArrayParam(cs, KwsDynamicWavesZoneRotationMatrixArray,    _dynamicWavesRotationMatrixes);

            cmd.SetComputeTextureParam(cs, kernelIndex, _dynamicWavesTextureNames[id],               simData.GetTarget);
            cmd.SetComputeTextureParam(cs, kernelIndex, _dynamicWavesNormalTextureNames[id],         simData.GetNormals);
            cmd.SetComputeTextureParam(cs, kernelIndex, _dynamicWavesAdditionalDataTextureNames[id], simData.GetAdditionalTarget);
            cmd.SetComputeTextureParam(cs, kernelIndex, _dynamicWavesDepthMaskNames[id],             simData.DynamicWavesMaskDepth);
            if (KWS_TileZoneManager.IsAnyDynamicWavesUseColorMode) cmd.SetComputeTextureParam(cs, kernelIndex, _dynamicWavesColorNames[id], simData.GetColorTarget);
        }

        internal static void UpdateMovableShaderTexturesByID(int id, KWS_DynamicWavesSimulationZone zone, CommandBuffer cmd, ComputeShader cs, int kernelIndex)
        {
            cmd.SetComputeTextureParam(cs, kernelIndex, KwsDynamicWavesMovable,                 zone.SimulationData.GetTarget);
            cmd.SetComputeTextureParam(cs, kernelIndex, KwsDynamicWavesAdditionalDataRTMovable, zone.SimulationData.GetAdditionalTarget);
            cmd.SetComputeTextureParam(cs, kernelIndex, KwsDynamicWavesDepthMaskMovable,        zone.SimulationData.DynamicWavesMaskDepth);
        }

        private void UpdateSimulationDataShaderParams(CommandBuffer cmd, KWS_DynamicWavesSimulationZone zone)
        {
            var simData = zone.SimulationData;

            cmd.SetGlobalVector(KwsDynamicWavesZonePosition,       zone.Position);
            cmd.SetGlobalVector(KwsDynamicWavesZoneSize,           zone.Size);
            cmd.SetGlobalVector(KwsDynamicWavesZoneRotationMatrix, zone.RotationMatrix);

            cmd.SetGlobalTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesMaskRT,    simData.DynamicWavesMask);
            cmd.SetGlobalTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesDepthMask, simData.DynamicWavesMaskDepth);

            cmd.SetGlobalTexture(KWS_ShaderConstants.OrthoDepth.KWS_WaterOrthoDepthRT,  simData.Depth);
            cmd.SetGlobalTexture(KWS_ShaderConstants.OrthoDepth.KWS_WaterOrthoDepthSDF, simData.DepthSDF);

            cmd.SetGlobalVector(KWS_ShaderConstants.OrthoDepth.KWS_OrthoDepthPos,         zone.Position);
            cmd.SetGlobalVector(KWS_ShaderConstants.OrthoDepth.KWS_OrthoDepthNearFarSize, zone.BakedNearFarSizeXZ);

            cmd.SetGlobalTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWaves,                 simData.GetTarget);
            cmd.SetGlobalTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesNormals,          simData.GetNormals);
            cmd.SetGlobalTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesAdditionalDataRT, simData.GetAdditionalTarget);

            if (KWS_TileZoneManager.IsAnyDynamicWavesUseColorMode) cmd.SetGlobalTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesMaskColorRT, simData.DynamicWavesMaskColor);
        }

        private void UpdateSimulationDataShaderParams(Material mat, KWS_DynamicWavesSimulationZone zone)
        {
            var simData = zone.SimulationData;

            var angleRad  = zone.transform.rotation.eulerAngles.y * Mathf.Deg2Rad;
            var cos       = Mathf.Cos(angleRad);
            var sin       = Mathf.Sin(angleRad);
            var rotMatrix = new Vector4(cos, sin, -sin, cos);

            mat.SetVector(KwsDynamicWavesZonePosition,       zone.Position);
            mat.SetVector(KwsDynamicWavesZoneSize,           zone.Size);
            mat.SetVector(KwsDynamicWavesZoneRotationMatrix, rotMatrix);

            mat.SetTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesMaskRT,    simData.DynamicWavesMask);
            mat.SetTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesDepthMask, simData.DynamicWavesMaskDepth);

            mat.SetTexture(KWS_ShaderConstants.OrthoDepth.KWS_WaterOrthoDepthRT,  simData.Depth);
            mat.SetTexture(KWS_ShaderConstants.OrthoDepth.KWS_WaterOrthoDepthSDF, simData.DepthSDF);

            mat.SetVector(KWS_ShaderConstants.OrthoDepth.KWS_OrthoDepthPos,         zone.Position);
            mat.SetVector(KWS_ShaderConstants.OrthoDepth.KWS_OrthoDepthNearFarSize, zone.BakedNearFarSizeXZ);

            mat.SetTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWaves,                 simData.GetTarget);
            mat.SetTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesNormals,          simData.GetNormals);
            mat.SetTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesAdditionalDataRT, simData.GetAdditionalTarget);

            if (KWS_TileZoneManager.IsAnyDynamicWavesUseColorMode) mat.SetTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesMaskColorRT, simData.DynamicWavesMaskColor);
        }

        private void ExecuteFoamParticles(CommandBuffer cmd, Camera cam, int fpsFrames, KWS_DynamicWavesSimulationZone zone)
        {
            var simData       = zone.SimulationData;
            var particlesData = simData.FoamParticlesData;
            var particleCS    = particlesData._particlesComputeShader;
            if (!particleCS) return;

            if (Time.frameCount <= 3) ClearBuffer(cmd, particlesData, particleCS);

            var deltaTime = particlesData.MaxTimeSlices / (60f / (1 + zone.MaxSkippedFrames));

            ParticlesInitKernelData(cmd, cam, zone, particlesData, particleCS, deltaTime);

            var firstSliceFrame = 0;
            var lastSliceFrame  = particlesData.MaxTimeSlices - 1;

            if (particlesData.ParticlesTimeSlicingFrame == firstSliceFrame)
            {
                ParticlesKernelSpawn(cmd, particlesData, particleCS, simData, ID_KWS_FoamParticlesBuffer1, ID_KWS_FoamParticlesBuffer2);
                ParticlesKernelInitGPUDispatch(cmd, particlesData, particleCS);
                ParticlesResetPinPongBuffers(cmd, particlesData);
            }


            ParticlesKernelUpdate(cmd, particlesData, particleCS, simData, ID_KWS_FoamParticlesBuffer, ID_KWS_FoamParticlesBuffer2);


            if (particlesData.ParticlesTimeSlicingFrame == lastSliceFrame)
            {
                ParticlesCopyCounters(cmd, particlesData);
                ParticlesKernelComputeIndirectRenderingArgs(cmd, particlesData, particleCS);
                particlesData.SwapParticlesBuffers();

                particlesData.ParticlesInterpolationTime = 0;
            }

            particlesData.IterateSlice();
        }

        private void ExecuteSplashParticles(CommandBuffer cmd, Camera cam, int fpsFrames, KWS_DynamicWavesSimulationZone zone)
        {
            var simData       = zone.SimulationData;
            var particlesData = simData.SplashParticlesData;
            var particleCS    = particlesData._particlesComputeShader;
            if (!particleCS) return;

            var tileSize   = 64f;
            var tilesCount = Mathf.CeilToInt(cam.pixelWidth / tileSize * (cam.pixelHeight / tileSize));
            particlesData.TileParticleCountBuffer = KWS_CoreUtils.GetOrUpdateBuffer<uint>(ref particlesData.TileParticleCountBuffer, tilesCount, ComputeBufferType.Structured);
            cmd.SetComputeIntParam(particleCS, KwsTilesCount, tilesCount);

            if (Time.frameCount <= 3) ClearBuffer(cmd, particlesData, particleCS);

            var deltaTime = fpsFrames / 60f;

            KWS_CoreUtils.SetAllVPCameraMatrices(cam, cmd, particleCS);

            ParticlesInitKernelData(cmd, cam, zone, particlesData, particleCS, deltaTime);
            ParticlesKernelSpawn(cmd, particlesData, particleCS, simData, ID_KWS_SplashParticlesBuffer1, ID_KWS_SplashParticlesBuffer2);
            ParticlesKernelInitGPUDispatch(cmd, particlesData, particleCS);
            ParticlesResetPinPongBuffers(cmd, particlesData);
            ParticlesKernelUpdate(cmd, particlesData, particleCS, simData, ID_KWS_SplashParticlesBuffer, ID_KWS_SplashParticlesBuffer2);
            ParticlesCopyCounters(cmd, particlesData);
            ParticlesKernelComputeIndirectRenderingArgs(cmd, particlesData, particleCS);

            particlesData.SwapParticlesBuffers();
            particlesData.ParticlesInterpolationTime = 0;
        }


        private void ClearBuffer(CommandBuffer cmd, ParticlesData particlesData, ComputeShader particleCS)
        {
            cmd.SetBufferCounterValue(particlesData.GetCurrentParticlesBuffer,  0);
            cmd.SetBufferCounterValue(particlesData.GetPreviousParticlesBuffer, 0);
            cmd.SetBufferCounterValue(particlesData.GetPreviousParticlesBuffer, 0);

            cmd.SetComputeBufferParam(particleCS, particlesData._kernelClearAll, KwsCounterBuffer,         particlesData.CounterBuffer);
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelClearAll, KwsDispatchIndirectArgs,  particlesData.DispatchIndirectArgs);
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelClearAll, KwsParticlesIndirectArgs, particlesData.ParticlesIndirectArgs);
            cmd.DispatchCompute(particleCS, particlesData._kernelClearAll, 1, 1, 1);
        }


        private void ParticlesInitKernelData(CommandBuffer cmd, Camera cam, KWS_DynamicWavesSimulationZone zone, ParticlesData particlesData, ComputeShader particleCS, float deltaTime)
        {
            cmd.SetComputeFloatParam(particleCS, KwsDynamicWavesFlowSpeedMultiplier, zone.FlowSpeedMultiplier);
            cmd.SetComputeFloatParam(particleCS, KwsFoamStrengthRiver,               zone.FoamStrengthRiver);
            cmd.SetComputeFloatParam(particleCS, KwsFoamStrengthShoreline,           zone.FoamStrengthShoreline);

            cmd.SetComputeFloatParam(particleCS, KwsDeltaTime, deltaTime * Time.timeScale);

            cmd.SetComputeVectorParam(particleCS, KwsDistancePerPixel, new Vector2(2f * zone.Size.x / zone.TextureSize.x, 2f * zone.Size.z / zone.TextureSize.y));
            cmd.SetComputeIntParam(particleCS, MaxParticles, particlesData.MaxParticlesBudget);
            cmd.SetComputeVectorParam(particleCS, KwsWorldSpaceCameraPos, cam.transform.position);
            cmd.SetComputeVectorParam(particleCS, KwsCameraForward,       cam.transform.forward);
            cmd.SetComputeIntParam(particleCS, KwsParticlesTimeSlicingFrame, particlesData.ParticlesTimeSlicingFrame);
            cmd.SetComputeFloatParam(particleCS, KwsSplashParticlesBudgetNormalized, _normalizedBudget[zone.MaxSplashParticlesBudget]);
            cmd.SetComputeIntParam(particleCS, KwsCurrentFrame, particlesData.CurrentFrame);
            cmd.SetComputeVectorParam(particleCS, KwsCurrentScreenSize, new Vector4(cam.pixelWidth, cam.pixelHeight, 0, 0)); //_ScreenParams doesnt works in editor

            cmd.SetComputeIntParam(particleCS, KwsUsePhytoplanktonEmission, zone.UsePhytoplanktonEmission ? 1 : 0);
        }

        private void ParticlesKernelSpawn(CommandBuffer cmd, ParticlesData particlesData, ComputeShader particleCS, SimulationData simData, int bufferID1, int bufferID2)
        {
            if (KWS_TileZoneManager.IsAnyDynamicWavesUseColorMode) cmd.SetComputeTextureParam(particleCS, particlesData._kernelSpawnParticles, KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesColorDataRT, simData.GetColorTarget);
            var target = simData.GetTarget;

            cmd.SetComputeTextureParam(particleCS, particlesData._kernelSpawnParticles, KWS_ShaderConstants.DynamicWaves.KWS_DynamicWaves,                 target);
            cmd.SetComputeTextureParam(particleCS, particlesData._kernelSpawnParticles, KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesAdditionalDataRT, simData.GetAdditionalTarget);
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelSpawnParticles, KwsCounterBuffer, particlesData.CounterBuffer);
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelSpawnParticles, bufferID1,           particlesData.GetCurrentParticlesBuffer);
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelSpawnParticles, bufferID2,           particlesData.GetPreviousParticlesBuffer);
            if (particlesData.TileParticleCountBuffer != null) cmd.SetComputeBufferParam(particleCS, particlesData._kernelSpawnParticles, KwsTileParticleCount, particlesData.TileParticleCountBuffer);


            var dispatchSize = new Vector2(Mathf.CeilToInt(target.rt.width / 8f), Mathf.CeilToInt(target.rt.height / 8f));
            cmd.DispatchCompute(particleCS, particlesData._kernelSpawnParticles, (int)dispatchSize.x, (int)dispatchSize.y, 1);
        }


        private void ParticlesKernelInitGPUDispatch(CommandBuffer cmd, ParticlesData particlesData, ComputeShader particleCS)
        {
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelComputeDispatchArgsForUpdateParticles, KwsCounterBuffer,         particlesData.CounterBuffer);
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelComputeDispatchArgsForUpdateParticles, KwsDispatchIndirectArgs,  particlesData.DispatchIndirectArgs);
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelComputeDispatchArgsForUpdateParticles, KwsParticlesIndirectArgs, particlesData.ParticlesIndirectArgs);
            if (particlesData.TileParticleCountBuffer != null) cmd.SetComputeBufferParam(particleCS, particlesData._kernelComputeDispatchArgsForUpdateParticles, KwsTileParticleCount, particlesData.TileParticleCountBuffer);

            cmd.DispatchCompute(particleCS, particlesData._kernelComputeDispatchArgsForUpdateParticles, 1, 1, 1);
        }


        private void ParticlesResetPinPongBuffers(CommandBuffer cmd, ParticlesData particlesData)
        {
            cmd.SetBufferCounterValue(particlesData.GetPreviousParticlesBuffer, 0);
        }

        private void ParticlesKernelUpdate(CommandBuffer cmd, ParticlesData particlesData, ComputeShader particleCS, SimulationData simData, int bufferReadOnlyID, int bufferID2)
        {
            if (KWS_TileZoneManager.IsAnyDynamicWavesUseColorMode) cmd.SetComputeTextureParam(particleCS, particlesData._kernelSpawnParticles, KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesColorDataRT, simData.GetColorTarget);

            cmd.SetComputeTextureParam(particleCS, particlesData._kernelUpdateParticles, KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesAdditionalDataRT, simData.GetAdditionalTarget);
            cmd.SetComputeTextureParam(particleCS, particlesData._kernelUpdateParticles, KWS_ShaderConstants.DynamicWaves.KWS_DynamicWaves,                 simData.GetTarget);
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelUpdateParticles, KwsCounterBuffer, particlesData.CounterBuffer);
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelUpdateParticles, bufferReadOnlyID,    particlesData.GetCurrentParticlesBuffer);
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelUpdateParticles, bufferID2,           particlesData.GetPreviousParticlesBuffer);
            if (particlesData.TileParticleCountBuffer != null) cmd.SetComputeBufferParam(particleCS, particlesData._kernelUpdateParticles, KwsTileParticleCount, particlesData.TileParticleCountBuffer);

            cmd.DispatchCompute(particleCS, particlesData._kernelUpdateParticles, particlesData.DispatchIndirectArgs, 0);
        }

        private void ParticlesCopyCounters(CommandBuffer cmd, ParticlesData particlesData)
        {
            cmd.CopyCounterValue(particlesData.GetPreviousParticlesBuffer, particlesData.CounterBuffer, 0);
        }


        private void ParticlesKernelComputeIndirectRenderingArgs(CommandBuffer cmd, ParticlesData particlesData, ComputeShader particleCS)
        {
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelComputeDispatchArgsForInstancedRenderingParticles, KwsCounterBuffer,         particlesData.CounterBuffer);
            cmd.SetComputeBufferParam(particleCS, particlesData._kernelComputeDispatchArgsForInstancedRenderingParticles, KwsParticlesIndirectArgs, particlesData.ParticlesIndirectArgs);
            cmd.DispatchCompute(particleCS, particlesData._kernelComputeDispatchArgsForInstancedRenderingParticles, 1, 1, 1);
        }


        private void ExecuteParticles(Camera cam)
        {
            var simZones = KWS_TileZoneManager.DynamicWavesZones;
            foreach (var iZone in simZones)
            {
                var zone = (KWS_DynamicWavesSimulationZone)iZone;

                if (zone.UseFoamParticles && zone.IsZoneVisible)
                {
                    var particlesData = zone.SimulationData.FoamParticlesData;
                    var mat           = particlesData._particlesMaterial;

                    mat.SetBuffer(KwsFoamParticlesBuffer, particlesData.GetCurrentParticlesBuffer);
                    mat.SetFloat(KwsParticlesFoamInterpolationTime, particlesData.ParticlesInterpolationTime);
                    mat.SetFloat(KwsFoamParticlesScale,             zone.FoamParticlesScale);
                    mat.SetFloat(KwsFoamParticlesAlphaMultiplier,   zone.FoamParticlesAlphaMultiplier);
                    mat.SetKeyword("KWS_USE_PHYTOPLANKTON_EMISSION",       zone.UsePhytoplanktonEmission);
                    
                    UpdateSimulationDataShaderParams(mat, zone);

                    var renderParams = particlesData._particlesRenderParams;
                    renderParams.camera      = cam;
                    renderParams.worldBounds = zone.Bounds;
                    
                    Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, particlesData.ParticlesIndirectArgs);
                }

                if (zone.UseSplashParticles && zone.IsZoneVisible)
                {
                    var particlesData = zone.SimulationData.SplashParticlesData;
                    var mat           = particlesData._particlesMaterial;
                    
                    mat.SetTexture(KWS_ShaderConstants.DynamicWaves.KWS_DynamicWavesColorDataRT, zone.SimulationData.GetColorTarget);
                    mat.SetBuffer(KwsSplashParticlesBuffer, particlesData.GetCurrentParticlesBuffer);
                    mat.SetFloat(KwsParticlesSplashInterpolationTime, particlesData.ParticlesInterpolationTime);
                    mat.SetFloat(KwsSplashParticlesScale,             zone.SplashParticlesScale);
                    mat.SetFloat(KwsSplashParticlesAlphaMultiplier,   zone.SplashParticlesAlphaMultiplier);

                    var isVertexShadow  = zone.ReceiveShadowMode is SplashReceiveShadowModeEnum.DirectionalLowQuality or SplashReceiveShadowModeEnum.AllShadowsLowQuality;
                    var isDirShadowOnly = zone.ReceiveShadowMode is SplashReceiveShadowModeEnum.DirectionalLowQuality or SplashReceiveShadowModeEnum.DirectionalHighQuality;
                    var isAllShadows    = zone.ReceiveShadowMode is SplashReceiveShadowModeEnum.AllShadowsLowQuality or SplashReceiveShadowModeEnum.AllShadowsHighQuality;
                    
                    mat.SetKeyword("KWS_USE_PER_VERTEX_SHADOWS",      isVertexShadow);
                    mat.SetKeyword("KWS_USE_DIR_SHADOW",             isDirShadowOnly);
                    mat.SetKeyword("KWS_USE_ALL_SHADOWS",             isAllShadows);
                    mat.SetKeyword("KWS_USE_SPLASH_SHADOW_CAST_FAST", zone.CastShadowMode == SplashCasticShadowModeEnum.LowQuality);
                   
                    UpdateSimulationDataShaderParams(mat, zone);

                    var renderParams = particlesData._particlesRenderParams;
                    renderParams.camera            = cam;
                    renderParams.worldBounds       = zone.Bounds;
                    renderParams.shadowCastingMode = zone.CastShadowMode == SplashCasticShadowModeEnum.Disabled ? ShadowCastingMode.Off : ShadowCastingMode.On;
                   
                    Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, particlesData.ParticlesIndirectArgs);
                }
            }
        }


        private static void UpdateWetDecalPositon(Camera cam, out Vector3 decalSize, out Vector3 decalPos)
        {
            var waterInstance = WaterSystem.Instance;
            var farDistance   = cam.farClipPlane * 0.5f;
            
            decalSize     = waterInstance.WorldSpaceBounds.size;
            decalPos      = cam.transform.position;

            farDistance = Mathf.Min(farDistance, WaterSystem.QualitySettings.MeshDetailingFarDistance);
            decalSize.x = Mathf.Min(decalSize.x, farDistance);
            decalSize.y = Mathf.Min(decalSize.y, farDistance);
            decalSize.z = Mathf.Min(decalSize.z, farDistance);
            
            decalPos.y -= decalSize.y * 0.5f;
            decalPos.y += waterInstance.CurrentMaxHeightOffsetRelativeToWater;
          
        }

        void UpdateWetDecal(Camera cam)
        {
#if KWS_URP || KWS_HDRP
            if (!_wetDecalProjector) return;
            
            UpdateWetDecalPositon(cam, out var decalSize, out var decalPos);
            var rotatedSize = new Vector3(decalSize.x, decalSize.z, decalSize.y);
            _wetDecalProjector.size               = rotatedSize;
            _wetDecalProjector.transform.position = decalPos;
           #endif
        }

        
        private void ExecuteWetMap(WaterPassContext waterContext)
        {
            if (WaterSystem.QualitySettings.UseWetEffect == false)
            {
                if (_wetDecalObject)
                {
                    KW_Extensions.SafeDestroy(_wetDecalObject);
                    _wetDecalObject = null;
                }

                return;
            }
            
            var cam           = waterContext.cam;
          

#if KWS_BUILTIN
            if (!_cubeMesh) _cubeMesh       = MeshUtils.CreateCubeMesh();
            if (!_wetMaterial) _wetMaterial = KWS_CoreUtils.CreateMaterial("Hidden/KriptoFX/KWS/KWS_DynamicWavesWetDecal");

            UpdateWetDecalPositon(cam, out var decalSize, out var decalPos);
            var decalTRS = Matrix4x4.TRS(decalPos, Quaternion.identity, decalSize);

            waterContext.cmd.SetRenderTarget(KWS_CoreUtils.GetMrt(BuiltinRenderTextureType.GBuffer0, BuiltinRenderTextureType.GBuffer1, waterContext.cameraColor), waterContext.cameraColor);
            waterContext.cmd.DrawMesh(_cubeMesh, decalTRS, _wetMaterial, 0, 0);
#endif

#if KWS_URP || KWS_HDRP

            if (!_wetMaterial)
            {
               _wetMaterial = Resources.Load<Material>("PlatformSpecific/WetDecal");
            }
            
            if (!_wetDecalObject)
            {
                _wetDecalObject                    = new GameObject("WetnessDecal");
                _wetDecalObject.hideFlags          = HideFlags.HideAndDontSave;
                _wetDecalObject.transform.SetParent(WaterSystem.UpdateManagerObject.transform);
                _wetDecalObject.transform.rotation = Quaternion.Euler(90, 0, 0);
                _wetDecalProjector                 = _wetDecalObject.AddComponent<DecalProjector>();
                _wetDecalProjector.material        = _wetMaterial;
            }
#endif
        }

     
        private List<KWS_DynamicWavesObject> GetInteractScriptsInArea(Vector3 pos, Vector3 size, Quaternion rotation)
        {
            _interactScriptsInArea.Clear();

            var halfSize   = new Vector2(size.x * 0.5f, size.z * 0.5f);
            var inverseRot = Quaternion.Inverse(rotation);

            foreach (var instance in KWS_TileZoneManager.DynamicWavesObjects)
            {
                var instancePos = instance.transform.position;
                var relative    = instancePos - pos;

                var local   = inverseRot * relative;
                var local2D = new Vector2(local.x, local.z);

                if (Mathf.Abs(local2D.x) <= halfSize.x && Mathf.Abs(local2D.y) <= halfSize.y) _interactScriptsInArea.Add(instance);
            }

            return _interactScriptsInArea;
        }

        internal class ParticlesData
        {
            public readonly int MaxTimeSlices = 4;

            internal int _kernelClearAll;
            internal int _kernelComputeDispatchArgsForInstancedRenderingParticles;
            internal int _kernelComputeDispatchArgsForUpdateParticles;
            internal int _kernelSpawnParticles;
            internal int _kernelUpdateParticles;

            private  ComputeBuffer _ParticlesBuffer1;
            private  ComputeBuffer _ParticlesBuffer2;
            internal ComputeShader _particlesComputeShader;

            internal Material      _particlesMaterial;
            internal RenderParams  _particlesRenderParams;
            internal ComputeBuffer CounterBuffer;
            public   int           CurrentFrame;

            internal ComputeBuffer DispatchIndirectArgs;
            public   int           MaxParticlesBudget;

            internal GraphicsBuffer ParticlesIndirectArgs;
            internal float          ParticlesInterpolationTime;

            internal int ParticlesTimeSlicingFrame;

            internal LocalKeyword  ShaderKeyword;
            internal ComputeBuffer TileParticleCountBuffer;
            public   ComputeBuffer GetCurrentParticlesBuffer  => CurrentFrame % 2 == 0 ? _ParticlesBuffer1 : _ParticlesBuffer2;
            public   ComputeBuffer GetPreviousParticlesBuffer => CurrentFrame % 2 == 0 ? _ParticlesBuffer2 : _ParticlesBuffer1;

            public void InitializeParticlesBuffers<T>(int maxParticles, string particlesShaderName, string shaderKeyword) where T : struct
            {
                if (_ParticlesBuffer1 == null || _ParticlesBuffer1.count != maxParticles)
                {
                    ReleaseParticlesBuffers();

                    MaxParticlesBudget = maxParticles;

                    _ParticlesBuffer1 = KWS_CoreUtils.GetOrUpdateBuffer<T>(ref _ParticlesBuffer1, maxParticles, ComputeBufferType.Append);
                    _ParticlesBuffer1.SetCounterValue(0);

                    _ParticlesBuffer2 = KWS_CoreUtils.GetOrUpdateBuffer<T>(ref _ParticlesBuffer2, maxParticles, ComputeBufferType.Append);
                    _ParticlesBuffer2.SetCounterValue(0);

                    CounterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
                    CounterBuffer.SetCounterValue(0);

                    ParticlesIndirectArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 4, sizeof(uint));
                    var args = new uint[4] { 3, 1, 0, 0 }; //trinagless count, instances count, 0, 0
                    ParticlesIndirectArgs.SetData(args);
                }

                if (!_particlesComputeShader)
                {
                    _particlesComputeShader = KWS_CoreUtils.LoadComputeShader("Common/CommandPass/KWS_DynamicWavesFoamParticlesCompute");

                    _kernelClearAll                                          = _particlesComputeShader.FindKernel("ClearAll");
                    _kernelUpdateParticles                                   = _particlesComputeShader.FindKernel("UpdateParticles");
                    _kernelComputeDispatchArgsForUpdateParticles             = _particlesComputeShader.FindKernel("ComputeDispatchArgsForUpdateParticles");
                    _kernelSpawnParticles                                    = _particlesComputeShader.FindKernel("SpawnParticles");
                    _kernelComputeDispatchArgsForInstancedRenderingParticles = _particlesComputeShader.FindKernel("ComputeDispatchArgsForInstancedRenderingParticles");
                }

                if (DispatchIndirectArgs == null) DispatchIndirectArgs = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);

                if (_particlesMaterial == null) _particlesMaterial = KWS_CoreUtils.CreateMaterial(particlesShaderName);
                _particlesRenderParams = new RenderParams(_particlesMaterial);
                ShaderKeyword          = new LocalKeyword(_particlesComputeShader, shaderKeyword);

                _particlesComputeShader.SetKeyword(ShaderKeyword, true);
            }

            public void ReleaseParticlesBuffers()
            {
                KWS_CoreUtils.ReleaseComputeBuffers(_ParticlesBuffer1, _ParticlesBuffer2, CounterBuffer, DispatchIndirectArgs, TileParticleCountBuffer);
                _ParticlesBuffer1 = _ParticlesBuffer2 = CounterBuffer = DispatchIndirectArgs = TileParticleCountBuffer = null;

                ParticlesIndirectArgs?.Release();
                ParticlesIndirectArgs = null;

                KW_Extensions.SafeDestroy(_particlesComputeShader, _particlesMaterial);
                _particlesMaterial      = null;
                _particlesComputeShader = null;

                ParticlesTimeSlicingFrame  = 0;
                ParticlesInterpolationTime = 0;


                CurrentFrame = 0;

                this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.Release);
            }

            public void SwapParticlesBuffers()
            {
                CurrentFrame++;
            }

            public void IterateSlice()
            {
                ParticlesTimeSlicingFrame++;
                if (ParticlesTimeSlicingFrame >= MaxTimeSlices) ParticlesTimeSlicingFrame = 0;
            }


            public void UpdateInterpolationTime(int framesCount60Fps, int skippedFrames, bool useSlices)
            {
                var timeSlices  = useSlices ? MaxTimeSlices : 1;
                var currentFPS  = 60f                       / Mathf.Max(1, framesCount60Fps);
                var slicedDelta = KW_Extensions.DeltaTime() * (currentFPS / (1 + skippedFrames) / timeSlices);

                ParticlesInterpolationTime += slicedDelta;
            }
        }

        internal class SimulationData
        {
            //public Vector3 ZonePos;
            //public Vector3 ZoneSize;
            //public Vector4 NearFarSizeXZ;
            //public Vector2Int TextureSize;

            public int CurrentFrame;

            public Texture  Depth;
            public Texture  DepthSDF;
            public RTHandle DynamicWaves1;
            public RTHandle DynamicWaves2;
            public RTHandle DynamicWavesAdditionalData1;
            public RTHandle DynamicWavesAdditionalData2;
            public RTHandle DynamicWavesColorData1;
            public RTHandle DynamicWavesColorData2;
            public RTHandle DynamicWavesMask;

            public RTHandle DynamicWavesMaskColor;
            public RTHandle DynamicWavesMaskDepth;
            public RTHandle DynamicWavesNormals;


            internal ParticlesData FoamParticlesData   = new();
            internal ParticlesData SplashParticlesData = new();

            public RTHandle GetTarget               => DynamicWaves2;
            public RTHandle GetAdditionalTarget     => CurrentFrame % 2 == 0 ? DynamicWavesAdditionalData2 : DynamicWavesAdditionalData1;
            public RTHandle GetAdditionalTargetNext => CurrentFrame % 2 == 0 ? DynamicWavesAdditionalData1 : DynamicWavesAdditionalData2;
            public RTHandle GetNormals              => DynamicWavesNormals;

            public RTHandle GetColorTarget
            {
                get
                {
                    if (DynamicWavesColorData1 == null) InitializeSimTexturesColor();
                    return CurrentFrame % 2 == 0 ? DynamicWavesColorData2 : DynamicWavesColorData1;
                }
            }

            public RTHandle GetColorTargetNext => CurrentFrame % 2 == 0 ? DynamicWavesColorData1 : DynamicWavesColorData2;


            public void InitializeSimTextures(int width, int height)
            {
                DynamicWaves1 = KWS_CoreUtils.RTHandles.Alloc(width, height, name: "_dynamicWavesRT1", colorFormat: GraphicsFormat.R16G16B16A16_UNorm);
                DynamicWaves2 = KWS_CoreUtils.RTHandles.Alloc(width, height, name: "_dynamicWavesRT2", colorFormat: GraphicsFormat.R16G16B16A16_UNorm);

                DynamicWavesAdditionalData1 = KWS_CoreUtils.RTHandles.Alloc(width, height, name: "_dynamicWavesAdditionalData1", colorFormat: GraphicsFormat.R8G8B8A8_UNorm);
                DynamicWavesAdditionalData2 = KWS_CoreUtils.RTHandles.Alloc(width, height, name: "_dynamicWavesAdditionalData2", colorFormat: GraphicsFormat.R8G8B8A8_UNorm);

                DynamicWavesNormals = KWS_CoreUtils.RTHandles.Alloc(width, height, name: "_dynamicWavesNormals", colorFormat: GraphicsFormat.R8G8B8A8_SNorm);

                DynamicWavesMask      = KWS_CoreUtils.RTHandles.Alloc(width, height, name: "_dynamicWavesMaskRT",      colorFormat: GraphicsFormat.R8G8B8A8_UNorm);
                DynamicWavesMaskDepth = KWS_CoreUtils.RTHandles.Alloc(width, height, name: "_dynamicWavesMaskDepthRT", depthBufferBits: DepthBits.Depth16);

                "ShorelineWavesPass".WaterLog(DynamicWaves1, DynamicWavesAdditionalData1, DynamicWavesNormals, DynamicWavesMask, DynamicWavesMaskDepth);

                this.WaterLog(DynamicWaves1);
            }

            public void InitializeSimTexturesColor()
            {
                var width  = DynamicWaves1.rt.width;
                var height = DynamicWaves1.rt.height;

                DynamicWavesMaskColor  = KWS_CoreUtils.RTHandles.Alloc(width, height, name: "_dynamicWavesMaskColor",  colorFormat: GraphicsFormat.R8G8B8A8_UNorm);
                DynamicWavesColorData1 = KWS_CoreUtils.RTHandles.Alloc(width, height, name: "_dynamicWavesColorData1", colorFormat: GraphicsFormat.R8G8B8A8_UNorm);
                DynamicWavesColorData2 = KWS_CoreUtils.RTHandles.Alloc(width, height, name: "_dynamicWavesColorData2", colorFormat: GraphicsFormat.R8G8B8A8_UNorm);

                var cmd = new CommandBuffer();
                CoreUtils.SetRenderTarget(cmd, DynamicWavesMaskColor,  ClearFlag.Color, Color.clear);
                CoreUtils.SetRenderTarget(cmd, DynamicWavesColorData1, ClearFlag.Color, Color.clear);
                CoreUtils.SetRenderTarget(cmd, DynamicWavesColorData2, ClearFlag.Color, Color.clear);
                Graphics.ExecuteCommandBuffer(cmd);


                "ShorelineWavesPass".WaterLog(DynamicWavesMaskColor, DynamicWavesColorData1);
            }
            

            public void InitializePrebakedDepth(Texture depth, Texture depthSDF, Vector3 zonePos, Vector3 zoneSize)
            {
                Depth    = depth;
                DepthSDF = depthSDF;
                //ZonePos = zonePos;
                // ZoneSize = zoneSize;

                // NearFarSizeXZ = new Vector4(zonePos.y + zoneSize.y * 0.5f, zoneSize.y, zoneSize.x, zoneSize.z);
                //TextureSize = new Vector2Int(Depth.width, Depth.height);
            }

            public void InitializePrebakedSimData(Texture dynamicWaves)
            {
                var cmd = new CommandBuffer();
                cmd.Blit(dynamicWaves, DynamicWaves1);
                cmd.Blit(dynamicWaves, DynamicWaves2);
                Graphics.ExecuteCommandBuffer(cmd);

                CurrentFrame = 3;
                this.WaterLog("InitializePrebakedSimData");
            }

            public void InitializeParticlesBuffers(bool useFoamParticles, int maxFoamParticles, bool useSplashParticles, int maxSplashParticles)
            {
                if (useFoamParticles)
                {
                    FoamParticlesData.ReleaseParticlesBuffers();
                    FoamParticlesData.InitializeParticlesBuffers<KWS_DynamicWavesHelpers.FoamParticle>(maxFoamParticles, FoamParticlesShaderName, FoamComputeShaderKeyword);
                }

                if (useSplashParticles)
                {
                    SplashParticlesData.ReleaseParticlesBuffers();
                    SplashParticlesData.InitializeParticlesBuffers<KWS_DynamicWavesHelpers.SplashParticle>(maxSplashParticles, SplashParticlesShaderName, SplashComputeShaderKeyword);
                }
            }


            public void InitializeWetDecalData()
            {
            }

            public void SwapSimulationBuffers()
            {
                CurrentFrame++;
            }


            public void Release()
            {
                DynamicWaves1?.Release();
                DynamicWaves2?.Release();
                DynamicWavesAdditionalData1?.Release();
                DynamicWavesAdditionalData2?.Release();
                DynamicWavesNormals?.Release();
                DynamicWavesMask?.Release();
                DynamicWavesMaskDepth?.Release();

                DynamicWavesColorData1?.Release();
                DynamicWavesColorData2?.Release();
                DynamicWavesMaskColor?.Release();

                DynamicWaves1 = DynamicWaves2 = DynamicWavesAdditionalData1 = DynamicWavesAdditionalData2 = DynamicWavesNormals = DynamicWavesMask = DynamicWavesMaskDepth = null;

                DynamicWavesColorData1 = DynamicWavesColorData2 = DynamicWavesMaskColor = null;

                FoamParticlesData.ReleaseParticlesBuffers();
                SplashParticlesData.ReleaseParticlesBuffers();


                CurrentFrame = 0;

                this.WaterLog(string.Empty, KW_Extensions.WaterLogMessageType.ReleaseRT);
            }
        }
    }
}