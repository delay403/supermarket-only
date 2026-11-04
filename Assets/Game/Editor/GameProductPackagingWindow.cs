#if UNITY_EDITOR
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 打包统一面板：勾选游戏模式并同步 Build Settings（本地单包体）。
    /// </summary>
    public sealed class GameProductPackagingWindow : EditorWindow
    {
        const float MinWidth = 520f;
        const float MinHeight = 480f;

        Vector2 _scroll;
        GameProductSettings _product;
        string _status = string.Empty;

        [MenuItem("Game/Product/打包统一面板", false, 0)]
        public static void Open()
        {
            var window = GetWindow<GameProductPackagingWindow>(utility: false, title: "打包统一面板", focus: true);
            window.minSize = new Vector2(MinWidth, MinHeight);
            window.ReloadAssets();
            window.Show();
        }

        void OnEnable() => ReloadAssets();

        void OnFocus() => ReloadAssets();

        void ReloadAssets()
        {
            _product = GameProductPackagingUtility.LoadProductSettings();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            EditorGUILayout.Space(10f);
            DrawGameModesSection();
            EditorGUILayout.Space(10f);
            DrawActionsSection();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawHeader()
        {
            EditorGUILayout.LabelField("打包统一面板", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "在此勾选要打包的游戏模式；修改会写回 GameProductSettings，并同步 Build Settings。\n" +
                "当前为本地单包体模式：启用的玩法场景会写入 Build Settings，运行时本地加载。",
                MessageType.None);

            EditorGUILayout.LabelField(
                $"编辑器 BuildTarget: {EditorUserBuildSettings.activeBuildTarget}",
                EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新", GUILayout.Width(64f)))
                {
                    ReloadAssets();
                    _status = "已从 SO 重新加载。";
                }

                if (GUILayout.Button("打开 GameProductSettings", GUILayout.Width(180f)))
                {
                    if (_product != null)
                    {
                        Selection.activeObject = _product;
                        EditorGUIUtility.PingObject(_product);
                    }
                }
            }
        }

        void DrawGameModesSection()
        {
            EditorGUILayout.LabelField("游戏模式（仅勾选启用）", EditorStyles.boldLabel);

            if (_product == null)
            {
                EditorGUILayout.HelpBox(
                    "未找到 GameProductSettings.asset（Assets/Game/Core/Resources/）。",
                    MessageType.Error);
                return;
            }

            if (_product.modes == null || _product.modes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "modes 列表为空，将使用 Legacy Fallback。请先在 GameProductSettings 中配置模式。",
                    MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            for (var i = 0; i < _product.modes.Count; i++)
            {
                var entry = _product.modes[i];
                if (entry == null)
                    continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    entry.enabled = EditorGUILayout.Toggle(entry.enabled, GUILayout.Width(18f));
                    var title = string.IsNullOrWhiteSpace(entry.displayName)
                        ? (string.IsNullOrWhiteSpace(entry.modeId) ? $"Mode {i}" : entry.modeId)
                        : entry.displayName;
                    EditorGUILayout.LabelField(title);
                    EditorGUILayout.LabelField(entry.gameplayKind.ToString(), EditorStyles.miniLabel, GUILayout.Width(140f));
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_product);
                _status = "游戏模式勾选已修改（未保存到磁盘前可继续调整）。点下方「保存并同步」写入。";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全选", GUILayout.Width(64f)))
                    SetAllModesEnabled(true);
                if (GUILayout.Button("全不选", GUILayout.Width(64f)))
                    SetAllModesEnabled(false);
            }
        }

        void DrawActionsSection()
        {
            EditorGUILayout.LabelField("同步操作", EditorStyles.boldLabel);

            if (GUILayout.Button("保存并同步 Build Settings", GUILayout.Height(32f)))
            {
                if (_product != null)
                    EditorUtility.SetDirty(_product);
                AssetDatabase.SaveAssets();
                GameProductPackagingUtility.SyncBuildScenesNow(_product);
                _status = "已保存 GameProductSettings 并同步 Build Settings。";
                ReloadAssets();
            }

            if (GUILayout.Button("仅同步 Build Settings", GUILayout.Height(26f)))
            {
                GameProductPackagingUtility.SyncBuildScenesNow(_product);
                _status = "Build Settings 已按当前勾选的模式同步。";
                ReloadAssets();
            }

            if (GUILayout.Button("打开 Addressables Settings", GUILayout.Height(26f)))
            {
                var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
                else
                {
                    EditorUtility.DisplayDialog("Addressables", "未找到 Addressables 设置。", "确定");
                }
            }
        }

        void SetAllModesEnabled(bool enabled)
        {
            if (_product?.modes == null)
                return;

            for (var i = 0; i < _product.modes.Count; i++)
            {
                if (_product.modes[i] != null)
                    _product.modes[i].enabled = enabled;
            }

            EditorUtility.SetDirty(_product);
            _status = enabled ? "已全选游戏模式。" : "已取消全部游戏模式。";
        }
    }
}
#endif
