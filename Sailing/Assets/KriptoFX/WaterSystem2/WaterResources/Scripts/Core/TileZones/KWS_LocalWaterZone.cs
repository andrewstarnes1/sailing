using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KWS
{
    [ExecuteInEditMode]
    public class KWS_LocalWaterZone : MonoBehaviour, KWS_TileZoneManager.IWaterZone
    {
        public bool OverrideColorSettings = true;
        
        public float Transparent = 4;
        public Color WaterColor       = Color.white;
        public Color TurbidityColor   = new Color(159 / 255.0f, 59 / 255.0f, 0 / 255.0f);
        public bool UseSphericalBlending = false;
        
        public bool OverrideWindSettings = false;
        public float WindStrengthMultiplier = 0.05f;
        public float WindEdgeBlending = 0.75f;


        public bool  OverrideHeight      = false;
        public float HeightEdgeBlending  = 1.0f;
        public bool  ClipWaterBelowZone = false;
       

        [SerializeField] internal bool ShowColorSettings = true;
        [SerializeField] internal bool ShowWindSettings  = true;
        [SerializeField] internal bool ShowHeightSettings  = true;

        public Vector3    Position       => CachedAreaPos;
        public Vector3    Size           => CachedAreaSize;
        public Quaternion Rotation       => CachedRotation; 
        public Vector4    RotationMatrix => CachedRotationMatrix;
        public Bounds     Bounds         => CachedBounds;
        public bool       IsZoneVisible  => _IsZoneVisible;

        int KWS_TileZoneManager.IWaterZone.                                           ID                 { get; set; }
        Bounds KWS_TileZoneManager.IWaterZone.                                        OrientedBounds     => CachedOrientedBounds;
        KWS_TileZoneManager.PrecomputedOBBZone KWS_TileZoneManager.IWaterZone.PrecomputedObbZone => _precomputedObbZone;

        // bool 

        private Vector3    CachedAreaPos;
        private Vector3    CachedAreaSize;
        private Quaternion CachedRotation;
        private Bounds     CachedBounds;
        private Bounds     CachedOrientedBounds;
        private Vector4    CachedRotationMatrix;

        KWS_TileZoneManager.PrecomputedOBBZone _precomputedObbZone;

        private bool _IsZoneVisible;

        void KWS_TileZoneManager.IWaterZone.UpdateVisibility(Camera cam)
        {
            _IsZoneVisible = false;

            if (!KWS_UpdateManager.FrustumCaches.TryGetValue(cam, out var cache))
            {
                return;
            }

            var planes = cache.FrustumPlanes;
            var min    = Bounds.min;
            var max    = Bounds.max;
            
            if (OverrideHeight)
            {
                float waterLevel = WaterSystem.Instance.WaterPivotWorldPosition.y;
                min.y = Mathf.Min(min.y, waterLevel);
                max.y = Mathf.Max(max.y, waterLevel + WaterSystem.Instance.CurrentMaxHeightOffsetRelativeToWater);
            }
            
            _IsZoneVisible = KW_Extensions.IsBoxVisibleApproximated(ref planes, min, max);
        }

        void UpdateTransform()
        {
            var t = transform;
            CachedAreaPos        = t.position;
            CachedAreaSize       = t.localScale;
            CachedRotation       = t.rotation;
            CachedBounds         = new Bounds(CachedAreaPos, CachedAreaSize);
            CachedOrientedBounds = KW_Extensions.GetOrientedBounds(CachedAreaPos, CachedAreaSize, CachedRotation);

            var angleRad = CachedRotation.eulerAngles.y * Mathf.Deg2Rad;
            var cos      = Mathf.Cos(angleRad);
            var sin      = Mathf.Sin(angleRad);
            CachedRotationMatrix = new Vector4(cos, sin, -sin, cos);


            CachePrecomputedOBBZone();
        }

        internal void CachePrecomputedOBBZone()
        {
            var     bounds   = Bounds;
            Vector2 center   = new Vector2(bounds.center.x, bounds.center.z);
            Vector2 halfSize = new Vector2(bounds.size.x, bounds.size.z) * 0.5f;
        

            float angleRad = transform.rotation.eulerAngles.y * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);
            Vector4 rotMatrix = new Vector4(cos, -sin, sin, cos);

            _precomputedObbZone = new KWS_TileZoneManager.PrecomputedOBBZone();
            _precomputedObbZone.Center = center;
            _precomputedObbZone.Axis = new Vector2[2];
            _precomputedObbZone.Axis[0] = new Vector2(rotMatrix.x, rotMatrix.y); // right
            _precomputedObbZone.Axis[1] = new Vector2(rotMatrix.z, rotMatrix.w); // forward
            _precomputedObbZone.HalfSize = halfSize;
            _precomputedObbZone.RotMatrix = rotMatrix;

            Vector2 bX = new Vector2(1, 0);
            Vector2 bY = new Vector2(0, 1);

            _precomputedObbZone.Extents    =  new float[2];
            _precomputedObbZone.Extents[0] =  Mathf.Abs(Vector2.Dot(_precomputedObbZone.Axis[0] * halfSize.x, bX)) + Mathf.Abs(Vector2.Dot(_precomputedObbZone.Axis[1] * halfSize.y, bX));
            _precomputedObbZone.Extents[1] =  Mathf.Abs(Vector2.Dot(_precomputedObbZone.Axis[0] * halfSize.x, bY)) + Mathf.Abs(Vector2.Dot(_precomputedObbZone.Axis[1] * halfSize.y, bY));
        }

        void OnEnable()
        {
            transform.hasChanged = false;
            _IsZoneVisible = false;

            UpdateTransform();
            KWS_TileZoneManager.LocalWaterZones.Add(this);
        }

        void Update()
        {
            UpdateTransform();
        }

        void OnDisable()
        {
            KWS_TileZoneManager.LocalWaterZones.Remove(this);

            ReleaseTextures();
        }

        void OnDrawGizmosSelected()
        {
            var angles = transform.rotation.eulerAngles;
            angles.x = angles.z = 0;
            transform.rotation = Quaternion.Euler(angles);
            
            if (OverrideColorSettings && UseSphericalBlending)
            {
                transform.localScale = Vector3.one * transform.localScale.x;
            }

            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(0.85f, 0.85f, 0.2f, 0.99f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            //Gizmos.color = new Color(0.85f, 0.85f, 0.2f, 0.03f);
            //Gizmos.DrawCube(Vector3.zero, Vector3.one);

            if (transform.hasChanged)
            {
                transform.hasChanged = false;
                UpdateTransform();
            }
        }

        void OnValidate()
        {
            
        }


        void OnDrawGizmos()
        {   
            if (OverrideColorSettings && UseSphericalBlending)
            {
                transform.localScale = Vector3.one * transform.localScale.x;
            }
            
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(0.15f, 0.85f, 0.2f, 0.99f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
        void ReleaseTextures()
        {
           
        }

    }
}