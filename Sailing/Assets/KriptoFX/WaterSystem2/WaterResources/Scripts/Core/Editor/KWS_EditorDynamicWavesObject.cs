#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using static KWS.KWS_EditorUtils;

namespace KWS
{
    [CustomEditor(typeof(KWS_DynamicWavesObject))]
    internal class KWS_EditorDynamicWavesObject : Editor
    {
        private KWS_DynamicWavesObject _target;


        public override void OnInspectorGUI()
        {
            //var isChanged = DrawDefaultInspector();
            _target = (KWS_DynamicWavesObject)target;

            Undo.RecordObject(_target, "Changed Dynamic Waves Object");

            EditorGUI.BeginChangeCheck();
            EditorGUIUtility.labelWidth = 220;

            _target.InteractionType = (KWS_DynamicWavesObject.InteractionTypeEnum)EnumPopup("Mesh Type", "", _target.InteractionType, "");

            if (_target.InteractionType == KWS_DynamicWavesObject.InteractionTypeEnum.ObstacleObject)
            {
                _target.UseMeshFilterAsSource = Toggle("Use Mesh Filter As Source", "", _target.UseMeshFilterAsSource, "", false);

                if (_target.UseMeshFilterAsSource == false)
                {
                    _target.OverrideObstacleMesh = (Mesh)EditorGUILayout.ObjectField(_target.OverrideObstacleMesh, typeof(Mesh), true);
                }

                _target.MeshOffset = Vector3Field("Mesh Offset", "", _target.MeshOffset, "");
                _target.MeshScale  = Vector3Field("Mesh Scale",  "", _target.MeshScale,  "");
                Line();
            }
            else
            {
                Line();
                _target.ForceType = (KWS_DynamicWavesObject.ForceTypeEnum)EnumPopup("Type", "", _target.ForceType, "");
            }

            if (_target.InteractionType == KWS_DynamicWavesObject.InteractionTypeEnum.WaterSource)
            {
                _target.MotionFlowRate              = Slider("Motion Flow Rate",             "", _target.MotionFlowRate,             0,  1, "");
                _target.ConstantFlowRate            = Slider("Constant Flow Rate",           "", _target.ConstantFlowRate,           0,  1, "");
                _target.VelocityStrengthMultiplier  = Slider("Velocity Strength Multiplier", "", _target.VelocityStrengthMultiplier, -1, 1, "");
                _target.UseTransformForwardVelocity = Toggle("Use Transform Forward Velocity", "", _target.UseTransformForwardVelocity, "", false);

                _target.UseSourceColor = Toggle("Use Source Color", "", _target.UseSourceColor, "");
                if (_target.UseSourceColor)
                {
                    _target.SourceColor = ColorField("Source Color", "", _target.SourceColor, false, false, false, "");
                }
            }

            if (_target.InteractionType == KWS_DynamicWavesObject.InteractionTypeEnum.WaterDrain)
            {
                _target.ConstantDrainRate            = Slider("Drain Rate",           "", _target.ConstantDrainRate,           0,  1, "");
            }

            if (_target.InteractionType == KWS_DynamicWavesObject.InteractionTypeEnum.ForceObject)
            {
                _target.MotionForce                 = Slider("Motion Force",                 "", _target.MotionForce,                0,  3, "");
                _target.ConstantForce               = Slider("Constant Force",               "", _target.ConstantForce,              0,  1, "");
                _target.VelocityStrengthMultiplier  = Slider("Velocity Strength Multiplier", "", _target.VelocityStrengthMultiplier, -1, 1, "");
                _target.UseTransformForwardVelocity = Toggle("Use Transform Forward Velocity", "", _target.UseTransformForwardVelocity, "", false);

                _target.UseWaterSurfaceIntersection = Toggle("Use Water Surface Intersection", "", _target.UseWaterSurfaceIntersection, "", false);
            }

            if (_target.InteractionType == KWS_DynamicWavesObject.InteractionTypeEnum.ForceObject || _target.InteractionType == KWS_DynamicWavesObject.InteractionTypeEnum.WaterSource)
            {
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_target);
                // AssetDatabase.SaveAssets();
            }
        }
    }
}

#endif