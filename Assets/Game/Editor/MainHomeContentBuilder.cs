#if UNITY_EDITOR
using System.IO;
using Game.Core;
using Game.Core.Content;
using Game.UI.MainHome;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// 生成 MainHome UGUI 选关页 Prefab，并写入 MainHome 场景。
    /// </summary>
    public static class MainHomeContentBuilder
    {
        const string PrefabFolder = "Assets/Game/Core/Art/UI/Prefabs";
        const string ProductSettingsPath = GameContentPaths.ProductSettingsAssetPath;
        const string MainHomeScenePath = Game.Core.Content.GameContentPaths.MainHomeScenePath;
        const string ItemPrefabPath = PrefabFolder + "/MainHomeGameModeItemView.prefab";

        static Font UiFont =>
            AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Font/AlibabaPuHuiTi-3-85-Bold.ttf")
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("Game/MainHome/Setup Mode Select UI (Prefab + Scene)")]
        public static void SetupModeSelectUi()
        {
            EnsureFolders();
            var productSettings = EnsureProductSettingsAsset();
            var itemPrefab = EnsureItemPrefab(forceRebuild: false);
            WireMainHomeScene(itemPrefab, productSettings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MainHome] 选关 UGUI 已生成。请在 GameProductSettings 中为各模式指定 preview/list 图片与 Scene。");
        }

        [MenuItem("Game/MainHome/Rebuild Secondary Mode Item Prefab")]
        public static void RebuildItemPrefabMenu()
        {
            EnsureFolders();
            EnsureItemPrefab(forceRebuild: true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MainHome] 副模式卡片 Prefab 已重建。");
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Core/Art/UI"))
                EnsureContentFolder("Assets/Game/Core/Art/UI");
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/Game/Core/Art/UI", "Prefabs");
            if (!AssetDatabase.IsValidFolder(GameContentPaths.CoreResourcesRoot))
                EnsureContentFolder(GameContentPaths.CoreResourcesRoot);
        }

        static void EnsureContentFolder(string folderPath)
        {
            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var name = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureContentFolder(parent);
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder(parent, name);
        }

        static GameProductSettings EnsureProductSettingsAsset()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameProductSettings>(ProductSettingsPath);
            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<GameProductSettings>();
            settings.modes.Add(new MainHomeGameModeEntry
            {
                enabled = true,
                modeId = "worm_dodge",
                displayName = "躲避虫子",
                gameplayKind = MainHomeGameplayKind.WormDodge,
                customSceneName = "EscapeWormScene",
            });
            settings.modes.Add(new MainHomeGameModeEntry
            {
                enabled = true,
                modeId = "destroy_city",
                displayName = "摧毁城市",
                gameplayKind = MainHomeGameplayKind.DestroyCity,
                customSceneName = "DestroyCityScene",
            });
            settings.modes.Add(new MainHomeGameModeEntry
            {
                enabled = true,
                modeId = "fishing",
                displayName = "钓鱼",
                gameplayKind = MainHomeGameplayKind.Fishing,
                customSceneName = "FishScene",
            });
            settings.modes.Add(new MainHomeGameModeEntry
            {
                enabled = false,
                modeId = "supermarket_organize",
                displayName = "超市收纳",
                gameplayKind = MainHomeGameplayKind.SupermarketOrganize,
                customSceneName = "SupermarketOrganizeScene",
            });
            settings.modes.Add(new MainHomeGameModeEntry
            {
                enabled = true,
                modeId = "animal_hospital",
                displayName = "动物医院",
                gameplayKind = MainHomeGameplayKind.AnimalHospital,
                customSceneName = "AnimalHospitalScene",
            });
            settings.modes.Add(new MainHomeGameModeEntry
            {
                enabled = false,
                modeId = "tower",
                displayName = "爬塔",
                gameplayKind = MainHomeGameplayKind.Tower,
            });
            AssetDatabase.CreateAsset(settings, ProductSettingsPath);
            return settings;
        }

        static MainHomeGameModeItemView EnsureItemPrefab(bool forceRebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<MainHomeGameModeItemView>(ItemPrefabPath);
            if (existing != null && !forceRebuild)
                return existing;

            if (existing != null)
                AssetDatabase.DeleteAsset(ItemPrefabPath);

            var root = CreateUiObject("MainHomeGameModeItemView", null);
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(270f, 255f);

            var thumbImage = root.AddComponent<Image>();
            thumbImage.color = Color.white;
            thumbImage.preserveAspect = true;

            var button = root.AddComponent<Button>();
            button.targetGraphic = thumbImage;

            var lockedRoot = CreateUiObject("LockedRoot", root.transform);
            StretchFull(lockedRoot.GetComponent<RectTransform>());
            var lockedImage = lockedRoot.AddComponent<Image>();
            lockedImage.color = new Color(1f, 1f, 1f, 0.004f);
            lockedImage.raycastTarget = false;

            var lockIconGo = CreateUiObject("LockIcon", lockedRoot.transform);
            var lockIconRt = lockIconGo.GetComponent<RectTransform>();
            lockIconRt.anchorMin = lockIconRt.anchorMax = new Vector2(1f, 1f);
            lockIconRt.pivot = new Vector2(0.5f, 0.5f);
            lockIconRt.anchoredPosition = new Vector2(-20f, -25f);
            lockIconRt.sizeDelta = new Vector2(32f, 45f);
            var lockIcon = lockIconGo.AddComponent<Image>();
            lockIcon.raycastTarget = false;

            lockedRoot.SetActive(false);

            var itemView = root.AddComponent<MainHomeGameModeItemView>();
            BindItemView(itemView, thumbImage, button, lockedRoot);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<MainHomeGameModeItemView>();
        }

        static void WireMainHomeScene(MainHomeGameModeItemView itemPrefab, GameProductSettings productSettings)
        {
            if (!File.Exists(MainHomeScenePath))
            {
                Debug.LogWarning($"[MainHome] 场景不存在: {MainHomeScenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(MainHomeScenePath, OpenSceneMode.Single);
            var panel = Object.FindObjectOfType<MainHomeGameModeSelectPanel>(true);
            if (panel == null)
            {
                Debug.LogWarning("[MainHome] 场景未找到 MainHomeGameModeSelectPanel / GameSelectPanel。");
                return;
            }

            var so = new SerializedObject(panel);
            so.FindProperty("productSettings").objectReferenceValue = productSettings;
            so.FindProperty("loadSettingsFromResources").boolValue = true;
            so.FindProperty("itemPrefab").objectReferenceValue = itemPrefab;

            var content = panel.transform.Find("Background/Scroll View/Viewport/Content") as RectTransform;
            if (content != null)
                so.FindProperty("secondaryGridContent").objectReferenceValue = content;

            var mainModeImage = panel.transform.Find("Background/MainMode")?.GetComponent<Image>();
            if (mainModeImage != null)
            {
                so.FindProperty("mainModeImage").objectReferenceValue = mainModeImage;
                so.FindProperty("mainModeButton").objectReferenceValue = mainModeImage.GetComponent<Button>();
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MainHome] 已更新场景中的 GameSelectPanel 引用。");
        }

        static void BindItemView(
            MainHomeGameModeItemView itemView,
            Image thumbnail,
            Button button,
            GameObject lockOverlay)
        {
            var so = new SerializedObject(itemView);
            so.FindProperty("thumbnailImage").objectReferenceValue = thumbnail;
            so.FindProperty("clickButton").objectReferenceValue = button;
            so.FindProperty("lockOverlay").objectReferenceValue = lockOverlay;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }
}
#endif
