using Game.Core;
using Game.EscapeSnake;
using Game.WormDodge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    static class WormDodgeSceneSetupMenu
    {
        const string MenuRoot = "Game/Worm Dodge/";

        [MenuItem(MenuRoot + "Build All UI Prefabs + Setup Scene")]
        static void BuildAllAndSetupScene() => WormDodgeContentBuilder.SetupEscapeWormSceneFull();

        [MenuItem(MenuRoot + "Ensure Director In Scene")]
        static void EnsureDirector()
        {
            var motor = Object.FindObjectOfType<WormChainMotor>();
            var director = Object.FindObjectOfType<WormDodgeDirector>();
            if (director == null)
            {
                var go = new GameObject("WormDodgeDirector");
                director = go.AddComponent<WormDodgeDirector>();
                Undo.RegisterCreatedObjectUndo(go, "Create WormDodgeDirector");
            }

            if (motor != null)
            {
                var so = new SerializedObject(motor);
                so.FindProperty("postChasePatrolCooldownSeconds").floatValue = 15f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            Selection.activeGameObject = director.gameObject;
            Debug.Log("[WormDodge] 已确保场景中存在 WormDodgeDirector，并设置虫子追击冷却 15 秒。");
        }

        [MenuItem(MenuRoot + "Fix All Safe Zone Box Colliders")]
        static void FixAllSafeZoneBoxes()
        {
            var zones = Object.FindObjectsOfType<WormSafeZone>(true);
            for (var i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (zone == null)
                    continue;

                var box = zone.GetComponent<BoxCollider>();
                if (box == null)
                    continue;

                Undo.RecordObject(box, "Fit Safe Zone Box");
                Undo.RecordObject(zone, "Fit Safe Zone Box");
                zone.FitBoxColliderToPlatform();
                EditorUtility.SetDirty(zone);
            }

            Debug.Log($"[WormDodge] 已贴合 {zones.Length} 个安全区 BoxCollider。");
        }

        [MenuItem(MenuRoot + "Create Safe Zone (Box Trigger)")]
        static void CreateSafeZone()
        {
            var go = new GameObject("WormSafeZone");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(6f, 3f, 6f);
            go.AddComponent<WormSafeZone>();
            Undo.RegisterCreatedObjectUndo(go, "Create WormSafeZone");
            Selection.activeGameObject = go;
        }

        [MenuItem(MenuRoot + "Create Floor Settlement Zone")]
        static void CreateFloorZone()
        {
            var go = new GameObject("WormFloorZone");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(8f, 2f, 8f);
            var zone = go.AddComponent<WormFloorSettlementZone>();
            Undo.RegisterCreatedObjectUndo(go, "Create WormFloorZone");
            Selection.activeGameObject = go;
            Debug.Log($"[WormDodge] 已创建楼层结算区。请在 Inspector 设置 {nameof(WormFloorSettlementZone)} 的 coinReward，并将区域放到对应平台高度。");
        }

        [MenuItem(MenuRoot + "Create Floor Reward Popup Zone")]
        static void CreateFloorRewardZone()
        {
            var go = new GameObject("WormFloorRewardZone");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(8f, 2f, 8f);
            go.AddComponent<WormDodgeFloorRewardZone>();
            Undo.RegisterCreatedObjectUndo(go, "Create WormFloorRewardZone");
            Selection.activeGameObject = go;
        }

        [MenuItem(MenuRoot + "Create Finish Zone")]
        static void CreateFinishZone()
        {
            var go = new GameObject("WormFinishZone");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(10f, 3f, 10f);
            go.AddComponent<WormFinishZone>();
            Undo.RegisterCreatedObjectUndo(go, "Create WormFinishZone");
            Selection.activeGameObject = go;
        }

        [MenuItem(MenuRoot + "Select Product Settings (Worm Dodge Only)")]
        static void SelectProductSettings()
        {
            var asset = GameProductSettings.TryLoadFromResources();
            if (asset == null)
            {
                Debug.LogWarning("未找到 Resources/GameProductSettings.asset");
                return;
            }

            asset.productMode = GameProductMode.WormDodgeOnly;
            EditorUtility.SetDirty(asset);
            Selection.activeObject = asset;
            Debug.Log("[WormDodge] 已将 GameProductSettings 设为 WormDodgeOnly。");
        }
    }
}
