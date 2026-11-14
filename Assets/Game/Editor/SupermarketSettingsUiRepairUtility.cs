#if UNITY_EDITOR
using System;
using System.Linq;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class SupermarketSettingsUiRepairUtility
    {
        const string ScenePath = "Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity";
        const string PrefabPath = "Assets/Game/Core/Art/UI/Prefabs/Panel/SettingPanel.prefab";
        const string AtlasPath = "Assets/Game/Core/Art/UI/Main.spriteatlas";
        const string UiFolderPath = "Assets/Game/Core/Art/UI/Main";

        [MenuItem("Tools/Supermarket/Repair Settings UI")]
        public static void RepairMenu() => Repair();

        public static void RepairBatch()
        {
            Repair();
            SupermarketOnlyProjectValidator.ValidateBatch();
        }

        static void Repair()
        {
            RepairAtlas();
            RepairSettingsPrefab();
            RepairSceneEntry();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SupermarketSettingsUiRepairUtility] Settings UI repaired without restoring lucky-wheel content.");
        }

        static void RepairAtlas()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, AtlasPath);
            }

            var packables = SpriteAtlasExtensions.GetPackables(atlas);
            if (packables.Length > 0)
                SpriteAtlasExtensions.Remove(atlas, packables);

            var uiFolder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(UiFolderPath);
            if (uiFolder == null)
                throw new InvalidOperationException("Settings UI folder is missing: " + UiFolderPath);

            SpriteAtlasExtensions.Add(atlas, new[] { uiFolder });
            SpriteAtlasExtensions.SetIncludeInBuild(atlas, true);
            SpriteAtlasExtensions.SetPackingSettings(atlas, new SpriteAtlasPackingSettings
            {
                padding = 4,
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = true,
            });
            SpriteAtlasExtensions.SetTextureSettings(atlas, new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear,
            });
            EditorUtility.SetDirty(atlas);
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, BuildTarget.StandaloneWindows64, false);
        }

        static void RepairSettingsPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var panel = root.GetComponentInChildren<SettingsPanel>(true);
                if (panel == null)
                    throw new InvalidOperationException("SettingsPanel controller is missing: " + PrefabPath);

                var mainPanel = panel.mainPanel != null ? panel.mainPanel : Find(root.transform, "MainPanel") as RectTransform;
                var closeButton = panel.closeButton != null ? panel.closeButton : Find(root.transform, "Close")?.GetComponent<Button>();
                panel.mainPanel = mainPanel;
                panel.closeButton = closeButton;

                SetImage(mainPanel, "tanchuangkuang.png", Image.Type.Sliced);
                SetImage(mainPanel != null ? mainPanel.Find("Image") : null, "shezhi1.png");
                if (closeButton != null)
                {
                    var closeImage = closeButton.GetComponent<Image>();
                    closeImage.sprite = LoadSprite("guanbi.png");
                    closeImage.preserveAspect = true;
                    closeButton.targetGraphic = closeImage;
                }

                panel.sfxIconNormal = LoadSprite("yinxiao1.png");
                panel.sfxIconMuted = LoadSprite("yinxiao2.png");
                panel.bgmIconNormal = LoadSprite("yinyue1.png");
                panel.bgmIconMuted = LoadSprite("yinyue2.png");
                if (panel.sfxIcon != null)
                    panel.sfxIcon.sprite = panel.sfxIconNormal;
                if (panel.bgmIcon != null)
                    panel.bgmIcon.sprite = panel.bgmIconNormal;

                RepairSlider(panel.sfxSlider);
                RepairSlider(panel.bgmSlider);
                EditorUtility.SetDirty(panel);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void RepairSlider(Slider slider)
        {
            if (slider == null)
                return;

            SetImage(slider.transform.Find("Background"), "yinliangtiao.png", Image.Type.Sliced);
            if (slider.fillRect != null)
                SetImage(slider.fillRect, "yinliangtiao1.png", Image.Type.Sliced);
            if (slider.handleRect != null)
                SetImage(slider.handleRect, "yinliangjian.png");
        }

        static void RepairSceneEntry()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "Canvas");
            var setting = canvas != null ? canvas.transform.Find("Setting") as RectTransform : null;
            if (setting == null)
                throw new InvalidOperationException("Scene settings entry is missing: Canvas/Setting");

            setting.anchorMin = new Vector2(0f, 1f);
            setting.anchorMax = new Vector2(0f, 1f);
            setting.pivot = new Vector2(0.5f, 0.5f);
            setting.sizeDelta = new Vector2(48f, 48f);
            setting.anchoredPosition = new Vector2(42f, -42f);

            var image = setting.GetComponent<Image>();
            if (image == null)
                image = setting.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite("shezhi.png");
            image.preserveAspect = true;
            image.raycastTarget = true;

            var button = setting.GetComponent<Button>();
            if (button != null)
                button.targetGraphic = image;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Sprite LoadSprite(string fileName)
        {
            var path = UiFolderPath + "/" + fileName;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException("Required settings Sprite is missing: " + path);
            return sprite;
        }

        static void SetImage(Transform transform, string fileName, Image.Type type = Image.Type.Simple)
        {
            if (transform == null)
                return;
            var image = transform.GetComponent<Image>();
            if (image == null)
                return;
            image.sprite = LoadSprite(fileName);
            image.type = type;
            image.preserveAspect = type == Image.Type.Simple;
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                var found = Find(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
#endif
