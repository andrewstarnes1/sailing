using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace KWS
{
    public partial class WaterSystem
    {
        internal int  BakedFluidsSimPercentPassed;
        internal bool _isFluidsSimBakedMode;

        [Flags]
        public enum WaterSettingsCategory
        {
            ColorSettings       = 1 << 0,
            Waves               = 1 << 1,
            Reflection          = 1 << 2,
            ColorRefraction     = 1 << 3,
            WetEffect           = 1 << 4,
            Foam                = 1 << 5,
            VolumetricLighting  = 1 << 6,
            Caustic             = 1 << 7,
            Underwater          = 1 << 8,
            Mesh                = 1 << 9,
            Rendering           = 1 << 10,
            Transform           = 1 << 11,
            TransformWaterLevel = 1 << 12,
            LocalZone           = 1 << 13,
            SimulationZone      = 1 << 14,
            All                 = ~0
        }


#if KWS_DEBUG

        private void OnDrawGizmos()
        {
            if (DebugAABB) Gizmos.DrawWireCube(WorldSpaceBounds.center, new Vector3(1000, WorldSpaceBounds.size.y, 1000));
            if (DebugQuadtree) DebugHelpers.DebugQuadtree(this);
            if (DebugBuoyancy && Application.isPlaying)
            {
                DebugHelpers.RequestBuoyancy();
                DebugHelpers.DebugBuoyancy();
            }
        }

        private void OnGUI()
        {
            if (DebugDynamicWaves) DebugHelpers.DebugDynamicWaves();
        }

#endif


#if UNITY_EDITOR
        public static void CreateOrFindWaterSystem(Vector3 pos, bool throwErrorIfExist)
        {
            var existing = FindFirstObjectByType<WaterSystem>();
            if (existing != null)
            {
                if (throwErrorIfExist)
                {
                    EditorUtility.DisplayDialog(
                        "Main Water System already exists",
                        "This scene already has a main Water System that controls global water settings. Only one is allowed per scene.",
                        "Ok"
                    );
                    return;
                }

                return;
            }

            var go = new GameObject("Main Water System");
            go.transform.position = pos;
            go.AddComponent<WaterSystem>();

            KWS_EditorUtils.SetEditorCameraPosition(pos);
            go.layer = KWS_Settings.Water.WaterLayer;

            Undo.RegisterCreatedObjectUndo(go, "Create Main Water System");
            Selection.activeObject = go;
        }


        [MenuItem("GameObject/Effects/KWS Water/Main Water System")]
        private static void CreateWaterSystemEditor(MenuCommand menuCommand)
        {
            var pos = SceneView.lastActiveSceneView.camera.transform.TransformPoint(Vector3.forward * 3f);
            CreateOrFindWaterSystem(pos, true);
        }


        [MenuItem("GameObject/Effects/KWS Water/Dynamic Waves Simulation Zone")]
        private static void CreateDynamicWavesSimulationZoneEditor(MenuCommand menuCommand)
        {
            var go  = new GameObject("Dynamic Waves Simulation Zone");
            var pos = SceneView.lastActiveSceneView.camera.transform.TransformPoint(Vector3.forward * 3f);

            CreateOrFindWaterSystem(pos + Vector3.down * 25, false);

            go.transform.position   = pos;
            go.transform.localScale = new Vector3(150, 50, 150);

            KWS_EditorUtils.SetEditorCameraPosition(go.transform.position);
            var simZone = go.AddComponent<KWS_DynamicWavesSimulationZone>();

            var source = FindFirstObjectByType<KWS_DynamicWavesObject>();
            if (!source) CreateEditorDynamicObject(menuCommand, pos);

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }

        [MenuItem("GameObject/Effects/KWS Water/Dynamic Waves Object")]
        private static void CreateDynamicWavesSourceEditor(MenuCommand menuCommand)
        {
            var pos = SceneView.lastActiveSceneView.camera.transform.TransformPoint(Vector3.forward * 3f);
            CreateEditorDynamicObject(menuCommand, pos);
        }

        private static void CreateEditorDynamicObject(MenuCommand menuCommand, Vector3 pos)
        {
            var go = new GameObject("Dynamic Waves Object");
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(5, 5, 5);

            KWS_EditorUtils.SetEditorCameraPosition(go.transform.position);
            var source = go.AddComponent<KWS_DynamicWavesObject>();

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }


        [MenuItem("GameObject/Effects/KWS Water/Local Water Zone")]
        private static void CreateLocalWaterZoneEditor(MenuCommand menuCommand)
        {
            var go            = new GameObject("Local Water Zone");
            var pos           = SceneView.lastActiveSceneView.camera.transform.TransformPoint(Vector3.forward * 3f);
            var waterInstance = Instance;
            if (waterInstance != null) pos.y = Instance.WaterPivotWorldPosition.y;
            else pos.y                       = 0;
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(50, 20, 50);

            KWS_EditorUtils.SetEditorCameraPosition(go.transform.position);
            var simZone = go.AddComponent<KWS_LocalWaterZone>();

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;

            CreateOrFindWaterSystem(go.transform.position, false);
        }

#endif
    }
}