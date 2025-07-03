using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;


namespace KWS
{
    public class KWS_TileZoneManager
    {
        public interface IWaterZone
        {
            Vector3    Position { get; }
            Vector3    Size     { get; }
            Quaternion Rotation { get; }
            Vector4    RotationMatrix { get; }
            Bounds     Bounds   { get; }

            Bounds OrientedBounds { get; }
            PrecomputedOBBZone PrecomputedObbZone { get; }
            int ID { get; set; }

            bool IsZoneVisible { get; }

            void UpdateVisibility(Camera cam);
        }
        
        internal static List<KWS_DynamicWavesObject> DynamicWavesObjects = new List<KWS_DynamicWavesObject>();

        public static List<IWaterZone> LocalWaterZones = new List<IWaterZone>();
        public static List<IWaterZone> VisibleLocalWaterZones = new List<IWaterZone>();

        public static List<IWaterZone> DynamicWavesZones        = new List<IWaterZone>();
        public static   List<IWaterZone> VisibleDynamicWavesZones = new List<IWaterZone>();

        internal static Action<KWS_DynamicWavesSimulationZone, bool> OnDynamicWavesBakingStateChanged;
        internal static bool                                         IsAnyZoneInBakingMode;
        internal static bool                                         IsAnyDynamicWavesUseColorMode;
        internal static KWS_DynamicWavesSimulationZone               MovableZone;

        internal static float MaxZoneHeight;


        private static ComputeBuffer _tileIndexRangesBuffer_dynamicWaves;
        private static ComputeBuffer _globalTileIndicesBuffer_dynamicWaves;
        private static ComputeBuffer _zoneDataBuffer_dynamicWaves;

        private static ComputeBuffer _tileIndexRangesBuffer_LocalZone;
        private static ComputeBuffer _globalTileIndicesBuffer_LocalZone;
        private static ComputeBuffer _zoneDataBuffer_LocalZone;

        private static List<int> _globalTileIndices = new List<int>();
        private static List<Vector2Int> _tileIndexRanges = new List<Vector2Int>();
        private static List<ZoneData> _zoneData_dynamicWaves = new List<ZoneData>();
        private static List<LocalWaterZoneData> _zoneData_LocalZone = new List<LocalWaterZoneData>();

        static         WaterZoneDistanceComparer _comparerByDistance         = new WaterZoneDistanceComparer();
        private static WaterZoneHeightComparer   _comparerByHeight = new WaterZoneHeightComparer();

        internal static int MaxVisibleZones = 8;
        internal const int GlobalMaxGridTiles = 20;
        internal const float GlobalGridSizeInMeters = 20f;

        const                   int    MaxTileCount                   = GlobalMaxGridTiles * GlobalMaxGridTiles;
        static                  int[,] _tileZoneIndices               = new int[MaxTileCount, MaxVisibleZones];
        static                  int[]  _tileZoneCounts                = new int[MaxTileCount];
        private static readonly int    KwsTileIndexRanges             = Shader.PropertyToID("KWS_TileIndexRanges");
        private static readonly int    KwsGlobalTileIndices           = Shader.PropertyToID("KWS_GlobalTileIndices");
        private static readonly int    KwsZoneData                    = Shader.PropertyToID("KWS_ZoneData");
        private static readonly int    WorldMin                       = Shader.PropertyToID("_WorldMin");
        private static readonly int    WorldSize                      = Shader.PropertyToID("_WorldSize");
        private static readonly int    GridSize                       = Shader.PropertyToID("_GridSize");
        private static readonly int    KwsWaterDynamicWavesZonesCount = Shader.PropertyToID("KWS_WaterDynamicWavesZonesCount");
        private static readonly int    KwsTileIndexRangesLocalZone    = Shader.PropertyToID("KWS_TileIndexRanges_LocalZone");
        private static readonly int    KwsGlobalTileIndicesLocalZone  = Shader.PropertyToID("KWS_GlobalTileIndices_LocalZone");
        private static readonly int    KwsZoneDataLocalZone           = Shader.PropertyToID("KWS_ZoneData_LocalZone");
        private static readonly int    WorldMinLocalZone              = Shader.PropertyToID("_WorldMin_LocalZone");
        private static readonly int    WorldSizeLocalZone             = Shader.PropertyToID("_WorldSize_LocalZone");
        private static readonly int    GridSizeLocalZone              = Shader.PropertyToID("_GridSize_LocalZone");
        private static readonly int    KwsWaterLocalZonesCount        = Shader.PropertyToID("KWS_WaterLocalZonesCount");


        private int _lastVisibleDynamicWavesZonesHashCode;
        private int _lastVisibleLocalWavesZonesHashCode;

        public struct ZoneData
        {
            public Vector3 center;
            public Vector3 halfSize;
            public Vector4 rotMatrix; // (c0.x, c0.y, c1.x, c1.y)

            public int     ID;
            public Vector2 uv;
            public float   flowSpeedMultiplier;
        }

        public struct LocalWaterZoneData
        {
            public Vector3 center;
            public Vector3 halfSize;
            public Vector4 rotMatrix; // (c0.x, c0.y, c1.x, c1.y)

            public int ID;
            public Vector2 uv;

            public float OverrideColorSettings;
            public float Transparent;
            public Color WaterColor;
            public Color TurbidityColor;
            public float UseSphereBlending;

            public float OverrideWindSettings;
            public float WindStrengthMultiplier;
            public float WindEdgeBlending;
            
            public float OverrideHeight;
            public float HeightEdgeBlending;
            public float ClipWaterBelowZone;
        }

        public void OnEnable()
        {
            SetDefaultBuffers();
        }

        public void OnDisable()
        {
            ReleaseBuffers();
        }

        public void ExecutePerFrame(HashSet<Camera> cameras)
        {
            var cam = KWS_CoreUtils.GetFixedUpdateCamera(cameras);
            if (cam == null) return;

            MovableZone = null;
            VisibleDynamicWavesZones.Clear();
            if (DynamicWavesZones.Count > 0) UpdateDynamicWavesZonesVisibility(cam);

            VisibleLocalWaterZones.Clear();
            if (LocalWaterZones.Count > 0) UpdateLocalWaterZonesVisibility(cam);
        }

        void SetDefaultBuffers()
        {
            KWS_CoreUtils.SetFallbackBuffer<KWS_CoreUtils.Uint2>(ref _tileIndexRangesBuffer_dynamicWaves, KwsTileIndexRanges);
            KWS_CoreUtils.SetFallbackBuffer<KWS_CoreUtils.Uint2>(ref _tileIndexRangesBuffer_LocalZone,    KwsTileIndexRangesLocalZone);
            
            KWS_CoreUtils.SetFallbackBuffer<int>(ref _globalTileIndicesBuffer_dynamicWaves, KwsGlobalTileIndices);
            KWS_CoreUtils.SetFallbackBuffer<int>(ref _globalTileIndicesBuffer_LocalZone, KwsGlobalTileIndicesLocalZone);
            
            KWS_CoreUtils.SetFallbackBuffer<ZoneData>(ref _zoneDataBuffer_dynamicWaves, KwsZoneData);
            KWS_CoreUtils.SetFallbackBuffer<LocalWaterZoneData>(ref _zoneDataBuffer_LocalZone, KwsZoneDataLocalZone);
        }

        void UpdateDynamicWavesZonesVisibility(Camera cam)
        {
            foreach (var zone in DynamicWavesZones)
            {
                zone.UpdateVisibility(cam);
                if (!zone.IsZoneVisible) continue;

                if (((KWS_DynamicWavesSimulationZone)zone).ZoneType == KWS_DynamicWavesSimulationZone.SimulationZoneTypeMode.MovableZone) MovableZone = (KWS_DynamicWavesSimulationZone)zone;
                else VisibleDynamicWavesZones.Add(zone);
            }

            IsAnyDynamicWavesUseColorMode = DynamicWavesObjects.Any(s => s.UseSourceColor && s.isActiveAndEnabled);
            if (VisibleDynamicWavesZones.Count == 0) return;

           
            SortVisibleZonesByDistance(cam.transform.position, VisibleDynamicWavesZones, MaxVisibleZones);
            UpdateDynamicWavesZoneData();

            var visibleZonesHashCode = GetVisibleZonesHash(VisibleDynamicWavesZones);
            if (_lastVisibleDynamicWavesZonesHashCode != visibleZonesHashCode)
            {

                UpdateWaterClusters(VisibleDynamicWavesZones, ref _tileIndexRangesBuffer_dynamicWaves, ref _globalTileIndicesBuffer_dynamicWaves, out var worldBounds, out var gridSizeX, out var gridSizeZ);
               
                _lastVisibleDynamicWavesZonesHashCode = visibleZonesHashCode;


                Shader.SetGlobalBuffer(KwsTileIndexRanges, _tileIndexRangesBuffer_dynamicWaves);
                Shader.SetGlobalBuffer(KwsGlobalTileIndices, _globalTileIndicesBuffer_dynamicWaves);
                Shader.SetGlobalBuffer(KwsZoneData, _zoneDataBuffer_dynamicWaves);

                Shader.SetGlobalVector(WorldMin, worldBounds.min);
                Shader.SetGlobalVector(WorldSize, worldBounds.size);
                Shader.SetGlobalVector(GridSize, new Vector2(gridSizeX, gridSizeZ));
                Shader.SetGlobalInteger(KwsWaterDynamicWavesZonesCount, VisibleLocalWaterZones.Count);

            }

        }
         
        void UpdateLocalWaterZonesVisibility(Camera cam)
        {
            foreach (var zone in LocalWaterZones)
            {
                zone.UpdateVisibility(cam);
                if (!zone.IsZoneVisible) continue;

                VisibleLocalWaterZones.Add(zone);
            }
            if (VisibleLocalWaterZones.Count == 0) return;

            SortVisibleZonesByDistance(cam.transform.position, VisibleLocalWaterZones, MaxVisibleZones);
            SortVisibleZonesByHeight(VisibleLocalWaterZones);
            UpdateLocalWaterZoneData();

            var visibleZonesHashCode = GetVisibleZonesHash(VisibleLocalWaterZones);
            if (_lastVisibleLocalWavesZonesHashCode != visibleZonesHashCode)
            {
                UpdateWaterClusters(VisibleLocalWaterZones, ref _tileIndexRangesBuffer_LocalZone, ref _globalTileIndicesBuffer_LocalZone, out var worldBounds, out var gridSizeX, out var gridSizeZ);
                _lastVisibleLocalWavesZonesHashCode = visibleZonesHashCode;

                Shader.SetGlobalBuffer(KwsTileIndexRangesLocalZone,   _tileIndexRangesBuffer_LocalZone);
                Shader.SetGlobalBuffer(KwsGlobalTileIndicesLocalZone, _globalTileIndicesBuffer_LocalZone);
                Shader.SetGlobalBuffer(KwsZoneDataLocalZone,          _zoneDataBuffer_LocalZone);

                Shader.SetGlobalVector(WorldMinLocalZone, worldBounds.min);
                Shader.SetGlobalVector(WorldSizeLocalZone, worldBounds.size);
                Shader.SetGlobalVector(GridSizeLocalZone, new Vector2(gridSizeX, gridSizeZ));

                Shader.SetGlobalInteger(KwsWaterLocalZonesCount, VisibleLocalWaterZones.Count);
            }


        }

        internal static void UpdateDynamicWavesZoneData()
        {
            _zoneData_dynamicWaves.Clear();
            for (int i = 0; i < VisibleDynamicWavesZones.Count; i++)
            {
                var zone = VisibleDynamicWavesZones[i];
                if (zone == null) continue;

                var cacheData    = zone.PrecomputedObbZone;
                var unpackedZone = (KWS_DynamicWavesSimulationZone)zone;

                _zoneData_dynamicWaves.Add(new ZoneData
                {
                    center    = zone.Position,
                    halfSize  = zone.Size * 0.5f,
                    rotMatrix = cacheData.RotMatrix,
                    ID        = zone.ID,
                    flowSpeedMultiplier = unpackedZone.FlowSpeedMultiplier,
                });
            }

            _zoneDataBuffer_dynamicWaves = KWS_CoreUtils.GetOrUpdateBuffer<ZoneData>(ref _zoneDataBuffer_dynamicWaves, _zoneData_dynamicWaves.Count);
            _zoneDataBuffer_dynamicWaves.SetData(_zoneData_dynamicWaves);
        }

        internal static void UpdateLocalWaterZoneData()
        {
            _zoneData_LocalZone.Clear();
            for (int i = 0; i < VisibleLocalWaterZones.Count; i++)
            {
                var zone = VisibleLocalWaterZones[i];
                if (zone == null) continue;

                var unpackedZone = (KWS_LocalWaterZone)zone;

                var cacheData    = zone.PrecomputedObbZone;
              
                _zoneData_LocalZone.Add(new LocalWaterZoneData()
                {
                    center    = zone.Position,
                    halfSize  = zone.Size * 0.5f,
                    rotMatrix = cacheData.RotMatrix,
                    ID        = zone.ID,

                    OverrideColorSettings = unpackedZone.OverrideColorSettings ? 1 : 0,
                    Transparent           = unpackedZone.Transparent,
                    WaterColor            = unpackedZone.WaterColor,
                    TurbidityColor        = unpackedZone.TurbidityColor,
                    UseSphereBlending     = unpackedZone.UseSphericalBlending ? 1 : 0,

                    OverrideWindSettings   = unpackedZone.OverrideWindSettings ? 1 : 0,
                    WindStrengthMultiplier = unpackedZone.WindStrengthMultiplier,
                    WindEdgeBlending       = unpackedZone.WindEdgeBlending,
                    
                    OverrideHeight     = unpackedZone.OverrideHeight ? 1 : 0,
                    HeightEdgeBlending = unpackedZone.HeightEdgeBlending,
                    ClipWaterBelowZone = unpackedZone.ClipWaterBelowZone ? 1 : 0,
                });
            }
      
            _zoneDataBuffer_LocalZone = KWS_CoreUtils.GetOrUpdateBuffer<LocalWaterZoneData>(ref _zoneDataBuffer_LocalZone, _zoneData_LocalZone.Count);
            _zoneDataBuffer_LocalZone.SetData(_zoneData_LocalZone);
        }


        internal static void UpdateWaterClusters(List<IWaterZone> zones, ref ComputeBuffer tileIndexRangesBuffer, ref ComputeBuffer globalTileIndicesBuffer, out Bounds worldBounds, out int gridSizeX, out int gridSizeZ)
        {
            //System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
           // stopwatch.Start();

            worldBounds = zones[0].OrientedBounds;
            for (int i = 1; i < zones.Count; i++)
            {
                if (zones[i] == null) continue;
                worldBounds.Encapsulate(zones[i].OrientedBounds);
            }

            float worldMinX = worldBounds.min.x;
            float worldMinZ = worldBounds.min.z;

            gridSizeX = Mathf.Clamp(Mathf.CeilToInt(worldBounds.size.x / GlobalGridSizeInMeters), 1, GlobalMaxGridTiles);
            gridSizeZ = Mathf.Clamp(Mathf.CeilToInt(worldBounds.size.z / GlobalGridSizeInMeters), 1, GlobalMaxGridTiles);

            float cellSizeX = worldBounds.size.x / gridSizeX;
            float cellSizeZ = worldBounds.size.z / gridSizeZ;

          
            _globalTileIndices.Clear();
            _tileIndexRanges.Clear();

            Array.Clear(_tileZoneCounts, 0, _tileZoneCounts.Length);
            Vector2 tileHalfSize = new Vector2(cellSizeX, cellSizeZ) * 0.5f;

        
            for (int z = 0; z < gridSizeZ; z++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    var tileIndex = z * gridSizeX + x;
                    var tileCenter = new Vector2(worldMinX + (x + 0.5f) * cellSizeX, worldMinZ + (z + 0.5f) * cellSizeZ);


                    for (var zoneIdx = 0; zoneIdx < zones.Count; zoneIdx++)
                    {
                        var zone = zones[zoneIdx];

                        if (FastOBBIntersects(tileCenter, tileHalfSize, zone.PrecomputedObbZone))
                        {
                            var currentCount = _tileZoneCounts[tileIndex];
                            if (currentCount < MaxVisibleZones)
                            {
                                _tileZoneIndices[tileIndex, currentCount] = zone.ID;
                                _tileZoneCounts[tileIndex]++;
                            }
                        }
                    }
                }
            }

            for (int z = 0; z < gridSizeZ; z++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    var tileIndex = z * gridSizeX + x;
                    var offset = _globalTileIndices.Count;
                    var count = _tileZoneCounts[tileIndex];

                    if (count > 0)
                    {
                        for (int i = 0; i < count; i++)
                            _globalTileIndices.Add(_tileZoneIndices[tileIndex, i]);

                        _tileIndexRanges.Add(new Vector2Int(offset, count));
                    }
                    else
                    {
                        _globalTileIndices.Add(-1);
                        _tileIndexRanges.Add(new Vector2Int(offset, 1));
                    }
                }
            }

            if (_tileIndexRanges.Count == 0 || _globalTileIndices.Count == 0) return;

            tileIndexRangesBuffer = KWS_CoreUtils.GetOrUpdateBuffer<KWS_CoreUtils.Uint2>(ref tileIndexRangesBuffer, _tileIndexRanges.Count);
            globalTileIndicesBuffer = KWS_CoreUtils.GetOrUpdateBuffer<int>(ref globalTileIndicesBuffer, _globalTileIndices.Count);
          

            tileIndexRangesBuffer.SetData(_tileIndexRanges);
            globalTileIndicesBuffer.SetData(_globalTileIndices);
          
           // stopwatch.Stop();
          //  Debug.Log($"Elapsed time (Fast OBB): {stopwatch.Elapsed.TotalMilliseconds} ms");
        }



        internal static void SortVisibleZonesByDistance(Vector3 cameraPosition, List<IWaterZone> zones, int maxVisibleZones)
        {
            _comparerByDistance.CameraPosition = cameraPosition;
            zones.Sort(_comparerByDistance);

            if (zones.Count > maxVisibleZones)
                zones.RemoveRange(maxVisibleZones, zones.Count - maxVisibleZones);

            for (int i = 0; i < zones.Count; i++)
            {
                zones[i].ID = i;
            }

            MaxZoneHeight = -1000000;
            foreach (var zone in zones)
            {
                MaxZoneHeight = Mathf.Max(MaxZoneHeight, zone.Position.y + zone.Size.y * 0.5f);
            }
        }

        internal static void SortVisibleZonesByHeight(List<IWaterZone> zones)
        {
            zones.Sort(_comparerByHeight);
            
            for (int i = 0; i < zones.Count; i++)
            {
                zones[i].ID = i;
            }
        }

        internal static int GetVisibleZonesHash(List<IWaterZone> zones)
        {
            unchecked
            {
                int hash = 17;
                foreach (var zone in zones)
                {
                    hash = hash * 31 + zone.GetHashCode();
                    hash = hash * 31 + zone.OrientedBounds.GetHashCode();
                }
                return hash;
            }
        }


        public struct PrecomputedOBBZone
        {
            public Vector2 Center;
            public Vector2[] Axis; // [0] = right, [1] = forward
            public Vector3 HalfSize;
            public float[] Extents;
            public Vector4 RotMatrix;
        }

        public static bool FastOBBIntersects(Vector2 tileCenter, Vector2 tileHalfSize, PrecomputedOBBZone obbBCache)
        {
            Vector2 d = tileCenter - obbBCache.Center;

            for (int i = 0; i < 2; i++)
            {
                float proj = Mathf.Abs(Vector2.Dot(d, obbBCache.Axis[i])) - (
                    Vector2.Dot(obbBCache.Axis[i], obbBCache.Axis[i]) * obbBCache.HalfSize[i] + GlobalGridSizeInMeters * 0.75f +
                    Mathf.Abs(Vector2.Dot(tileHalfSize, obbBCache.Axis[i]))
                );
                if (proj > 0) return false;
            }

            Vector2 bX = new Vector2(1, 0);
            Vector2 bY = new Vector2(0, 1);

            float projX = Mathf.Abs(Vector2.Dot(d, bX)) - (tileHalfSize.x + obbBCache.Extents[0]);
            if (projX > 0) return false;

            float projY = Mathf.Abs(Vector2.Dot(d, bY)) - (tileHalfSize.y + obbBCache.Extents[1]);
            if (projY > 0) return false;

            return true;
        }




        public static void ReleaseBuffers()
        {
            _tileIndexRangesBuffer_dynamicWaves?.Release();
            _tileIndexRangesBuffer_dynamicWaves = null;

            _globalTileIndicesBuffer_dynamicWaves?.Release();
            _globalTileIndicesBuffer_dynamicWaves = null;

            _zoneDataBuffer_dynamicWaves?.Release();
            _zoneDataBuffer_dynamicWaves = null;


            _tileIndexRangesBuffer_LocalZone?.Release();
            _tileIndexRangesBuffer_LocalZone = null;

            _globalTileIndicesBuffer_LocalZone?.Release();
            _globalTileIndicesBuffer_LocalZone = null;

            _zoneDataBuffer_LocalZone?.Release();
            _zoneDataBuffer_LocalZone = null;
        }
    }


    internal class WaterZoneDistanceComparer : IComparer<KWS_TileZoneManager.IWaterZone>
    {
        public Vector3 CameraPosition;

        public int Compare(KWS_TileZoneManager.IWaterZone a, KWS_TileZoneManager.IWaterZone b)
        {
            float distA = Vector3.SqrMagnitude(a.Bounds.ClosestPoint(CameraPosition) - CameraPosition);
            float distB = Vector3.SqrMagnitude(b.Bounds.ClosestPoint(CameraPosition) - CameraPosition);
            return distA.CompareTo(distB);
        }
    }

    internal class WaterZoneHeightComparer : IComparer<KWS_TileZoneManager.IWaterZone>
    {
        public int Compare(KWS_TileZoneManager.IWaterZone a, KWS_TileZoneManager.IWaterZone b)
        {
            return a.Position.y.CompareTo(b.Position.y);
        }
    }
}
