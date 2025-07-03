using System.Collections.Generic;
using UnityEngine;

namespace KWS
{
    [ExecuteInEditMode]
    public class KWS_DynamicWavesObject : MonoBehaviour
    {
        public InteractionTypeEnum InteractionType = InteractionTypeEnum.WaterSource;
        public ForceTypeEnum       ForceType       = ForceTypeEnum.Sphere;
        
        public bool                UseWaterSurfaceIntersection = false;
      
        public float MotionForce  = 1.0f;
        public float ConstantForce = 0.0f;

        public float MotionFlowRate   = 0.0f;
        public float ConstantFlowRate = 0.1f;
        
        public float ConstantDrainRate = 0.1f;

        public bool    UseMeshFilterAsSource = true;
        public Mesh    OverrideObstacleMesh;
        public Vector3 MeshOffset    = Vector3.zero;
        public Vector3 MeshScale = Vector3.one;

      
        public float VelocityStrengthMultiplier = 1.0f;
        public bool UseTransformForwardVelocity = false;
       
        public bool UseSourceColor = false;
        public   Color SourceColor = new Color(0.2f, 0.0f, 0.01f, 1);
        
        
        internal Mesh  CurrentMesh => UseMeshFilterAsSource ? _renderMesh : OverrideObstacleMesh;

        internal DynamicWaveDataStruct DynamicWaveData;
        
        private float   _force;
        private Vector3 _relativePos;
        private Vector3 _relativeScale;
        private Vector3 _lastPos;
        private Vector3 _lastRotation;
        private Mesh    _renderMesh;
        private float   _lastTime;
        private float   _timeDelta;
        
        static Mesh _triangleMesh;

        public enum InteractionTypeEnum
        {
            WaterSource,
            WaterDrain,
            ForceObject,        
            ObstacleObject
        }
        
        public enum ForceTypeEnum
        {
            Sphere,
            Box,
            Triangle
            
        }
        
        
        //don't forget about 32-bit pad!
        public struct DynamicWaveDataStruct
        {
            public uint  ZoneInteractionType;
            public float Force;
            public float WaterHeight;
            public uint  UseColor;

            public Vector4 Size;
            public Vector4 Position;

            public Vector3 ForceDirection;
            public uint    UseWaterIntersection;

            public Vector4   Color;
            public Matrix4x4 MatrixTRS;
        }

        Transform _t;

        
        void OnEnable()
        {
            _t = transform;
            KWS_TileZoneManager.DynamicWavesObjects.Add(this);


            var meshFilter                      = GetComponent<MeshFilter>();
            if (meshFilter) _renderMesh = meshFilter.sharedMesh;
            if (InteractionType == InteractionTypeEnum.ObstacleObject && !UseMeshFilterAsSource && !meshFilter) Debug.LogError(name + " (KWS_DynamicWavesObstacle) Can't find the mesh filter");

            _lastPos = _relativePos = _t.TransformPoint(MeshOffset);
            _force   = 0;

            UpdateData(1, forceUpdate: true);
        }

        void OnDisable()
        {
            KWS_TileZoneManager.DynamicWavesObjects.Remove(this);
            
            KWS_DynamicWavesHelpers.Release();

            if (KWS_TileZoneManager.DynamicWavesObjects.Count == 0)
            {
                KW_Extensions.SafeDestroy(_triangleMesh);
                _triangleMesh = null;
            }
        }
        
        
        bool IsTransformChanged(Transform t)
        {
            if (t.hasChanged)
            {
                t.hasChanged = false;
                return true;
            }
            return false;
        }

        void UpdateData(int frames, bool forceUpdate)
        {
            float   forceMagnitude = 0;
            Vector3 forceDir       = Vector3.up;
            var     currentTime    = KW_Extensions.TotalTime();
            _timeDelta = (currentTime - _lastTime);
            _lastTime  = currentTime;
            
            if (IsTransformChanged(_t) || forceUpdate)
            {
                if (InteractionType == InteractionTypeEnum.ObstacleObject) _relativePos = _t.TransformPoint(MeshOffset);
                else _relativePos                                                       = _t.position;
                
                forceDir       = (_lastPos - _relativePos).normalized;
                forceMagnitude = (_lastPos - _relativePos).magnitude;
                _lastPos       = _relativePos;
                
                forceMagnitude /= (Time.deltaTime * 60f);
               
                var currentRotation = _t.rotation.eulerAngles;

                var rotationForce = Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(currentRotation.x, _lastRotation.x)),
                                              Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(currentRotation.y, _lastRotation.y)),
                                                        Mathf.Abs(Mathf.DeltaAngle(currentRotation.z, _lastRotation.z))));
                _lastRotation  = currentRotation;
                forceMagnitude = Mathf.Max(forceMagnitude, rotationForce * 0.5f);
                
                if (InteractionType == InteractionTypeEnum.ObstacleObject)
                {
                    DynamicWaveData.MatrixTRS = Matrix4x4.TRS(_relativePos, transform.rotation, Vector3.Scale(_t.lossyScale, MeshScale));
                }
                else
                {
                    DynamicWaveData.MatrixTRS = _t.localToWorldMatrix;
                }
            }


            float targetForce = Mathf.Min(1, _force + forceMagnitude);
            _force =  Mathf.Lerp(_force, targetForce, Time.deltaTime * 5f);
            _force *= 0.8f;
          
          
            if (_force < 0.0001f)
                _force = 0f;

            var currentForce = InteractionType switch
            {
                InteractionTypeEnum.WaterSource => _force * MotionFlowRate + ConstantFlowRate,
                InteractionTypeEnum.ForceObject => _force * MotionForce    + ConstantForce,
                InteractionTypeEnum.WaterDrain  => -ConstantDrainRate,
                _                               => 0f
            };

            DynamicWaveData.Position             = _relativePos;
            DynamicWaveData.Size                 = Vector3.Scale(_t.lossyScale, MeshScale);
            DynamicWaveData.Force                = currentForce;
            DynamicWaveData.ForceDirection       = UseTransformForwardVelocity ? _t.forward * Mathf.Clamp01(DynamicWaveData.Force * 10) * VelocityStrengthMultiplier : forceDir * DynamicWaveData.Force * VelocityStrengthMultiplier;
            DynamicWaveData.UseWaterIntersection = (uint)(UseWaterSurfaceIntersection ? 1 : 0);
            DynamicWaveData.Color                = SourceColor;
            DynamicWaveData.UseColor             = UseSourceColor ? 1u : 0u;
        }


        internal void CustomUpdate(int frames)
        {
            UpdateData(frames, forceUpdate: false);
        }
        
        public void DrawGizmo(Color color, bool canDrawMesh)
        {
            Gizmos.color = color;

            if (InteractionType == InteractionTypeEnum.ObstacleObject)
            {
                if (canDrawMesh && CurrentMesh)
                {
                    Gizmos.DrawWireMesh(CurrentMesh, 0, _t.TransformPoint(MeshOffset), _t.rotation, Vector3.Scale(_t.lossyScale, MeshScale));
                }
            }
            else
            {
                if (ForceType == ForceTypeEnum.Sphere)
                {
                    Gizmos.matrix = _t.localToWorldMatrix;
                    Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
                }
                if (ForceType == ForceTypeEnum.Box)
                {
                    Gizmos.matrix = _t.localToWorldMatrix;
                    Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                }

                if (ForceType == ForceTypeEnum.Triangle)
                {
                    if (_triangleMesh == null) _triangleMesh = MeshUtils.CreateTriangle(1);
                    Gizmos.DrawWireMesh(_triangleMesh, 0, _t.position, _t.rotation, _t.lossyScale);
                }
            }
            

           
        }

        void OnDrawGizmos()
        {
            DrawGizmo(Color.blue, canDrawMesh : false);
        }
        void OnDrawGizmosSelected()
        {
            DrawGizmo(Color.yellow, canDrawMesh: true);
        }
    }
}