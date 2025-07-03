using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

using System.IO;
using UnityEngine.Rendering;

//todo add KWS_STANDARD/KWS_HDRP/KWS_URP

namespace KWS
{
    [ExecuteAlways]
    [Serializable]
    [AddComponentMenu("")]
    public partial class WaterSystem : MonoBehaviour
    {
        public static WaterSystem Instance { get; private set; }
        public static WaterQualityLevelSettings QualitySettings => KWS_WaterSettingsRuntimeLoader._settings;

        public static Action<WaterSystem.WaterSettingsCategory> OnAnyWaterSettingsChanged;


        //Color settings
        public float Transparent = 10;
        public Color DyeColor = new Color(1, 1, 1, 1);
        public Color TurbidityColor = new Color(7 / 255.0f, 65 / 255.0f, 80 / 255.0f);

        //Waves settings
        public float WindSpeed = 5.0f;
        public float WindRotation = 0;
        public float WindTurbulence = 0.25f;
        public WaterQualityLevelSettings.FftWavesQualityEnum FftWavesQuality = WaterQualityLevelSettings.FftWavesQualityEnum.Ultra;
        public int FftWavesCascades = 4;
        public float WavesAreaScale = 1;
        public float WavesTimeScale = 1;
        public WindZone WindZone;
        public float WindZoneSpeedMultiplier = 1;
        public float WindZoneTurbulenceMultiplier = 1;

        //Refraction

        public float RefractionAproximatedDepth   = 5f;
        public float RefractionSimpleStrength     = 0.25f;
        public float RefractionDispersionStrength = 0.35f;

        //Foam
        public float OceanFoamStrength                 = 0.2f;
        public float OceanFoamDisappearSpeedMultiplier = 0.5f;
        public float OceanFoamTextureSize              = 30;

        //Wet
        public float WetStrength = 1.0f;

        //Caustic
        public float CausticDepth    = 5;
        public float CausticStrength = 2.5f;
        
        [SerializeField] internal bool ShowColorSettings = true;
        [SerializeField] internal bool ShowWavesSettings = true;
        [SerializeField] internal bool ShowRefractionSettings       = false;
        [SerializeField] internal bool ShowFoamSettings             = false;
        [SerializeField] internal bool ShowWetSettings              = false;
        [SerializeField] internal bool ShowCausticEffectSettings    = false;


        #region public API methods


        /// <summary>
        /// You can manually control the water rendering.
        /// For example, you can disable rendering if you go into a cave, etc
        /// </summary>
        public bool WaterRenderingActive = true;

        public static bool IsCameraPartialUnderwater { get;     private set; }
        public static   bool   IsCameraFullUnderwater    { get; private set; }
        internal static bool   IsCameraRequireWaterDrops { get; private set; }
        internal        Bounds WorldSpaceBounds;


        ///// <summary>
        ///// Check if the current world space position is under water. For example, you can detect if your character enters the water to like triggering a swimming state.
        ///// </summary>
        ///// <param name="worldPos"></param>
        ///// <returns></returns>
        public static bool IsPositionUnderWater(Vector3 worldPos)
        {
            _worldPointRequest.SetNewPosition(worldPos);
            WaterSystem.TryGetWaterSurfaceData(_worldPointRequest);

            if (_worldPointRequest.IsDataReady)
            {
                return worldPos.y < _worldPointRequest.Result.Position.y;
            }
            return false;
        }


        /// <summary>
        /// Retrieves water position, normal, and velocity at a given world position.
        /// Works only with global wind.
        /// </summary>
        /// <param name="surfaceRequest">
        /// A reference to a <see cref="WaterSurfaceRequest"/> instance, which must be created beforehand.
        /// This request contains an array of data necessary to handle updates from different scripts and rendering queue 
        /// (such as Update, FixedUpdate, OnGUI, etc.).
        /// 
        /// Example usage:
        /// <code>
        /// WaterSurfaceRequest request = new WaterSurfaceRequest();
        /// request.SetNewPositions(positionsArray);
        /// var result = request.Result[index];
        /// </code>
        /// </param>
        /// <returns>Returns <c>true</c> if data retrieval was successful, otherwise <c>false</c>.</returns>
        public static void TryGetWaterSurfaceData(IWaterSurfaceRequest surfaceRequest)
        {
            BuoyancyPass.TryGetWaterSurfaceData(surfaceRequest);
        }

       
        internal static float FindWaterLevelAtLocation(Vector3 worldPosition) //todo 
        {
            if (Instance.WorldSpaceBounds.ContainsXZ(worldPosition)) return Instance.WaterPivotWorldPosition.y;
            return worldPosition.y;
        }

        /// <summary>
        /// Activate this option if you want to manually synchronize the time for all clients over the network
        /// </summary>
        public static bool UseNetworkTime;
        public static float NetworkTime;
        public static bool UseNetworkBuoyancy;


        #endregion

        #region editor variables


        internal bool DebugQuadtree = false;
        internal bool DebugAABB = false;
        internal bool DebugBuoyancy = false;
        internal bool DebugDynamicWaves = false;
        internal bool DebugUpdateManager = false;


        #endregion

        #region internal variables


        //internal Vector3 WaterRelativeWorldPosition
        //{
        //    get
        //    {
        //        var pos = KWS_UpdateManager.CurrentRenderedCameraTransform.position;
        //        pos.y = WaterPivotWorldPosition.y;
        //        return pos;
        //    }
        //}

        internal Vector3 WaterPivotWorldPosition => WaterRootTransform.position;

        internal float WaterBoundsSurfaceHeight;

        private Transform _waterTransform;
        internal Transform WaterRootTransform
        {
            get
            {
                if (_waterTransform == null) _waterTransform = transform;
                return _waterTransform;
            }
        }

        internal bool IsWaterVisible { get; private set; }

        internal float CurrentMaxWaveHeight;
        internal float CurrentMaxHeightOffsetRelativeToWater;
        internal float SkyLodRelativeToWind => Mathf.Lerp(0.25f, KWS_Settings.Reflection.MaxSkyLodAtFarDistance, Mathf.Pow(Mathf.Clamp01(WaterSystem.Instance.WindSpeed / 15f), 0.35f));

        internal static float GlobalTimeScale = 1;

        #endregion

        #region private variables


#if KWS_DEBUG
        public static Vector4 Test4 = Vector4.zero;
        public static float VRScale = 1;
        public static Texture2D TestTexture;
#endif

        internal static GameObject UpdateManagerObject;
        internal static KWS_UpdateManager UpdateManagerInstance;

        internal bool IsWaterInitialized { get; private set; }
        private bool _isWaterPlatformSpecificResourcesInitialized;

        #endregion

        #region properties

        internal MeshQuadTree _meshQuadTree = new MeshQuadTree();

        #endregion

        private GameObject _infiniteOceanShadowCullingAnchor;
        private Transform _infiniteOceanShadowCullingAnchorTransform;


        private void Awake()
        {

        }

        private void OnEnable()
        {
            var allInstances = FindObjectsOfType<WaterSystem>(true);

            foreach (var instance in allInstances)
            {
                if (instance != this && instance.isActiveAndEnabled)
                {
                    Debug.LogError("Multiple active WaterSystems detected. Disabling extra instance.");
                    instance.gameObject.SetActive(false);
                }
            }
            Instance = this;

            WaterSharedResources.UpdateReflectionProbeCache();

            var updateManager = GetComponentInChildren<KWS_UpdateManager>(true);
            if (updateManager != null)
            {
                KW_Extensions.SafeDestroy(updateManager.gameObject);
            }

            UpdateManagerObject = KW_Extensions.CreateHiddenGameObject("KWS_UpdateManager");
            UpdateManagerInstance = UpdateManagerObject.AddComponent<KWS_UpdateManager>();
            UpdateManagerObject.transform.parent = transform;
            
            OnAnyWaterSettingsChanged += OnAnyWaterSettingsChangedEvent;
            UpdateWaterInstance(WaterSettingsCategory.All);
        }

        void OnDestroy()
        {
            if (IsWaterInitialized) OnDisable();
        }

        void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            if (Instance == null)
            {
                KW_Extensions.SafeDestroy(UpdateManagerObject);
                UpdateManagerObject = null;
                UnloadResources();
            }
            OnAnyWaterSettingsChanged -= OnAnyWaterSettingsChangedEvent;

            Release();
        }


        internal void OnPreWaterRender(Camera cam)
        {
            Profiler.BeginSample("Water.Rendering");
            RenderWater(cam);
            Profiler.EndSample();
        }



        internal void OnPreCameraRender(Camera cam)
        {
          
#if KWS_DEBUG
            if (DebugQuadtree && cam.cameraType != CameraType.Game) return;
#endif

            CurrentMaxWaveHeight = EvaluateMaxAmplitute();
            WorldSpaceBounds = CalculateCurrentWorldSpaceBounds();


            if (!KWS_UpdateManager.FrustumCaches.TryGetValue(cam, out var frustumCache)) return;
            IsWaterVisible = KW_Extensions.IsBoxVisibleAccurate(ref frustumCache.FrustumPlanes, ref frustumCache.FrustumCorners, WorldSpaceBounds.min, WorldSpaceBounds.max);

            if (_underwaterStateCameras.ContainsKey(cam))
            {
                IsCameraPartialUnderwater = _underwaterStateCameras[cam].isCameraPartialUnderwater;
                IsCameraFullUnderwater = _underwaterStateCameras[cam].isCameraFullUnderwater;
                IsCameraRequireWaterDrops = _underwaterStateCameras[cam].IsCameraRequireWaterDrops;
            }

#if KWS_DEBUG
            Shader.SetGlobalVector("Test4", Test4);
            if (KWS_CoreUtils.SinglePassStereoEnabled) UnityEngine.XR.XRSettings.eyeTextureResolutionScale = VRScale;
#endif

            SetGlobalCameraShaderParams(cam);
            SetGlobalOceanWaterShaderParams();
            SetGlobalPlatformSpecificShaderParams(cam);

            SetQualitySettingsGlobalKeywords(cam);
            SetQualitySettingsShaderParams();
            SetSettingsConstantShaderParams();
        }


        void Release()
        {
            IsWaterInitialized = false;

            _meshQuadTree.Release();

            if (_infiniteOceanShadowCullingAnchor != null) KW_Extensions.SafeDestroy(_infiniteOceanShadowCullingAnchor.GetComponent<MeshFilter>().sharedMesh,
                                                                                     _infiniteOceanShadowCullingAnchor.GetComponent<MeshRenderer>().sharedMaterial);

            KW_Extensions.SafeDestroy(_infiniteOceanShadowCullingAnchor);

            _isFluidsSimBakedMode = false;

            _isWaterPlatformSpecificResourcesInitialized = false;
            IsWaterVisible = false;
            IsCameraPartialUnderwater = false;
            IsCameraFullUnderwater = false;

            // CameraDatas.Clear();
#if KWS_DEBUG
            DebugHelpers.Release();
#endif

            _underwaterStateCameras.Clear();
        }
    }
}